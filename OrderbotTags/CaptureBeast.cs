#if !RB_TC
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.AClasses;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using ff14bot.Pathing;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>Patrols for specified beasts and fights until RB confirms the requested bestiary pet is captured.</summary>
    /// <remarks>
    /// Requires RB 1.0.916+. Ticks the selected combat routine above the capture threshold and
    /// pauses it while applying Capture. No Magitek assembly or catalogue
    /// is required. Optional Magitek auto-capture suppression is restored when the tag exits.
    /// Ground navigation stays in the starting zone. Death and bounded failures stop the bot.
    /// </remarks>
    [XmlElement("CaptureBeast")]
    public class CaptureBeast : LLProfileBehavior
    {
        // Global 7.56 Capture action and Interest Captured status.
        // Capture applies Interest Captured; the pact is awarded on the subsequent kill, not cast.
        private const uint CaptureAction = 44880;
        private const uint CaptureMark = 4626;
        // Allow the capture mark and post-kill unlock to arrive before retrying.
        private const int CaptureRetryMilliseconds = 5000;
        private const int UnlockGraceMilliseconds = 3000;
        // Retry failed paths after nearby objects and navigation have had time to update.
        private const int UnreachableRetryMilliseconds = 30000;
        private const int MaxCaptureRequests = 3;

        private Composite _hook;
        private bool _active;
        private bool _done;
        private bool _initialized;
        private bool _ownsTarget;
        private uint _zone;
        private uint _targetId = GameObjectManager.EmptyGameObject;
        private bool _isCaptureTarget;
        private bool _capturePaused;
        private CombatRoutine _routine;
        private Composite _routineRoot;
        private bool _routineStarted;
        private object _captureSettings;
        private PropertyInfo _useCaptureProperty;
        private bool _previousUseCapture;
        private int _markRequests;
        private int _attempts;
        private int _hotspotIndex;
        private long _lastMarkAt = -CaptureRetryMilliseconds;
        private long _targetStartedAt;
        private long _settleUntil;
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Dictionary<uint, long> _unreachableUntil = new Dictionary<uint, long>();
        private Vector3 _origin;

        /// <summary>Gets or sets the positive bestiary pet ID whose unlock completes this tag.</summary>
        [XmlAttribute("PetId")]
        public int PetId { get; set; }

        /// <summary>Gets or sets eligible GameObject.NpcId values, not BNpcBase IDs; all must yield PetId.</summary>
        [XmlAttribute("NpcIds")]
        [XmlAttribute("NpcId")]
        public uint[] NpcIds { get; set; } = Array.Empty<uint>();

        /// <summary>Gets or sets optional ground patrol points; omitted points search around the starting position.</summary>
        [XmlElement("HotSpots")]
        public List<HotSpot> HotSpots { get; set; } = new List<HotSpot>();

        /// <summary>Gets or sets the maximum distance in yalms from a patrol point for selecting a beast; default 50.</summary>
        [XmlAttribute("Radius")]
        [DefaultValue(50f)]
        public float Radius { get; set; } = 50f;

        /// <summary>Gets or sets the HP percentage at which to mark; default 25 lets the selected routine weaken the beast first.</summary>
        /// <remarks>Routine damage can overshoot the threshold; the tag pauses the routine as soon as the observed HP reaches it.</remarks>
        [XmlAttribute("CaptureHealthPercent")]
        [DefaultValue(25f)]
        public float CaptureHealthPercent { get; set; } = 25f;

        /// <summary>Gets or sets the level gap that forces an immediate mark despite the HP threshold; default 0 disables this override.</summary>
        [XmlAttribute("CaptureAtOnceLevelGap")]
        [DefaultValue(0)]
        public int CaptureAtOnceLevelGap { get; set; } = 0;

        /// <summary>Gets or sets the target level allowance; default 0 restricts captures to the player level or below.</summary>
        /// <remarks>Beasts above the current job level cannot be obtained. Keep this at 0 for capture profiles.</remarks>
        [XmlAttribute("MaxLevelAbovePlayer")]
        [DefaultValue(0)]
        public int MaxLevelAbovePlayer { get; set; } = 0;

        /// <summary>Gets or sets the out-of-combat HP percentage required before pulling; default 70.</summary>
        [XmlAttribute("RestHealthPercent")]
        [DefaultValue(70f)]
        public float RestHealthPercent { get; set; } = 70f;

        /// <summary>Gets or sets the total runtime limit in seconds, including travel, rest, and respawns; default 600.</summary>
        [XmlAttribute("Timeout")]
        [DefaultValue(600)]
        public int Timeout { get; set; } = 600;

        /// <summary>Gets or sets the per-target travel/combat limit in seconds; default 120.</summary>
        [XmlAttribute("TargetTimeout")]
        [DefaultValue(120)]
        public int TargetTimeout { get; set; } = 120;

        /// <summary>Gets or sets the maximum eligible beasts attempted before stopping; default 20.</summary>
        [XmlAttribute("MaxAttempts")]
        [DefaultValue(20)]
        public int MaxAttempts { get; set; } = 20;

        /// <summary>Gets true only after RB confirms the pet unlocked and the character is out of combat.</summary>
        public override bool IsDone => _done;

        /// <summary>Gets true because the tag owns its patrol and capture combat while active.</summary>
        public override bool HighPriority => true;

        /// <summary>Creates a capture tag with matching XML and programmatic defaults.</summary>
        public CaptureBeast() { }

        /// <summary>Validates the profile and installs only this tag's exclusive hook and stop handler.</summary>
        protected override void OnStart()
        {
            Cleanup();
            _done = false;
            _initialized = false;
            _attempts = 0;
            _hotspotIndex = 0;
            _settleUntil = 0;
            _unreachableUntil.Clear();
            ResetTarget();
            if (PetId <= 0 || PetId > byte.MaxValue || !Enum.IsDefined(typeof(BeastmasterPet), (BeastmasterPet)PetId) ||
                NpcIds == null || NpcIds.Length == 0 || NpcIds.Any(id => id == 0) ||
                !IsValidPercent(CaptureHealthPercent) || !IsValidPercent(RestHealthPercent) ||
                float.IsNaN(Radius) || float.IsInfinity(Radius) || Radius <= 0 ||
                Timeout <= 0 || TargetTimeout <= 0 || MaxAttempts <= 0 || CaptureAtOnceLevelGap < 0 || MaxLevelAbovePlayer < 0 ||
                (HotSpots != null && HotSpots.Any(p => p == null || !IsFinite(p.XYZ))))
            {
                Fail("Supply a valid PetId, nonzero NpcIds, finite patrol coordinates/radius, HP percentages in (0,100], nonnegative level limits, and positive time/attempt limits.");
                return;
            }
            if (Core.Me.CurrentJob != ClassJobType.BeastMaster || !ActionManager.HasSpell(CaptureAction))
            {
                Fail("Change to Beastmaster and unlock Capture before using this tag.");
                return;
            }
            _routine = RoutineManager.Current;
            if (_routine == null || _routine.CombatBehavior == null)
            {
                Fail("Select a combat routine that supports Beastmaster before capturing.");
                return;
            }
            // Tick a real composite once per pulse instead of awaiting an entire routine coroutine:
            // a long-running rotation must remain interruptible at the HP threshold.
            _routineRoot = new PrioritySelector(
                _routine.HealBehavior ?? new ActionAlwaysFail(),
                _routine.CombatBuffBehavior ?? new ActionAlwaysFail(),
                new Decorator(_ => !Core.Me.InCombat, _routine.PullBehavior ?? new ActionAlwaysFail()),
                _routine.CombatBehavior);
            SuppressRoutineCapture();
            _origin = Core.Me.Location;
            _zone = WorldManager.ZoneId;
            _clock.Restart();
            _active = true;
            _hook = new ActionRunCoroutine(_ => Pulse());
            TreeRoot.OnStop += OnBotStopped;
            // TreeStart runs after scheduling and before normal routine hooks, including Pull.
            // Do not replace another owner's hook or rely on optional combat-routine capabilities.
            TreeHooks.Instance.InsertHook("TreeStart", 0, _hook);
        }

        /// <summary>Creates a placeholder; the scoped TreeStart hook performs the exclusive work.</summary>
        /// <returns>A successful placeholder for the scheduled profile behavior.</returns>
        protected override Composite CreateBehavior() => new ActionAlwaysSucceed();

        /// <summary>Removes the capture hook when scheduling advances or the profile disposes this tag.</summary>
        protected override void OnDone() => Cleanup();

        /// <summary>Releases ownership and clears success when a profile loop resets the tag.</summary>
        protected override void OnResetCachedDone()
        {
            Cleanup();
            _done = false;
            _initialized = false;
        }

        private void OnBotStopped(BotBase bot) => Cleanup();

        private async Task<bool> Pulse()
        {
            if (!_active)
                return false;
            if (Core.Me.IsDead || Core.Me.CurrentJob != ClassJobType.BeastMaster || WorldManager.ZoneId != _zone)
                return Fail("Capture stopped because the character died, changed jobs, or left the starting zone.");
            if (_clock.Elapsed.TotalSeconds >= Timeout)
                return Fail($"Timed out before pet {PetId} was captured.");

            if (!_initialized)
            {
                // Load the login mask before selecting targets so missing data cannot look like a locked pet.
                if (!await PetManager.EnsureBeastmasterPetUnlockStateAsync(10000))
                    return Fail("Could not load Beastmaster capture data from the server.");
                _initialized = true;
                return true;
            }

            var unlocked = await PetManager.IsBeastmasterPetUnlockedAsync((BeastmasterPet)PetId);
            if (unlocked && !Core.Me.InCombat)
            {
                _done = true;
                Log.Information($"Captured pet {PetId} ({(BeastmasterPet)PetId}); bestiary unlock confirmed.");
                Cleanup();
                return true;
            }

            // Resolve the entity again each pulse; an old native wrapper may be invalid after a kill.
            var target = FindTarget();
            if (_targetId != GameObjectManager.EmptyGameObject && (target == null || !target.IsAlive))
            {
                ResetTarget();
                _settleUntil = _clock.ElapsedMilliseconds + UnlockGraceMilliseconds;
                Navigator.PlayerMover.MoveStop();
                if (_ownsTarget)
                    Core.Me.ClearTarget();
                _ownsTarget = false;
                return true;
            }

            if (target == null)
            {
                // Handle actual attackers even during the server-result grace period. This is a
                // selected-routine fight, with Capture reserved for the requested NPCs.
                target = GameObjectManager.Attackers.OfType<BattleCharacter>()
                    .Where(t => t.IsValid && t.IsAlive && t.CanAttack && t.IsTargetable &&
                        (!_unreachableUntil.TryGetValue(t.ObjectId, out var until) || _clock.ElapsedMilliseconds >= until))
                    .OrderBy(t => t.Distance2D()).FirstOrDefault();
                if (target == null && Core.Me.InCombat)
                {
                    Navigator.PlayerMover.MoveStop();
                    return true;
                }
                if (target == null && (_clock.ElapsedMilliseconds < _settleUntil || Core.Me.CurrentHealthPercent < RestHealthPercent))
                {
                    Navigator.PlayerMover.MoveStop();
                    return true;
                }
                if (target == null)
                {
                    if (_attempts >= MaxAttempts)
                        return Fail($"Pet {PetId} is still locked after {MaxAttempts} attempted beasts.");
                    target = GameObjectManager.GetObjectsOfType<BattleCharacter>()
                        .Where(t => IsEligibleTarget(t) && !t.InCombat && IsWithinPatrolArea(t.Location) &&
                            (!_unreachableUntil.TryGetValue(t.ObjectId, out var until) || _clock.ElapsedMilliseconds >= until))
                        .OrderBy(t => t.Distance2D()).FirstOrDefault();
                }
                if (target == null)
                {
                    Patrol();
                    return true;
                }

                _targetId = target.ObjectId;
                _isCaptureTarget = !unlocked && IsEligibleTarget(target);
                _targetStartedAt = _clock.ElapsedMilliseconds;
                _capturePaused = false;
                _markRequests = 0;
                _lastMarkAt = -CaptureRetryMilliseconds;
                if (_isCaptureTarget)
                    _attempts++;

                Log.Information((_isCaptureTarget ? "Capturing " : "Defending against ") + target.Name + $" (NPC {target.NpcId}).");
            }

            return HandleTarget(target);
        }

        private bool HandleTarget(BattleCharacter target)
        {
            if (!target.IsTargetable || !target.CanAttack)
            {
                // Live Goobbue patrols can lose attackability between selection and approach.
                // This is a lost candidate, not a failed profile. Reuse the path-failure cooldown
                // per entity so a flickering target cannot be selected again on the next pulse.
                Log.Information($"Skipping unavailable target {target.Name}; selecting another beast.");
                _unreachableUntil[target.ObjectId] = _clock.ElapsedMilliseconds + UnreachableRetryMilliseconds;
                if (_isCaptureTarget)
                    _attempts--; // An invalidated candidate must not exhaust the capture-attempt budget.
                ResetTarget();
                Navigator.PlayerMover.MoveStop();
                if (_ownsTarget)
                    Core.Me.ClearTarget();
                _ownsTarget = false;
                return true;
            }
            if (_clock.ElapsedMilliseconds - _targetStartedAt > TargetTimeout * 1000L)
                return Fail($"Target {target.Name} did not finish within {TargetTimeout} seconds.");
            // Patrol movement may mount through RB's normal distance/settings policy. Dismount
            // only after selecting a fight, otherwise every patrol pulse cancels mounted travel.
            if (Core.Me.IsMounted)
            {
                Navigator.PlayerMover.MoveStop();
                ActionManager.Dismount();
                return true;
            }

            if (!_capturePaused)
                target.Target();
            _ownsTarget = true;
            if (target.Distance2D() > 3)
            {
                var result = Navigator.MoveTo(new MoveToParameters(target.Location, "CaptureBeast target") { DistanceTolerance = 3f, UseMount = false });
                if (result == MoveResult.Failed && !Core.Me.InCombat)
                {
                    _unreachableUntil[target.ObjectId] = _clock.ElapsedMilliseconds + UnreachableRetryMilliseconds;
                    ResetTarget();
                    Core.Me.ClearTarget();
                    _ownsTarget = false;
                }
                return true;
            }
            Navigator.PlayerMover.MoveStop();
            target.Face();

            if (_isCaptureTarget && !target.HasAura(CaptureMark))
            {
                var markNow = ShouldCapture(target.CurrentHealthPercent, Core.Me.ClassLevel - target.ClassLevel);
                if (markNow)
                {
                    PauseForCapture(target);
                    if (Core.Me.IsCasting)
                    {
                        ActionManager.StopCasting();
                        return true;
                    }
                    // No damaging action until the mark is observed. Request acceptance is not
                    // confirmation; retry after the arrival window, with a hard per-target bound.
                    if (_clock.ElapsedMilliseconds - _lastMarkAt < CaptureRetryMilliseconds)
                        return true;
                    if (_markRequests >= MaxCaptureRequests)
                        return Fail($"Capture never marked NPC {target.NpcId}; verify that it yields pet {PetId}.");
                    if (ActionManager.CanCast(CaptureAction, target) && ActionManager.DoAction(CaptureAction, target))
                    {
                        Log.Information($"Capture requested on {target.Name} at {target.CurrentHealthPercent:F1}% HP.");
                        _markRequests++;
                        _lastMarkAt = _clock.ElapsedMilliseconds;
                    }
                    return true;
                }

                TickRoutine();
                return true;
            }

            if (_capturePaused)
            {
                Log.Information($"Capture mark confirmed on {target.Name}; resuming the combat routine.");
                _capturePaused = false;
                target.Target();
            }
            TickRoutine();
            return true;
        }

        // Keep combat paused if the target heals while the capture mark is pending.
        private bool ShouldCapture(float healthPercent, int levelGap) => _capturePaused ||
            healthPercent <= CaptureHealthPercent ||
            (CaptureAtOnceLevelGap > 0 && levelGap >= CaptureAtOnceLevelGap);

        private void TickRoutine()
        {
            if (!ReferenceEquals(_routine, RoutineManager.Current))
            {
                Fail("The selected combat routine changed during capture.");
                return;
            }
            if (!_routineStarted)
            {
                _routineRoot.Start(null);
                _routineStarted = true;
            }
            if (_routineRoot.Tick(null) != RunStatus.Running)
                StopRoutine();
        }

        private void StopRoutine()
        {
            if (!_routineStarted)
                return;
            _routineStarted = false;
            _routineRoot.Stop(null);
        }

        private void PauseForCapture(BattleCharacter target)
        {
            if (_capturePaused)
                return;
            _capturePaused = true;
            StopRoutine();
            Log.Information($"Pausing routine at {target.CurrentHealthPercent:F1}% HP to capture {target.Name}.");
            // Remove the player's attack target and heel the familiar while the mark is pending.
            // Capture uses its explicit target object; it does not need the UI target selected.
            Core.Me.ClearTarget();
            if (Core.Me.Pet != null && Core.Me.Pet.IsValid)
                PetManager.DoAction("Heel", Core.Me);
        }

        private void SuppressRoutineCapture()
        {
            // Magitek can mark early (or withhold damage for its own capture threshold).
            // This optional adapter has no binary dependency and changes only the in-memory
            // UseCapture setting, restoring it on completion/stop/reset. Other routines are untouched.
            var type = _routine.GetType().Assembly.GetType("Magitek.Models.BeastMaster.BeastMasterSettings");
            var setting = type?.GetProperty("UseCapture");
            var instance = type?.GetProperty("Instance")?.GetValue(null);
            if (setting == null || !setting.CanRead || !setting.CanWrite || setting.PropertyType != typeof(bool) || instance == null)
                return;
            _captureSettings = instance;
            _useCaptureProperty = setting;
            _previousUseCapture = (bool)setting.GetValue(instance);
            setting.SetValue(instance, false);
        }

        private BattleCharacter FindTarget() => _targetId == GameObjectManager.EmptyGameObject ? null :
            GameObjectManager.GetObjectsOfType<BattleCharacter>().FirstOrDefault(t => t.IsValid && t.ObjectId == _targetId);

        private bool IsEligibleTarget(BattleCharacter target) => target.IsValid && target.IsNpc && target.IsAlive &&
            target.IsTargetable && target.CanAttack && !target.IsFate && !target.IsActiveHuntTarget &&
            target.ClassLevel - Core.Me.ClassLevel <= MaxLevelAbovePlayer && NpcIds.Contains(target.NpcId);

        private bool IsWithinPatrolArea(Vector3 point) => HotSpots == null || HotSpots.Count == 0 ?
            point.Distance(_origin) <= Radius : HotSpots.Any(h => point.Distance(h.XYZ) <= Radius);

        private void Patrol()
        {
            var destination = HotSpots == null || HotSpots.Count == 0 ? _origin : HotSpots[_hotspotIndex].XYZ;
            if (Core.Me.Location.Distance(destination) <= 3)
            {
                Navigator.PlayerMover.MoveStop();
                if (HotSpots != null && HotSpots.Count > 1)
                    _hotspotIndex = (_hotspotIndex + 1) % HotSpots.Count;
                return;
            }
            // Preserve MoveToParameters' default UseMount: RB owns the configured mount,
            // distance threshold and mount availability. Short legs still walk; no second travel loop.
            if (Navigator.MoveTo(new MoveToParameters(destination, "CaptureBeast patrol") { DistanceTolerance = 3f }) == MoveResult.Failed)
                Fail("Ground navigation could not reach a capture patrol point.");
        }

        private void ResetTarget()
        {
            _targetId = GameObjectManager.EmptyGameObject;
            _isCaptureTarget = false;
            _capturePaused = false;
            StopRoutine();
        }

        private static bool IsValidPercent(float value) => !float.IsNaN(value) && value > 0 && value <= 100;
        private static bool IsFinite(Vector3 point) => !float.IsNaN(point.X) && !float.IsInfinity(point.X) &&
            !float.IsNaN(point.Y) && !float.IsInfinity(point.Y) && !float.IsNaN(point.Z) && !float.IsInfinity(point.Z);

        private bool Fail(string reason)
        {
            Log.Error(reason);
            Cleanup();
            TreeRoot.Stop("CaptureBeast: " + reason);
            return true;
        }

        private void Cleanup()
        {
            var wasActive = _active;
            _active = false;
            TreeRoot.OnStop -= OnBotStopped;
            if (_hook != null)
            {
                TreeHooks.Instance.RemoveHook("TreeStart", _hook);
                _hook = null;
            }
            try
            {
                StopRoutine();
            }
            finally
            {
                if (_useCaptureProperty != null)
                {
                    // Do not overwrite an explicit user change back to true while the tag ran.
                    if (!(bool)_useCaptureProperty.GetValue(_captureSettings))
                        _useCaptureProperty.SetValue(_captureSettings, _previousUseCapture);
                    _useCaptureProperty = null;
                    _captureSettings = null;
                }
                _routineRoot = null;
            }
            if (wasActive)
            {
                Navigator.PlayerMover.MoveStop();
                if (_ownsTarget)
                    Core.Me.ClearTarget();
                _ownsTarget = false;
            }
        }
    }
}
#endif