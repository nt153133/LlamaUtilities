using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.NeoProfiles;
using ff14bot.Objects;
using ff14bot.Pathing;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>
    /// Weakens configured mobs using auto-attacks, then uses an item on them at the
    /// requested health percentage.
    /// </summary>
    [XmlElement("WeakenAndUseItem")]
    public class WeakenAndUseItem : LLProfileBehavior
    {
        private readonly Dictionary<uint, int> _itemAttempts = new Dictionary<uint, int>();
        private readonly HashSet<uint> _exhaustedItemTargets = new HashSet<uint>();
        private readonly Dictionary<CapabilityFlags, CapabilityManagerHandle> _capabilityHandles =
            new Dictionary<CapabilityFlags, CapabilityManagerHandle>();
        private readonly CapabilityManagerHandle _itemUseMovementHandle = CapabilityManager.CreateNewHandle();

        private Composite _combatLogic;
        private Composite _auraFilterLogic;
        private Composite _nearestCombatTargetLogic;
        private Composite _nearestTargetLogic;
        private Func<bool> _whileCondition;
        private bool _finishing;
        private bool _isDone;
        private bool _usingItem;
        private DateTime _nextItemAttemptUtc = DateTime.MinValue;

        [XmlAttribute("ItemId")]
        public uint ItemId { get; set; }

        [XmlAttribute("NpcIds")]
        [XmlAttribute("NpcId")]
        public int[] NpcIds { get; set; }

        [XmlAttribute("XYZ")]
        public Vector3 XYZ { get; set; }

        [XmlAttribute("Radius")]
        [XmlAttribute("SearchRadius")]
        [DefaultValue(50f)]
        public float Radius { get; set; } = 50f;

        [XmlAttribute("UseHealthPercent")]
        [DefaultValue(10f)]
        public float UseHealthPercent { get; set; } = 10f;

        [XmlAttribute("UseDistance")]
        [DefaultValue(3.24f)]
        public float UseDistance { get; set; } = 3.24f;

        [XmlAttribute("StopAuraId")]
        [DefaultValue(0)]
        public int StopAuraId { get; set; }

        [XmlAttribute("BlacklistDuration")]
        [DefaultValue(180)]
        public int BlacklistDuration { get; set; } = 180;

        [XmlAttribute("MaxItemAttempts")]
        [DefaultValue(3)]
        public int MaxItemAttempts { get; set; } = 3;

        [XmlAttribute("While")]
        [XmlAttribute("Condition")]
        public string While { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        protected override void OnStart()
        {
            if (NpcIds == null || NpcIds.Length == 0)
            {
                Log.Error("NpcIds is required.");
                _isDone = true;
                return;
            }

            if (ItemId == 0)
            {
                Log.Error("ItemId is required.");
                _isDone = true;
                return;
            }

            try
            {
                _whileCondition = string.IsNullOrWhiteSpace(While)
                    ? (() => true)
                    : ScriptManager.GetCondition(While);
            }
            catch (Exception ex)
            {
                Logging.WriteDiagnostic(ScriptManager.FormatSyntaxErrorException(ex));
                TreeRoot.Stop();
                throw;
            }

            var hotspot = new HotSpot(XYZ, Radius);
            NeoProfileManager.CurrentGrindArea = new GrindArea
            {
                Hotspots = new List<HotSpot> { hotspot },
                TargetMobs = NpcIds.Select(id => new TargetMob { Id = id }).ToList()
            };

            // RebornBuddy documents SpecialAttacks specifically for mobs which must
            // be weakened before using a quest item. A compliant routine will use
            // only auto-attacks while this restriction is active.
            foreach (var capability in ControlledDamageCapabilities)
            {
                var handle = CapabilityManager.CreateNewHandle();
                CapabilityManager.Update(
                    handle,
                    capability,
                    () => !_isDone,
                    "WeakenAndUseItem requires controlled damage");
                _capabilityHandles[capability] = handle;
            }

            _combatLogic = new ActionRunCoroutine(_ => HandleCombat());
            TreeHooks.Instance.InsertHook("PreCombatLogic", 0, _combatLogic);
            _auraFilterLogic = new ActionRunCoroutine(_ => FilterAuraTargets());
            TreeHooks.Instance.InsertHook("TreeStart", 0, _auraFilterLogic);
            _nearestCombatTargetLogic = new ActionRunCoroutine(_ => SelectNearestCombatTarget());
            TreeHooks.Instance.InsertHook("SetCombatPoi", 0, _nearestCombatTargetLogic);
            _nearestTargetLogic = new ActionRunCoroutine(_ => SelectNearestTarget());
            TreeHooks.Instance.InsertHook("HotspotPoi", 0, _nearestTargetLogic);

            Log.Information($"Started for NPC IDs {string.Join(",", NpcIds)}. " +
                            $"Use item {ItemId} at or below {UseHealthPercent:0.##}% health; " +
                            $"stop aura {StopAuraId}.");
        }

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                new Decorator(_ => !ShouldContinue || _finishing,
                    new ActionRunCoroutine(_ => Finish())),
                new HookExecutor("HotspotPoi"));
        }

        protected override void OnDone()
        {
            RemoveHooksAndRestrictions();
            NeoProfileManager.CurrentGrindArea = null;

        }

        protected override void OnResetCachedDone()
        {
            _finishing = false;
            _isDone = false;
            _usingItem = false;
            _nextItemAttemptUtc = DateTime.MinValue;
            _itemAttempts.Clear();
            _exhaustedItemTargets.Clear();
        }

        private bool ShouldContinue
        {
            get
            {
                try
                {
                    return _whileCondition == null || _whileCondition();
                }
                catch (Exception ex)
                {
                    Log.Error($"While condition failed: {ex.Message}");
                    return false;
                }
            }
        }

        private BagSlot Item => InventoryManager.FilledSlots.FirstOrDefault(slot => slot.RawItemId == ItemId);

        private async Task<bool> HandleCombat()
        {
            if (_isDone)
                return false;

            if (!ShouldContinue || _finishing)
            {
                Navigator.PlayerMover.MoveStop();
                Core.Player.ClearTarget();
                Poi.Clear("WeakenAndUseItem condition ended");
                return true;
            }

            var target = Poi.Current?.BattleCharacter;
            if (target == null || !target.IsValid || !NpcIds.Contains((int)target.NpcId))
                return false;

            // Three failed item attempts only disqualifies the mob from further item
            // use. If it is still hostile, leave it available to the standard combat
            // routine instead of suppressing combat with a global blacklist.
            if (_exhaustedItemTargets.Contains(target.ObjectId))
                return false;

            if (HasStopAura(target))
            {
                StopAttackingAndBlacklist(target, "Stop aura detected");
                return true;
            }

            if (target.CurrentHealthPercent > UseHealthPercent)
                return false;

            // Clearing the target immediately halts auto-attacks before we yield to
            // movement or item-use coroutines.
            Navigator.PlayerMover.MoveStop();
            Core.Player.ClearTarget();
            await Coroutine.Yield();

            if (!target.IsValid || !target.IsAlive || HasStopAura(target))
            {
                StopAttackingAndBlacklist(target, "Target no longer needs the item");
                return true;
            }

            if (target.Distance() > UseDistance)
            {
                await Navigator.MoveToAndStopAsync(
                    new MoveToParameters(target.Location, "Moving into item range") { UseMount = false },
                    () => !target.IsValid || !target.IsAlive || HasStopAura(target) || !ShouldContinue,
                    UseDistance);

                if (!target.IsValid || !target.IsAlive || HasStopAura(target))
                {
                    StopAttackingAndBlacklist(target, "Target no longer needs the item");
                    return true;
                }

                // Moving targets can leave the completed path's endpoint. Recalculate
                // on the next pulse instead of trying the item while out of range.
                if (target.Distance() > UseDistance)
                {
                    return true;
                }
            }

            Navigator.PlayerMover.MoveStop();
            Navigator.Clear();

            if (DateTime.UtcNow < _nextItemAttemptUtc)
            {
                return true;
            }

            BeginItemUseMovementLock();
            try
            {
                target.Target();
                await Coroutine.Sleep(100);

                var item = Item;
                if (item == null)
                {
                    Log.Error($"Could not find item {ItemId}; combat remains suppressed for this target.");
                    return true;
                }

                Log.Information($"Using {item.EnglishName} on {target.Name} at {target.CurrentHealthPercent:0.##}% health.");
                var targetObjectId = target.ObjectId;
                var attemptNumber = RecordItemAttempt(targetObjectId);
                Log.Information($"Item attempt {attemptNumber}/{EffectiveMaxItemAttempts} on {target.Name} ({targetObjectId:X}).");
                var useStarted = item.Item.IsGroundTargeting
                    ? item.UseItem(target.Location)
                    : item.UseItem(target);

                if (!useStarted)
                {
                    Log.Warning($"Item {ItemId} did not start. Retrying on the next combat pulse.");
                    Core.Player.ClearTarget();
                    _nextItemAttemptUtc = DateTime.UtcNow.AddMilliseconds(750);
                    await HoldPositionUntil(
                        TimeSpan.FromMilliseconds(750),
                        () => HasStopAura(target) || !target.IsValid || !target.IsAlive);

                    if (attemptNumber >= EffectiveMaxItemAttempts)
                    {
                        AbandonItemAttempts(target);
                    }

                    return true;
                }

                _nextItemAttemptUtc = DateTime.UtcNow.AddSeconds(2);
                await HoldPositionUntil(
                    TimeSpan.FromSeconds(5),
                    () => Core.Player.IsCasting || HasStopAura(target) || !target.IsValid);
                await HoldPositionUntil(
                    TimeSpan.FromSeconds(15),
                    () => !Core.Player.IsCasting || HasStopAura(target) || !target.IsValid);
                await HoldPositionUntil(
                    TimeSpan.FromSeconds(3),
                    () => HasStopAura(target) || !target.IsValid || !target.IsAlive);

                if (!target.IsValid || !target.IsAlive || HasStopAura(target))
                {
                    _itemAttempts.Remove(targetObjectId);
                    StopAttackingAndBlacklist(target, "Item applied");
                }
                else if (attemptNumber >= EffectiveMaxItemAttempts)
                {
                    AbandonItemAttempts(target);
                }
                else
                {
                    Core.Player.ClearTarget();
                    Log.Warning($"Aura {StopAuraId} was not detected after using item {ItemId}; the tag will retry without attacking further.");
                }
            }
            finally
            {
                EndItemUseMovementLock();
            }

            return true;
        }

        private int EffectiveMaxItemAttempts => Math.Max(1, MaxItemAttempts);

        private int RecordItemAttempt(uint objectId)
        {
            _itemAttempts.TryGetValue(objectId, out var attempts);
            attempts++;
            _itemAttempts[objectId] = attempts;
            return attempts;
        }

        private void AbandonItemAttempts(BattleCharacter target)
        {
            if (target == null)
                return;

            var objectId = target.ObjectId;
            _exhaustedItemTargets.Add(objectId);
            Navigator.PlayerMover.MoveStop();
            Core.Player.ClearTarget();
            Poi.Clear($"WeakenAndUseItem: Maximum item attempts ({EffectiveMaxItemAttempts}) reached");
            Log.Warning($"Moving on from {target.Name} ({objectId:X}) after {EffectiveMaxItemAttempts} item attempts. " +
                        "It remains available to standard combat if it continues attacking.");
        }

        private void BeginItemUseMovementLock()
        {
            _usingItem = true;
            var suppressedAvoids = SuppressActiveAvoidance();
            Navigator.Clear();
            Navigator.PlayerMover.MoveStop();

            if (suppressedAvoids > 0)
            {
                Log.Information($"Suppressed {suppressedAvoids} active avoidance area(s) during item use.");
            }

            CapabilityManager.Update(
                _itemUseMovementHandle,
                CapabilityFlags.Movement,
                () => _usingItem,
                "WeakenAndUseItem is applying the quest item");
        }

        private void EndItemUseMovementLock()
        {
            _usingItem = false;
            CapabilityManager.Clear(
                _itemUseMovementHandle,
                CapabilityFlags.Movement,
                "WeakenAndUseItem item use ended");

            // Avoidance can retain a blocked or run-out path after the movement lock
            // ends. Reset both providers so normal combat gets a clean navigation
            // pulse immediately afterward.
            AvoidanceManager.ResetNavigation();
            Navigator.Clear();
            Navigator.PlayerMover.MoveStop();
        }

        private async Task HoldPositionUntil(TimeSpan timeout, Func<bool> stopCondition)
        {
            var endTime = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < endTime && !stopCondition())
            {
                // Avoidance has its own movement provider and can ignore combat-routine
                // capability flags. Cancel its path and stop the mover throughout the
                // brief protected item-use window.
                SuppressActiveAvoidance();
                Navigator.Clear();
                Navigator.PlayerMover.MoveStop();
                await Coroutine.Sleep(50);
            }

            Navigator.PlayerMover.MoveStop();
        }

        private static int SuppressActiveAvoidance()
        {
            var activeAvoidCount = AvoidanceManager.AvoidInfos.Count(avoid => avoid.CanRun);
            AvoidanceManager.RemoveAllAvoids(avoid => avoid.CanRun);
            AvoidanceManager.ResetNavigation();
            return activeAvoidCount;
        }

        private bool HasStopAura(BattleCharacter target)
        {
            return StopAuraId > 0 && target != null && target.IsValid && target.HasAura((uint)StopAuraId);
        }

        private async Task<bool> FilterAuraTargets()
        {
            if (StopAuraId <= 0 || NpcIds == null)
                return false;

            var completedTargets = GameObjectManager.GetObjectsOfType<BattleCharacter>()
                .Where(target => target.IsValid &&
                                 target.IsAlive &&
                                 NpcIds.Contains((int)target.NpcId) &&
                                 HasStopAura(target))
                .ToArray();

            foreach (var completedTarget in completedTargets)
            {
                _itemAttempts.Remove(completedTarget.ObjectId);

                if (!Blacklist.Contains(completedTarget.ObjectId, BlacklistFlags.All))
                {
                    Blacklist.Add(
                        completedTarget.ObjectId,
                        BlacklistFlags.All,
                        TimeSpan.FromSeconds(BlacklistDuration),
                        "WeakenAndUseItem: Stop aura pre-filter");
                }

                if (Poi.Current?.BattleCharacter?.ObjectId == completedTarget.ObjectId)
                {
                    Core.Player.ClearTarget();
                    Poi.Clear("WeakenAndUseItem: Aura target filtered");
                }
            }

            await Coroutine.Yield();
            return false;
        }

        private async Task<bool> SelectNearestTarget()
        {
            if (_isDone || !ShouldContinue || NpcIds == null)
                return false;

            var attackerIds = new HashSet<uint>(GameObjectManager.Attackers
                .Where(attacker => attacker != null && attacker.IsValid && attacker.IsAlive)
                .Select(attacker => attacker.ObjectId));

            var nearestTarget = GameObjectManager.GetObjectsOfType<BattleCharacter>()
                .Where(target => target.IsValid &&
                                 target.IsAlive &&
                                 target.IsVisible &&
                                 target.IsTargetable &&
                                 target.CanAttack &&
                                 NpcIds.Contains((int)target.NpcId) &&
                                 !HasStopAura(target) &&
                                 !_exhaustedItemTargets.Contains(target.ObjectId) &&
                                 !Blacklist.Contains(target.ObjectId, BlacklistFlags.All) &&
                                 target.Location.Distance2D(XYZ) <= Radius)
                .OrderByDescending(target => attackerIds.Contains(target.ObjectId))
                .ThenBy(target => target.DistanceSqr(Core.Player.Location))
                .FirstOrDefault();

            if (nearestTarget == null)
                return false;

            Poi.Current = new Poi(nearestTarget, PoiType.Kill);
            Log.Information($"Selected nearest target {nearestTarget.Name} ({nearestTarget.NpcId}) " +
                            $"at {nearestTarget.Distance():0.##} yalms.");
            await Coroutine.Yield();
            return true;
        }

        private async Task<bool> SelectNearestCombatTarget()
        {
            if (_isDone || !ShouldContinue)
                return false;

            // SetCombatPoi is where OrderBot's normal combat targeting was replacing
            // the nearby target selected by HotspotPoi. Own this choice only while
            // something is actively attacking us, then leave all movement and combat
            // execution to the standard combat routine.
            var nearestAttacker = GameObjectManager.Attackers
                .Where(target => target != null &&
                                 target.IsValid &&
                                 target.IsAlive &&
                                 target.IsVisible &&
                                 target.IsTargetable &&
                                 target.CanAttack &&
                                 !Blacklist.Contains(target.ObjectId, BlacklistFlags.All) &&
                                 !(NpcIds.Contains((int)target.NpcId) && HasStopAura(target)))
                .OrderBy(target => target.DistanceSqr(Core.Player.Location))
                .FirstOrDefault();

            if (nearestAttacker == null)
                return false;

            var currentTarget = Poi.Current?.BattleCharacter;
            if (currentTarget == null || currentTarget.ObjectId != nearestAttacker.ObjectId)
            {
                Poi.Current = new Poi(nearestAttacker, PoiType.Kill);
                Log.Information($"Prioritizing closest attacker {nearestAttacker.Name} " +
                                $"({nearestAttacker.NpcId}) at {nearestAttacker.Distance():0.##} yalms.");
            }

            await Coroutine.Yield();
            return true;
        }

        private void StopAttackingAndBlacklist(BattleCharacter target, string reason)
        {
            Navigator.PlayerMover.MoveStop();
            Core.Player.ClearTarget();

            if (target != null && target.IsValid)
            {
                Blacklist.Add(
                    target.ObjectId,
                    BlacklistFlags.All,
                    TimeSpan.FromSeconds(BlacklistDuration),
                    $"WeakenAndUseItem: {reason}");
            }

            Poi.Clear($"WeakenAndUseItem: {reason}");
        }

        private async Task<bool> Finish()
        {
            _finishing = true;
            Navigator.PlayerMover.MoveStop();
            Core.Player.ClearTarget();
            Poi.Clear("WeakenAndUseItem finished");

            RemoveHooksAndRestrictions();
            NeoProfileManager.CurrentGrindArea = null;
            _isDone = true;
            Log.Information("Finished.");
            await Coroutine.Yield();
            return true;
        }

        private void RemoveHooksAndRestrictions()
        {
            EndItemUseMovementLock();

            if (_combatLogic != null)
            {
                TreeHooks.Instance.RemoveHook("PreCombatLogic", _combatLogic);
                _combatLogic = null;
            }

            if (_auraFilterLogic != null)
            {
                TreeHooks.Instance.RemoveHook("TreeStart", _auraFilterLogic);
                _auraFilterLogic = null;
            }

            if (_nearestCombatTargetLogic != null)
            {
                TreeHooks.Instance.RemoveHook("SetCombatPoi", _nearestCombatTargetLogic);
                _nearestCombatTargetLogic = null;
            }

            if (_nearestTargetLogic != null)
            {
                TreeHooks.Instance.RemoveHook("HotspotPoi", _nearestTargetLogic);
                _nearestTargetLogic = null;
            }

            foreach (var restriction in _capabilityHandles)
            {
                CapabilityManager.Clear(
                    restriction.Value,
                    restriction.Key,
                    "WeakenAndUseItem ended");
            }

            _capabilityHandles.Clear();
        }

        private static readonly CapabilityFlags[] ControlledDamageCapabilities =
        {
            CapabilityFlags.SpecialAttacks,
            CapabilityFlags.Aoe,
            CapabilityFlags.GapCloser,
            CapabilityFlags.PetUse,
            CapabilityFlags.PetSummoning
        };

    }
}
