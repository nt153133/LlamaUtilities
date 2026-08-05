using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.BotBases;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.NeoProfiles;
using ff14bot.Objects;
using ff14bot.Pathing;
using ff14bot.RemoteWindows;
using ff14bot.Settings;
using LlamaLibrary.Helpers;
using TreeSharp;
using Action = TreeSharp.Action;

/***
 * Modified version of Y2krazy's Fate tag
 *
 *
 */

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLFate")]
    public class LLFate : LLProfileBehavior
    {
        private bool _done;
        private int _min;
        private int _max;
        private int _timeout;
        private bool hasExplicitMinLevel;
        private bool hasExplicitMaxLevel;

        [XmlAttribute("MaxLevel")]
        public string MaxLevel { get; set; }

        [XmlAttribute("MinLevel")]
        public string MinLevel { get; set; }

        [XmlAttribute("While")]
        [XmlAttribute("while")]
        public string WhileCondition { get; set; }

        [XmlAttribute("UseFlight")]
        [XmlAttribute("useflight")]
        [DefaultValue(true)]
        public bool UseFlight { get; set; }

        [XmlAttribute("FateIDs")]
        [XmlAttribute("FateIds")]
        [XmlAttribute("FateID")]
        [XmlAttribute("FateId")]
        [DefaultValue(new int[0])]
        public int[] FateIds { get; set; }

        [XmlAttribute("BlacklistID")]
        [XmlAttribute("BlacklistId")]
        [XmlAttribute("BlacklistIDs")]
        [XmlAttribute("BlacklistIds")]
        [XmlAttribute("Blacklist")]
        [XmlAttribute("blacklist")]
        [DefaultValue(new int[0])]
        public int[] BlacklistIds { get; set; }

        [XmlAttribute("Timeout")]
        [DefaultValue("600")]
        public string Timeout { get; set; }

        [XmlAttribute("MinProgress")]
        [DefaultValue(0)]
        public int MinProgress { get; set; }

        [XmlAttribute("CheckShareFate")]
        [DefaultValue(false)]
        public bool SharedFate { get; set; }

        /// <summary>
        /// Gets or sets whether LLFate should kill nearby non-FATE enemies while waiting for an eligible FATE.
        /// Hunting is opt-in so existing profiles remain focused exclusively on their configured FATE objectives.
        /// </summary>
        [XmlAttribute("HuntBetweenFates")]
        [DefaultValue(false)]
        public bool HuntBetweenFates { get; set; }

        /// <summary>
        /// Gets or sets the maximum distance, in yalms, at which LLFate may select an idle-hunting target.
        /// The bound prevents downtime hunting from pulling the character far enough away to delay a new FATE.
        /// </summary>
        [XmlAttribute("HuntRadius")]
        [DefaultValue(50f)]
        public float HuntRadius { get; set; } = 50f;

        private readonly FatebotSettings fatebotInstance = FatebotSettings.Instance;

        //private int timeout = 100;
        private DateTime saveNow = DateTime.Now;
        public override bool IsDone => _done;

        //some Statistics
        private FateData currentfate;
        // FateData is a live wrapper over a reusable client-table slot. It is retained only long
        // enough to select an event; all state used across bot pulses is copied into primitives.
        private FateIconType fateIcon;
        private int fateMaxLevel;
        private int fatesDone;
        private int participatedFateEndings;
        private int mobsHunted;
        private int died;
        private bool wasDead;
        private bool sessionSummaryReported;
        private uint huntedTargetObjectId;
        private Vector3? idleHuntAnchor;
        private Vector3? defensiveCombatAnchor;
        private Vector3? postFateCombatLeashAnchor;
        private DateTime combatRetirementDeadlineUtc;
        // Game-object IDs are transient and may be reused after an actor leaves the object table.
        // Suppress an abandoned hunt target only long enough for RB's combat scheduling to unwind;
        // retaining the ID for the whole tag run could incorrectly reject an unrelated later actor.
        private readonly Dictionary<uint, DateTime> abandonedIdleHuntTargetsUntil = new Dictionary<uint, DateTime>();
        private uint llFateZoneId;
        private bool zoneSafetyTriggered;
        private bool participatedInTrackedFate;
        private uint rejectedCenterLandingFateId;
        private readonly HashSet<uint> rejectedFateIds = new HashSet<uint>();
        private readonly Dictionary<uint, DateTime> landingFailureCooldowns = new Dictionary<uint, DateTime>();
        // Policy skips are stable for a FATE ID during one tag run. Remember their diagnostics so
        // a low-progress or unsupported event does not emit the same message every bot pulse.
        private readonly HashSet<uint> reportedPolicySkipFateIds = new HashSet<uint>();
        // FATE wrappers commonly disappear before their remaining attackers retire. Remembering
        // a successfully validated event lets defensive cleanup finish those known-safe actors
        // without weakening the fail-closed rule for unknown or KillHandIn event ownership.
        private readonly HashSet<uint> supportedFateIds = new HashSet<uint>();
        private readonly HashSet<uint> unsupportedFateIds = new HashSet<uint>();

        // Visibility only means that the client has rendered an actor; it does not prove that a
        // combat action can reach it. Give transient occlusion a short grace period, then suppress
        // the actor long enough for another target to be selected instead of immediately cycling
        // back to the same unreachable platform or room.
        private static readonly TimeSpan LineOfSightGracePeriod = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan LineOfSightSuppressionPeriod = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan CombatRetirementTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan IdleHuntAbandonmentPeriod = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan LineOfSightApproachTimeout = TimeSpan.FromSeconds(10);
        // FateData is frame-cached and may briefly disappear or expose partially refreshed fields.
        // Require a sustained invalid state before abandoning an event so one client-table refresh
        // cannot be mistaken for completion or split a running travel coroutine from tracked state.
        private static readonly TimeSpan FateSnapshotTransitionGracePeriod = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Immutable primitive copy of the live FATE-table fields LLFate consumes in one pulse.
        /// FateData wraps a reusable native slot, so retaining or repeatedly dereferencing it can
        /// combine fields from different client refresh states when a FATE spawns or retires.
        /// </summary>
        private struct FateSnapshot
        {
            public bool IsValid;
            public uint Id;
            public string Name;
            public Vector3 Location;
            public float Radius;
            public int Level;
            public int MaxLevel;
            public FateStatus Status;
            public TimeSpan TimeLeft;
            public FateIconType Icon;
            public int Progress;
        }
        // A failed landing describes the current terrain/event state, not the FATE definition for
        // the whole session. A bounded cooldown prevents an immediate retry loop while allowing a
        // later spawn of the same recurring FATE ID to be considered normally.
        private static readonly TimeSpan LandingFailureCooldown = TimeSpan.FromMinutes(5);
        private const float FateTargetLeashTolerance = 8f;
        // When no FATE or optional hunt owns combat, attackers are admitted only near the point at
        // which defensive combat began. This prevents RB's independent targeting pulse from turning
        // incidental aggro into an unbounded cross-zone chase while LLFate is idle.
        private const float DefensiveCombatLeash = 20f;
        // Small-radius FATEs can report a radius at or below the historical ten-yalm inset.
        // Clamp the travel destination instead of producing a zero or negative arrival radius.
        private const float MinimumFateArrivalDistance = 1f;
        private const int MaximumCenterLandingAssessments = 3;
        private const int MaximumLandingPointScans = 5;
        // Probe close offsets first so recovery stays near active enemies while still escaping an
        // unlandable prop, water cell, or exact event centroid. Y remains the anchor's ground Y;
        // CanLand performs the authoritative native terrain validation from the elevated point.
        private static readonly Vector3[] LandingProbeOffsets =
        {
            Vector3.Zero,
            new Vector3(6f, 0f, 0f), new Vector3(-6f, 0f, 0f),
            new Vector3(0f, 0f, 6f), new Vector3(0f, 0f, -6f),
            new Vector3(6f, 0f, 6f), new Vector3(6f, 0f, -6f),
            new Vector3(-6f, 0f, 6f), new Vector3(-6f, 0f, -6f),
            new Vector3(12f, 0f, 0f), new Vector3(-12f, 0f, 0f),
            new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, -12f),
        };
        private uint landingRecoveryFateId;
        private int centerLandingAssessmentFailures;
        private int landingPointScanFailures;
        private readonly Dictionary<uint, DateTime> lineOfSightSuppressedUntil = new Dictionary<uint, DateTime>();
        private uint obstructedPoiObjectId;
        private DateTime obstructedPoiSinceUtc;
        private DateTime trackedFateUnavailableSinceUtc;
        private DateTime trackedFateInvalidSinceUtc;
        private Composite safetyPulseHook;

        //----------------------
        public bool IsCompleted = false;

        //Fate class variables
        public static Vector3 Position = new Vector3(0f, 0f, 0f);

        private uint fateid = 0;
        private float fateRadius;
        private string fateName = "";
        private string fateStatus = "";
        private int forelornMaiden = 6737;
        private int theForlorn = 6738;

        //-------
        public static int currentstep = 0; //currentstep 1 we are in a fate / currentstep 0 we are not in a fate

        private static readonly Stopwatch ClusterTimer = Stopwatch.StartNew();

        // After a FATE retires, combat can linger briefly even though its enemies are already
        // retreating. Keep defensive cleanup local to the handoff point; following a retreating
        // attacker here makes the combat routine run indefinitely away from the completed FATE.
        private const float PostFateCombatLeash = 20f;
        // FATE coordinates describe an event centroid, not guaranteed walkable terrain. Keep the
        // established travel altitude, then validate enemy positions only after reaching object range.
        private const float FateLandingHeight = 15f;
        private const int FateEnemyLandingScanMilliseconds = 3000;
        protected Func<bool> condition;

        public LLFate() : base()
        {
        }

        private bool ShouldStop()
        {
            var compiledCondition = GetCondition();
            return compiledCondition != null && !compiledCondition();
        }

        /// <summary>
        /// Runs state retirement and target-safety checks from OrderBot's always-pulsed PoiAction
        /// slot. Normal profile behavior is skipped while a Kill POI exists, so keeping these
        /// checks inside the LLFate behavior tree would make LOS, hunt-radius, and FATE-end leashes
        /// ineffective precisely while combat owns the character.
        /// </summary>
        /// <returns><see cref="RunStatus.Failure"/> so ordinary OrderBot processing continues.</returns>
        private RunStatus RunSafetyPulse()
        {
            // PoiAction also pulses through loading and death transitions. Live wrappers are not
            // safe to dereference until RB has republished a valid local player for the new frame.
            if (_done || CommonBehaviors.IsLoading || Core.Player == null || !Core.Player.IsValid)
            {
                return RunStatus.Failure;
            }

            // A POI keeps the wrapper that existed when it was assigned. Reacquire it once from
            // the current object table and share that wrapper across this pulse so independent
            // validators cannot observe different generations of the same transient object ID.
            BattleCharacter liveCombatPoiTarget;
            TryGetLiveCombatPoiTarget(out liveCombatPoiTarget);

            CountDeath();
            UpdateHuntStatistics(liveCombatPoiTarget);
            UpdateDefensiveCombatState();
            EnforceZoneBoundary();
            ValidateCombatRetirementDeadline();
            if (_done)
            {
                return RunStatus.Failure;
            }
            InterruptIdleHuntForEligibleFate();
            IsFateStillActive();
            ValidateTrackedFate();
            UpdateFateData();
            ValidateLiveCombatPoi(liveCombatPoiTarget);
            return RunStatus.Failure;
        }

        /// <summary>
        /// Applies every POI leash to the single current-frame wrapper captured by the safety
        /// pulse. Keeping these reads in one guarded block prevents one validator from retaining a
        /// wrapper that another validator already observed despawning during a FATE transition.
        /// </summary>
        /// <param name="target">Current-frame Kill POI actor, or <c>null</c> when none is live.</param>
        private void ValidateLiveCombatPoi(BattleCharacter target)
        {
            try
            {
                ValidatePostFateCombatLeash(target);
                ValidateDefensiveCombatLeash(target);
                ValidateCombatPoiFateLeash(target);
                ValidateCombatPoiLineOfSight(target);
            }
            catch (Exception exception)
            {
                if (exception.GetType().Name != "ReadWriteMemoryException")
                {
                    throw;
                }

                // The actor can retire after current-frame reacquisition but before the final
                // native property read. Treat only GreyMagic's precise memory fault as a despawn.
                ClearStaleCombatPoi("Combat target despawned during LLFate safety validation.");
            }
        }

        private Func<bool> GetCondition()
        {
            try
            {
                if (condition == null)
                {
                    if (!string.IsNullOrWhiteSpace(WhileCondition))
                    {
                        condition = ScriptManager.GetCondition(WhileCondition);
                    }
                }

                return condition;
            }
            catch (Exception ex)
            {
                Log.Error(ScriptManager.FormatSyntaxErrorException(ex));
                TreeRoot.Stop("Unable to compile condition for GrindTag!");
                throw;
            }
        }

        [Obsolete]
        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(
                                        new Decorator(ret => ShouldStop(),
                                                      new Action(r => OnDoneWhile())),
                                        new Decorator(ret => DateTime.Now > saveNow + TimeSpan.FromSeconds(_timeout) && currentstep == 0,
                                                      new Action(r => OnTimeout())),

                                        // A missing FATE row is debounced for one second, but no profile action may
                                        // run against the old event during that window. The safety pulse owns the
                                        // transition and will either refresh the primitive snapshot or retire it.
                                        new Decorator(ret => IsTrackedFateTransitionPending(),
                                                      new Action(r => RunStatus.Success)),

                                        // Start fighting Fate Mobs but only when we are in close range to the fate position.
                                        new Decorator(r => currentstep == 1 && FateManager.WithinFate && Core.Me.ElementalLevel > 0 && fateMaxLevel < Core.Me.ElementalLevel && !Core.Me.IsLevelSynced,
                                                      new ActionRunCoroutine(async r =>
                                                      {
                                                          Log.Information("Applying Eureka Level Sync.");

                                                          ToDoList.LevelSync();

                                                          await Coroutine.Sleep(500);

                                                          return false;
                                                      })),
                                        // State-only diagnostics do not belong in this per-pulse path: log only
                                        // the level-sync action below so ordinary FATE participation stays quiet.
                                        new Decorator(r => currentstep == 1 && FateManager.WithinFate && fateMaxLevel < Core.Player.ClassLevel && !Core.Me.IsLevelSynced,
                                                      new ActionRunCoroutine(async r =>
                                                      {
                                                          Log.Information("Applying Level Sync.");

                                                          ToDoList.LevelSync();

                                                          await Coroutine.Sleep(500);

                                                          return false;
                                                      })),
                                        new Decorator(ret => !ShouldStop() && currentstep == 1 &&
                                                             (Vector3.Distance(Core.Player.Location, Position) > GetFateArrivalDistance(fateRadius) ||
                                                              MovementManager.IsFlying),
                                                      new PrioritySelector(
                                                                           // An already-airborne character must finish through the flight path even
                                                                           // when UseFlight is disabled; handing an airborne player to Navigator
                                                                           // leaves ground movement unable to make progress or land safely.
                                                                           new Decorator(ret => MovementManager.IsFlying ||
                                                                                                (UseFlight && AetherCurrentManager.FinishedZones.Contains(WorldManager.ZoneId)),
                                                                                         new ActionRunCoroutine(obj => FlyToFateAndLand())),
                                                                           // LLFate is already bounded to its starting zone, so ground travel must
                                                                           // remain pulse-driven to recheck While instead of entering GetTo's
                                                                           // uninterruptible navigation-graph coroutine.
                                                                           new Decorator(ret => !MovementManager.IsFlying &&
                                                                                                WorldManager.ZoneId == llFateZoneId &&
                                                                                                IsUsableFate(FateManager.GetFateById(fateid), fateid, false),
                                                                                         new ActionRunCoroutine(obj => GroundMoveToFate())))),
                                        //Find fates
                                        new Decorator(r => currentstep == 1 && fateid != 0 && Poi.Current.Type != PoiType.Kill,
                                                      new ActionRunCoroutine(r => MoveToFocusedFate())),
                                        new Decorator(ret => fateid == 0 && currentstep == 0,
                                                      new Sequence(new ActionRunCoroutine(async r =>
                                                      {
                                                          await GetFates();
                                                          // Reward inventory can update while an awaited FATE scan yields.
                                                          // Recheck before committing movement to the newly selected event.
                                                          if (ShouldStop())
                                                          {
                                                              currentfate = null;
                                                              Navigator.Stop();
                                                              return;
                                                          }

                                                          if (currentfate != null)
                                                          {
                                                              GoFate();
                                                          }
                                                          else
                                                          {
                                                              GoHunting();
                                                          }
                                                      }))),
                                        new ActionAlwaysSucceed());
        }

        // End of B Tree

        private async Task<bool> FlyToFateAndLand()
        {
            var fate = FateManager.GetFateById(fateid);
            FateSnapshot selectedSnapshot;
            if (!TryReadFateSnapshot(fate, out selectedSnapshot) ||
                !IsUsableFateSnapshot(selectedSnapshot, fateid, false))
                return false;

            // FateData is a live wrapper over a reusable client slot. Cache the validated target
            // before yielding so a slot refresh cannot redirect an in-progress flight elsewhere.
            var selectedFateId = selectedSnapshot.Id;
            var selectedFateName = selectedSnapshot.Name;
            var selectedFateLocation = selectedSnapshot.Location;
            var selectedFateRadius = selectedSnapshot.Radius;

            bool AbortFlight()
            {
                var activeFate = FateManager.GetFateById(selectedFateId);
                return currentstep != 1 || fateid != selectedFateId ||
                       WorldManager.ZoneId != llFateZoneId ||
                       !IsUsableFate(activeFate, selectedFateId, false) ||
                       ShouldStop();
            }

            // Flight cannot begin while combat is still active. Anchor this wait so a retreating
            // enemy from the previous FATE cannot drag the combat routine across the zone while
            // LLFate is waiting to mount for the newly selected event.
            var combatWaitAnchor = Core.Player.Location;
            var combatWaitAnnounced = false;
            var combatWaitMovementCleared = false;
            // These fields are also consumed by PoiAction because assigning a defensive Kill POI
            // suspends this coroutine until combat releases it.
            postFateCombatLeashAnchor = combatWaitAnchor;
            combatRetirementDeadlineUtc = DateTime.UtcNow + CombatRetirementTimeout;
            while (Core.Me.InCombat && !Core.Me.IsDead)
            {
                if (ShouldStop() ||
                    currentstep != 1 || fateid != selectedFateId ||
                    WorldManager.ZoneId != llFateZoneId ||
                    !IsUsableFate(FateManager.GetFateById(selectedFateId), selectedFateId, false))
                {
                    Navigator.Stop();
                    MovementManager.MoveStop();
                    ClearCombatRetirementState();
                    return false;
                }

                if (DateTime.UtcNow >= combatRetirementDeadlineUtc)
                {
                    // Remaining in combat prevents mounting, teleporting, and safe profile
                    // handoff. Fail closed instead of holding ActionRunCoroutine forever, which
                    // also prevents the normal LLFate timeout sibling from being evaluated.
                    ReleaseOwnedControl("LLFate combat-retirement timeout");
                    _done = true;
                    var reason = $"LLFate could not leave combat within {CombatRetirementTimeout.TotalSeconds:0} seconds.";
                    Log.Error(reason);
                    TreeRoot.Stop(reason);
                    return false;
                }

                if (!combatWaitAnnounced)
                {
                    Log.Information("Waiting to leave combat before flying...");
                    combatWaitAnnounced = true;
                }

                var eligibleAttackers = GameObjectManager.Attackers
                    .Where(a => a.IsValid &&
                                a.CanAttack &&
                                a.IsTargetable &&
                                IsLineOfSightCandidate(a) &&
                                !a.IsDead &&
                                !IsUnsupportedOrUnresolvedFateActor(a) &&
                                Vector3.Distance(a.Location, combatWaitAnchor) <= PostFateCombatLeash)
                    .ToArray();
                // Keep the current defensive engagement stable while it remains eligible. Picking
                // the nearest attacker on every half-second sample allowed two moving actors to
                // trade places repeatedly and recreate the same POI churn as active FATE combat.
                var existingTarget = Poi.Current?.BattleCharacter;
                var target = existingTarget == null
                    ? null
                    : eligibleAttackers.FirstOrDefault(a => a.ObjectId == existingTarget.ObjectId);
                target = target ?? eligibleAttackers.OrderBy(a => a.Distance()).FirstOrDefault();
                if (target != null)
                {
                    if (Poi.Current?.Type != PoiType.Kill || existingTarget?.ObjectId != target.ObjectId)
                    {
                        Poi.Current = new Poi(target, PoiType.Kill);
                    }

                    combatWaitMovementCleared = false;
                }
                else if (!combatWaitMovementCleared)
                {
                    // POI and current target are separate combat inputs in RebornBuddy. Clear both,
                    // then release every ground-movement owner so the routine cannot keep chasing a
                    // target that has crossed the post-FATE leash.
                    Poi.Clear("No nearby attacker during post-FATE combat wait");
                    Core.Player.ClearTarget();
                    Navigator.Stop();
                    MovementManager.MoveStop();
                    combatWaitMovementCleared = true;
                }

                await Coroutine.Sleep(500);
            }

            ClearCombatRetirementState();

            if (combatWaitAnnounced)
            {
                Poi.Clear("Post-FATE combat wait completed");
                Navigator.Stop();
                MovementManager.MoveStop();

                // Chain FATEs commonly appear at the completed event's location while combat is
                // still retiring. Re-evaluate exactly once at this safe handoff, before flight
                // owns movement, so a newly spawned local event can replace a stale distant pick
                // without allowing destination thrashing during travel.
                if (TryAdoptCloserFateAfterCombatWait(
                                                         selectedFateId,
                                                         selectedFateName,
                                                         selectedFateLocation,
                                                         selectedFateRadius))
                {
                    return false;
                }
            }

            // Fly and land are evaluated separately because FlyToAndLandAsync returns false for
            // cancellation, route failure, and landing failure alike. Unknown terrain results and
            // failed positive probes receive a small bounded retry budget before alternate terrain
            // is used; otherwise the tag can remain airborne until the entire FATE expires.
            if (rejectedCenterLandingFateId != selectedFateId)
            {
                var centerFlightResult = await CommonTasks.FlyToAsync(
                                                                          selectedFateLocation.Add(0f, FateLandingHeight, 0f),
                                                                          abortCondition: AbortFlight,
                                                                          destinationName: $"FlyToFate:{selectedFateName}");
                if (centerFlightResult != MoveResult.ReachedDestination || AbortFlight())
                {
                    Flightor.Clear();
                    return false;
                }

                if (!MovementManager.IsFlying)
                {
                    // Successful arrival completes this event generation's landing attempt. The
                    // same FATE ID may spawn again later and must receive a fresh retry budget.
                    ClearLandingRecoveryState();
                    return true;
                }

                var centerLandingAssessment = CommonTasks.CanLand();
                if (centerLandingAssessment == CanLandResult.Yes)
                {
                    var landed = await CommonTasks.Land() && !MovementManager.IsFlying;
                    if (landed)
                    {
                        ClearLandingRecoveryState();
                        return true;
                    }

                    centerLandingAssessmentFailures++;
                    if (centerLandingAssessmentFailures < MaximumCenterLandingAssessments)
                    {
                        return false;
                    }
                }
                else if (centerLandingAssessment != CanLandResult.No)
                {
                    centerLandingAssessmentFailures++;
                    if (centerLandingAssessmentFailures < MaximumCenterLandingAssessments)
                    {
                        return false;
                    }
                }

                rejectedCenterLandingFateId = selectedFateId;
                Log.Information($"The center of FATE \"{selectedFateName}\" did not produce a safe landing; probing nearby FATE terrain.");
            }

            var scanDeadline = DateTime.UtcNow.AddMilliseconds(FateEnemyLandingScanMilliseconds);
            do
            {
                if (AbortFlight())
                {
                    return false;
                }

                var landingPoint = FindFateLandingPoint(
                                                                selectedFateId,
                                                                selectedFateLocation,
                                                                selectedFateRadius);
                if (landingPoint.HasValue)
                {
                    Log.Information($"Using a validated alternate position to land for FATE \"{selectedFateName}\".");
                    var enemyResult = await CommonTasks.FlyToAndLandAsync(
                                                                               landingPoint.Value,
                                                                               abortCondition: AbortFlight,
                                                                               destinationName: $"FateEnemyLanding:{selectedFateName}");
                    if (enemyResult)
                    {
                        ClearLandingRecoveryState();
                        return true;
                    }

                    if (AbortFlight())
                    {
                        return false;
                    }

                    landingPointScanFailures++;
                    if (landingPointScanFailures >= MaximumLandingPointScans)
                    {
                        RejectFateAfterLandingFailure(selectedFateId, selectedFateName);
                    }

                    return false;
                }

                await Coroutine.Sleep(250);
            }
            while (DateTime.UtcNow < scanDeadline);

            if (AbortFlight())
            {
                return false;
            }

            landingPointScanFailures++;
            if (landingPointScanFailures >= MaximumLandingPointScans)
            {
                RejectFateAfterLandingFailure(selectedFateId, selectedFateName);
            }

            return false;
        }

        /// <summary>
        /// Finds an airborne landing point at or near live enemies, then falls back to a small ring
        /// around the event center. Enemy locations are snapshotted before any coroutine yields,
        /// and RebornBuddy's native landing query rejects water, props, and other unsafe geometry.
        /// </summary>
        /// <param name="selectedFateId">Stable ID of the FATE that owns the recovery attempt.</param>
        /// <param name="selectedFateLocation">Snapshotted event center used to enforce its radius.</param>
        /// <param name="selectedFateRadius">Snapshotted event radius.</param>
        /// <returns>A validated flight position, or <c>null</c> until a suitable enemy is loaded.</returns>
        private static Vector3? FindFateLandingPoint(
                                                       uint selectedFateId,
                                                       Vector3 selectedFateLocation,
                                                       float selectedFateRadius)
        {
            var anchors = GameObjectManager.GetObjectsOfType<BattleCharacter>()
                .Where(enemy => enemy.IsValid &&
                                enemy.IsFate &&
                                enemy.FateId == selectedFateId &&
                                enemy.CanAttack &&
                                enemy.IsTargetable &&
                                enemy.IsVisible &&
                                !enemy.IsDead)
                .Select(enemy => enemy.Location)
                .Where(location => location != Vector3.Zero &&
                                   Vector3.Distance(location, selectedFateLocation) <= selectedFateRadius)
                .OrderBy(location => Vector3.Distance(location, Core.Player.Location))
                // Limit native terrain probes per pulse. Moving enemies are re-snapshotted on the
                // next bounded scan, so examining every actor adds cost without improving safety.
                .Take(4)
                .Concat(new[] { selectedFateLocation })
                .ToArray();

            foreach (var anchor in anchors)
            {
                foreach (var offset in LandingProbeOffsets)
                {
                    var groundLocation = anchor.Add(offset.X, offset.Y, offset.Z);
                    if (Vector3.Distance(groundLocation, selectedFateLocation) > selectedFateRadius)
                    {
                        continue;
                    }

                    var flightLocation = groundLocation.Add(0f, FateLandingHeight, 0f);
                    if (CommonTasks.CanLand(flightLocation) == CanLandResult.Yes)
                    {
                        return flightLocation;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Starts a fresh bounded landing attempt when LLFate adopts a different event generation.
        /// The rejected-center marker belongs to the same attempt as the counters and must be reset
        /// with them; otherwise a recurring FATE can inherit an old terrain verdict after cooldown.
        /// </summary>
        private void ResetLandingRecoveryState(uint selectedFateId)
        {
            if (landingRecoveryFateId == selectedFateId)
            {
                return;
            }

            landingRecoveryFateId = selectedFateId;
            rejectedCenterLandingFateId = 0;
            centerLandingAssessmentFailures = 0;
            landingPointScanFailures = 0;
        }

        /// <summary>
        /// Invalidates the exhausted landing attempt after applying its cooldown. Setting the owner
        /// ID back to zero ensures a later spawn of the same FATE receives the complete center and
        /// alternate-point retry budgets instead of inheriting the terminal counters.
        /// </summary>
        private void ClearLandingRecoveryState()
        {
            landingRecoveryFateId = 0;
            rejectedCenterLandingFateId = 0;
            centerLandingAssessmentFailures = 0;
            landingPointScanFailures = 0;
        }

        /// <summary>
        /// Retires an event that exhausted every safe landing path and excludes it for this tag run.
        /// Remaining airborne is safer than descending blindly over water or invalid terrain; a
        /// later eligible FATE can still be reached normally through Flightor.
        /// </summary>
        private void RejectFateAfterLandingFailure(uint selectedFateId, string selectedFateName)
        {
            if (currentstep != 1 || fateid != selectedFateId)
            {
                return;
            }

            landingFailureCooldowns[selectedFateId] = DateTime.UtcNow + LandingFailureCooldown;
            ClearLandingRecoveryState();
            RetireTrackedFateState($"FATE {selectedFateId} exhausted safe landing recovery");
            Log.Warning($"Skipping FATE \"{selectedFateName}\" ({selectedFateId}) for {LandingFailureCooldown.TotalMinutes:0} minutes because no safe landing point was found after {MaximumLandingPointScans} bounded scans. A later spawn will be retried.");
        }

        /// <summary>
        /// Moves toward the selected FATE without monopolizing the behavior tree for the entire
        /// route. The profile's While condition is evaluated after every yield so an inventory
        /// reward immediately cancels travel instead of being noticed only at the destination.
        /// </summary>
        /// <returns><c>true</c> when the FATE perimeter is reached; otherwise <c>false</c>.</returns>
        private async Task<bool> GroundMoveToFate()
        {
            var selectedFateId = fateid;
            while (!ShouldStop() && currentstep == 1 && fateid == selectedFateId &&
                   WorldManager.ZoneId == llFateZoneId && !Core.Me.IsDead)
            {
                var fate = FateManager.GetFateById(selectedFateId);
                FateSnapshot snapshot;
                if (!TryReadFateSnapshot(fate, out snapshot) ||
                    !IsUsableFateSnapshot(snapshot, selectedFateId, false))
                {
                    break;
                }

                Position = snapshot.Location;
                fateRadius = snapshot.Radius;
                if (Vector3.Distance(Core.Player.Location, Position) <= GetFateArrivalDistance(fateRadius))
                {
                    Navigator.Stop();
                    return true;
                }

                // Preserve GetTo's practical mount behavior while retaining control of the loop.
                // Summoning can yield, so While is checked again before issuing movement.
                if (!Core.Me.IsMounted && !Core.Me.InCombat &&
                    Core.Me.Distance(Position) > CharacterSettings.Instance.MountDistance)
                {
                    await CommonTasks.SummonFlyingMount();
                    if (ShouldStop() || currentstep != 1 || fateid != selectedFateId)
                    {
                        break;
                    }
                }

                Navigator.MoveTo(Position);
                await Coroutine.Yield();
            }

            Navigator.Stop();
            return false;
        }

        private async Task<bool> FlyTo(Vector3 destination, bool land = false, bool dismount = false, bool ignoreIndoors = true, float minHeight = 0f)
        {
            if (destination == Vector3.Zero)
            {
                return false;
            }

            if (Core.Me.InCombat)
            {
                return false;
            }

            while (!Core.Me.IsDead)
            {
                if (InPosition(destination))
                {
                    await StopMovement();
                    break;
                }

                if (CommonBehaviors.IsLoading)
                {
                    await CommonTasks.HandleLoading();
                    break;
                }

                if (!Core.Me.IsMounted && Core.Me.Location.Distance(destination) > CharacterSettings.Instance.MountDistance)
                {
                    await CommonTasks.SummonFlyingMount();
                    await Coroutine.Sleep(500);
                }

                var parameters = new FlyToParameters(destination) { CheckIndoors = !ignoreIndoors };
                if (MovementManager.IsDiving)
                {
                    parameters.CheckIndoors = false;
                }

                if (minHeight > 0)
                {
                    parameters.MinHeight = minHeight;
                }

                Flightor.MoveTo(parameters);
                await Coroutine.Yield();
            }

            if (!MovementManager.IsDiving && MovementManager.IsFlying && land)
            {
                await Land();
                await Coroutine.Sleep(500);
            }

            if (Core.Me.IsMounted && dismount)
            {
                await Dismount();
                await Coroutine.Sleep(500);
            }

            Flightor.Clear();

            return true;
        }

        private async Task<bool> StopMovement()
        {
            if (!MovementManager.IsFlying)
            {
                if (!MovementManager.IsMoving)
                {
                    return true;
                }

                var ticks = 0;
                while (MovementManager.IsMoving && ticks < 100)
                {
                    MovementManager.MoveStop();
                    await Coroutine.Sleep(100);
                    ticks++;
                }

                if (ticks >= 100)
                {
                    Log.Verbose("Timeout whilst trying to stop movement.");
                }

                return true;
            }
            else
            {
                MovementManager.MoveStop();
                await Coroutine.Sleep(100);
                return true;
            }
        }

        private async Task<bool> Land()
        {
            if (!MovementManager.IsFlying || MovementManager.IsSwimming)
            {
                return true;
            }

            var ticks = 0;
            if (CommonTasks.CanLand() == CanLandResult.Yes)
            {
                while (ticks < 100 && await CommonTasks.Land())
                {
                    if (!MovementManager.IsFlying)
                    {
                        break;
                    }

                    await Coroutine.Sleep(100);
                    ticks++;
                }

                if (ticks >= 100)
                {
                    Log.Verbose("Timeout whilst trying to land.");
                }
            }
            else
            {
                var closestObject = GameObjectManager.GameObjects.OrderBy(r => r.DistanceSqr(Core.Me.Location)).FirstOrDefault();
                if (await CommonTasks.DescendTo(closestObject.Y) == DescendToResult.Success)
                {
                    MovementManager.StopDescending();
                    Log.Verbose("Manual descend complete.");
                }
            }

            return true;
        }

        private async Task<bool> Dismount()
        {
            if (!Core.Me.IsMounted)
            {
                return true;
            }

            var ticks = 0;
            while (Core.Me.IsMounted && ticks < 100)
            {
                ActionManager.Dismount();
                await Coroutine.Sleep(100);
                ticks++;
            }

            if (ticks >= 100)
            {
                Log.Verbose("Timeout whilst trying to dismount.");
            }

            return true;
        }

        private static bool InPosition(Vector3 location)
        {
            if (Core.Me.Location.Distance2DSqr(location) > 5.0f * 5.0f)
            {
                return false;
            }

            var yTolerance = Math.Max(3.5f, 5.0f);
            return Math.Abs(location.Y - Core.Me.Location.Y) < yTolerance;
        }

        [Obsolete]
        private async Task MoveToFocusedFate()
        {
            Vector3 currentMove;
            if (fateIcon == FateIconType.ProtectNPC || fateIcon == FateIconType.ProtectNPC2)
            {
                if (ClusterTimer.ElapsedMilliseconds > 5000)
                {
                    Log.Information("Moving using cluster logic.");

                    var x = 0.0f;
                    var y = 0.0f;
                    var z = 0.0f;
                    var total = 0.0f;
                    GameObjectManager.GetObjectsOfType<BattleCharacter>()
                        .Where(bc =>
                                   ((bc.IsFate && bc.FateId == fateid && !bc.CanAttack) || bc.Type == GameObjectType.Pc) &&
                                   bc.Location.Distance(Position) < fateRadius)
                        .ForEach(bc =>
                        {
                            total++;
                            x += bc.Location.X;
                            y += bc.Location.Y;
                            z += bc.Location.Z;
                        });
                    if (total > 0)
                    {
                        currentMove = new Vector3(x / total, y / total, z / total);
                        Navigator.MoveTo(currentMove);
                    }

                    ClusterTimer.Restart();
                }
            }
            else
            {
                if (!TrySetFateCombatPoi())
                {
                    await ApproachFateTargetForLineOfSight();
                }
            }
        }

        /// <summary>
        /// Approaches one selected-FATE enemy without creating a Kill POI until a combat ray is
        /// available. This lets Navigator handle legitimate corners and structures while keeping
        /// the combat routine from spamming actions at an obstructed target. Attempts are bounded;
        /// a target that remains hidden is temporarily suppressed so another actor can be tried.
        /// </summary>
        private async Task ApproachFateTargetForLineOfSight()
        {
            var target = GetFateApproachTarget();
            if (target == null)
            {
                return;
            }

            var targetObjectId = target.ObjectId;
            var selectedFateId = fateid;
            var deadline = DateTime.UtcNow + LineOfSightApproachTimeout;
            while (DateTime.UtcNow < deadline && !ShouldStop() && !Core.Me.IsDead)
            {
                var liveTarget = GameObjectManager.GetObjectByObjectId(targetObjectId) as BattleCharacter;
                if (liveTarget == null || !liveTarget.IsValid || liveTarget.IsDead ||
                    liveTarget.FateId != selectedFateId ||
                    !IsInsideTrackedFateTargetBoundary(liveTarget) ||
                    !IsUsableFate(FateManager.GetFateById(selectedFateId), selectedFateId, false))
                {
                    break;
                }

                if (IsLineOfSightCandidate(liveTarget))
                {
                    Navigator.Stop();
                    MovementManager.MoveStop();
                    Poi.Current = new Poi(liveTarget, PoiType.Kill);
                    return;
                }

                var moveResult = Navigator.MoveTo(new MoveToParameters(liveTarget.Location, $"Approaching {liveTarget.Name} for line of sight")
                {
                    DistanceTolerance = 5f,
                    UseMount = false
                });
                if (moveResult == MoveResult.Failed)
                {
                    break;
                }

                await Coroutine.Yield();
            }

            Navigator.Stop();
            MovementManager.MoveStop();
            var failedTarget = GameObjectManager.GetObjectByObjectId(targetObjectId) as BattleCharacter;
            if (failedTarget != null && failedTarget.IsValid && !failedTarget.IsDead &&
                failedTarget.FateId == selectedFateId &&
                IsInsideTrackedFateTargetBoundary(failedTarget) && !failedTarget.InLineOfSight())
            {
                SuppressObstructedTarget(failedTarget, "bounded LOS approach failed");
            }
        }

        private void GoFate()
        {
            // Selection and movement occur on different pulses. Never arm a destination after a
            // delayed reward has already satisfied the profile's completion condition.
            if (ShouldStop())
            {
                currentfate = null;
                Navigator.Stop();
                return;
            }

            if (currentfate != null)
            {
                FateSnapshot snapshot;
                if (!TryReadFateSnapshot(currentfate, out snapshot) ||
                    !IsUsableFateSnapshot(snapshot, snapshot.Id, false))
                {
                    currentfate = null;
                    return;
                }

                participatedInTrackedFate = false;
                var idleHuntTarget = Poi.Current?.BattleCharacter;
                if (huntedTargetObjectId != 0 && idleHuntTarget?.ObjectId == huntedTargetObjectId)
                {
                    // A newly eligible FATE always outranks optional downtime hunting.
                    Poi.Clear("Stopping idle hunt for an eligible FATE.");
                    huntedTargetObjectId = 0;
                }
                idleHuntAnchor = null;

                // Copy every value needed after this pulse. Holding the live wrapper across a FATE
                // retirement previously caused deterministic ReadWriteMemoryException failures
                // when Panda's post-FATE cleanup yielded control back to OrderBot.
                Position = snapshot.Location;
                fateRadius = snapshot.Radius;
                fateid = snapshot.Id;
                fateName = snapshot.Name;
                fateIcon = snapshot.Icon;
                fateMaxLevel = snapshot.MaxLevel;
                Log.Information($"Fate Details: Name:{fateName} Id:{fateid} Icon:{fateIcon} Location:{Position} Radius:{fateRadius} MaxLevel:{fateMaxLevel}");
                currentstep = 1;
                // A newly selected ID starts a fresh snapshot-observation window. Carrying a timer
                // from the prior event could otherwise make the first incomplete pulse terminal.
                trackedFateUnavailableSinceUtc = default(DateTime);
                trackedFateInvalidSinceUtc = default(DateTime);
                ResetLandingRecoveryState(fateid);
                currentfate = null;
            }
        }

        /// <summary>
        /// Reconsiders the selected destination after lingering combat has ended. A candidate
        /// already containing the player has priority; otherwise the normal nearest-event ordering
        /// applies. This method is called only once before travel begins, so ordinary FATE spawns
        /// cannot redirect an active flight.
        /// </summary>
        /// <param name="selectedFateId">ID selected before the combat wait.</param>
        /// <param name="selectedFateName">Stable name selected before the combat wait.</param>
        /// <param name="selectedFateLocation">Stable center selected before the combat wait.</param>
        /// <param name="selectedFateRadius">Stable radius selected before the combat wait.</param>
        /// <returns><c>true</c> when LLFate adopted a newly eligible, closer event.</returns>
        private bool TryAdoptCloserFateAfterCombatWait(
                                                            uint selectedFateId,
                                                            string selectedFateName,
                                                            Vector3 selectedFateLocation,
                                                            float selectedFateRadius)
        {
            var refreshedFate = SelectBestEligibleFate(logCandidates: false);
            FateSnapshot refreshedSnapshot;
            if (!TryReadFateSnapshot(refreshedFate, out refreshedSnapshot) ||
                refreshedSnapshot.Id == selectedFateId)
            {
                return false;
            }

            var selectedDistance = Vector3.Distance(Core.Player.Location, selectedFateLocation);
            var refreshedDistance = Core.Me.Distance(refreshedSnapshot.Location);
            var playerInsideSelected = selectedDistance <= selectedFateRadius;
            var playerInsideRefreshed = refreshedDistance <= refreshedSnapshot.Radius;
            if ((!playerInsideRefreshed || playerInsideSelected) && refreshedDistance >= selectedDistance)
            {
                return false;
            }

            var refreshedName = refreshedSnapshot.Name;
            Log.Information(
                $"A closer FATE appeared during the combat wait; switching from \"{selectedFateName}\" to \"{refreshedName}\".");
            Poi.Clear("Switching to a closer FATE that appeared during combat wait");
            Navigator.Stop();
            MovementManager.MoveStop();
            currentfate = refreshedFate;
            rejectedCenterLandingFateId = 0;
            GoFate();
            return currentstep == 1 && fateid == refreshedSnapshot.Id;
        }

        private void GoHunting()
        {
            if (!HuntBetweenFates || currentfate != null || Core.Me.InCombat)
            {
                return;
            }

            var existingTarget = Poi.Current?.BattleCharacter;
            if (Poi.Current?.Type == PoiType.Kill && existingTarget != null && existingTarget.IsValid && !existingTarget.IsDead)
            {
                return;
            }

            var target = GetNormalTargets();
            if (target != null)
            {
                huntedTargetObjectId = target.ObjectId;
                // HuntRadius is a leash from the point at which downtime hunting starts, not a
                // rolling player-relative selection radius that can follow a fleeing target forever.
                idleHuntAnchor = Core.Player.Location;
                Poi.Current = new Poi(target, PoiType.Kill);
                Log.Information($"No eligible FATE is active; hunting nearby {target.Name} while waiting.");
            }
        }

        private void UpdateHuntStatistics(BattleCharacter liveCombatPoiTarget)
        {
            try
            {
                if (huntedTargetObjectId == 0)
                {
                    // The anchor remains active while combat winds down after an abandoned target so
                    // RB's independent targeting pulse cannot replace it with a distant attacker.
                    if (idleHuntAnchor.HasValue && !Core.Me.InCombat)
                    {
                        idleHuntAnchor = null;
                    }
                    return;
                }

                // Prefer the safety pulse's current-frame POI wrapper. The fallback covers the brief
                // interval after RB clears a dead POI but before LLFate records the completed hunt.
                var target = liveCombatPoiTarget != null && liveCombatPoiTarget.ObjectId == huntedTargetObjectId
                    ? liveCombatPoiTarget
                    : GameObjectManager.GetObjectByObjectId(huntedTargetObjectId) as BattleCharacter;
                if (target != null && target.IsValid && !target.IsDead && !IsInsideIdleHuntLeash(target))
                {
                    var abandonedTargetId = huntedTargetObjectId;
                    abandonedIdleHuntTargetsUntil[abandonedTargetId] = DateTime.UtcNow + IdleHuntAbandonmentPeriod;
                    if (liveCombatPoiTarget != null && liveCombatPoiTarget.ObjectId == abandonedTargetId)
                    {
                        Poi.Clear("Idle hunt target exceeded HuntRadius.");
                    }
                    if (Core.Player.CurrentTargetId == abandonedTargetId)
                    {
                        Core.Player.ClearTarget();
                    }
                    Navigator.Stop();
                    MovementManager.MoveStop();
                    Log.Information($"Abandoned idle hunt target {target.Name}; it moved beyond the {HuntRadius:0.#}-yalm HuntRadius leash.");
                    huntedTargetObjectId = 0;
                    if (!Core.Me.InCombat)
                    {
                        idleHuntAnchor = null;
                    }
                    return;
                }

                if (target != null && target.IsValid && target.IsDead)
                {
                    mobsHunted++;
                    Log.Information($"Completed idle hunt. Session hunt total: {mobsHunted}.");
                    huntedTargetObjectId = 0;
                    if (!Core.Me.InCombat)
                    {
                        idleHuntAnchor = null;
                    }
                }
                else if (target == null && !Core.Me.InCombat)
                {
                    // A missing object is not enough to claim a kill: it may have phased or left object range.
                    huntedTargetObjectId = 0;
                    idleHuntAnchor = null;
                }
            }
            catch (Exception exception)
            {
                if (exception.GetType().Name != "ReadWriteMemoryException")
                {
                    throw;
                }

                // Hunt actors can retire after the pulse reacquires the POI but before statistics
                // finish reading it. Clear only the stale control surfaces; the next pulse decides
                // whether combat is still active before retiring the hunt anchor and target ID.
                ClearStaleCombatPoi("Idle hunt target despawned during LLFate statistics validation.");
            }
        }

        private bool IsInsideIdleHuntLeash(BattleCharacter unit)
        {
            return !idleHuntAnchor.HasValue ||
                   Vector3.Distance(unit.Location, idleHuntAnchor.Value) <= HuntRadius;
        }

        /// <summary>
        /// Clears the incidental-aggro anchor whenever another explicit LLFate objective owns
        /// targeting, or after combat ends. The provider can establish the anchor first because
        /// CombatTargeting may pulse before the OrderBot safety hook in a scheduling pass.
        /// </summary>
        private void UpdateDefensiveCombatState()
        {
            if (!Core.Me.InCombat || fateid != 0 || idleHuntAnchor.HasValue || postFateCombatLeashAnchor.HasValue)
            {
                defensiveCombatAnchor = null;
            }
        }

        /// <summary>
        /// Lets a newly eligible FATE preempt downtime hunting from the always-pulsed safety hook.
        /// A Kill POI prevents normal profile selection from running, so waiting for GetFates would
        /// incorrectly make the optional hunt more important than the tag's primary FATE work.
        /// </summary>
        private void InterruptIdleHuntForEligibleFate()
        {
            if (huntedTargetObjectId == 0 || currentfate != null || ShouldStop())
            {
                return;
            }

            var eligibleFate = SelectBestEligibleFate(logCandidates: false);
            FateSnapshot eligibleSnapshot;
            if (!TryReadFateSnapshot(eligibleFate, out eligibleSnapshot) ||
                !IsUsableFateSnapshot(eligibleSnapshot, 0, false))
            {
                return;
            }

            // Hand any unavoidable combat to the existing bounded retirement path before GoFate
            // clears the hunt anchor. This prevents provider admission from becoming unbounded in
            // the scheduling pass that switches objectives.
            if (Core.Me.InCombat)
            {
                postFateCombatLeashAnchor = Core.Player.Location;
                combatRetirementDeadlineUtc = DateTime.UtcNow + CombatRetirementTimeout;
            }

            // Selection returns a live native wrapper. Use the validated primitive snapshot for
            // diagnostics so a row retiring at this handoff cannot fault the always-pulsed hook.
            Log.Information($"Eligible FATE \"{eligibleSnapshot.Name}\" appeared; interrupting optional idle hunting.");
            currentfate = eligibleFate;
            GoFate();
        }

        /// <summary>
        /// Enforces the combat-retirement deadline from an always-running hook. A defensive Kill
        /// POI prevents the profile coroutine that created it from resuming, so checking only
        /// inside that coroutine cannot guarantee a bound.
        /// </summary>
        private void ValidateCombatRetirementDeadline()
        {
            if (combatRetirementDeadlineUtc == default(DateTime))
            {
                return;
            }

            if (!Core.Me.InCombat)
            {
                ClearCombatRetirementState();
                return;
            }

            if (DateTime.UtcNow < combatRetirementDeadlineUtc)
            {
                return;
            }

            ReleaseOwnedControl("LLFate combat-retirement safety timeout");
            _done = true;
            combatRetirementDeadlineUtc = default(DateTime);
            var reason = $"LLFate could not leave combat within {CombatRetirementTimeout.TotalSeconds:0} seconds.";
            Log.Error(reason);
            TreeRoot.Stop(reason);
        }

        /// <summary>Clears the temporary leash and deadline after combat safely retires.</summary>
        private void ClearCombatRetirementState()
        {
            postFateCombatLeashAnchor = null;
            combatRetirementDeadlineUtc = default(DateTime);
        }

        /// <summary>
        /// Clears an existing Kill POI after its actor crosses the post-FATE leash. Provider
        /// admission prevents new reacquisition, but RebornBuddy continues executing an already
        /// assigned POI until its owner explicitly retires it.
        /// </summary>
        private void ValidatePostFateCombatLeash(BattleCharacter target)
        {
            if (!postFateCombatLeashAnchor.HasValue || !Core.Me.InCombat)
            {
                return;
            }

            if (target == null ||
                Vector3.Distance(target.Location, postFateCombatLeashAnchor.Value) <= PostFateCombatLeash)
            {
                return;
            }

            var targetId = target.ObjectId;
            Poi.Clear($"Post-FATE target exceeded the {PostFateCombatLeash:0.#}-yalm leash");
            if (Core.Player != null && Core.Player.CurrentTargetId == targetId)
            {
                Core.Player.ClearTarget();
            }
            Navigator.Stop();
            MovementManager.MoveStop();
            Log.Information($"Released retreating target \"{target.Name}\" after it crossed the post-FATE combat leash.");
        }

        /// <summary>
        /// Releases an already assigned incidental-aggro POI after it crosses the same fixed area
        /// enforced during provider admission. Provider filtering prevents reacquisition, while
        /// this check retires a POI that RB assigned before the actor moved out of bounds.
        /// </summary>
        private void ValidateDefensiveCombatLeash(BattleCharacter target)
        {
            if (!defensiveCombatAnchor.HasValue || !Core.Me.InCombat || fateid != 0 ||
                idleHuntAnchor.HasValue || postFateCombatLeashAnchor.HasValue)
            {
                return;
            }

            if (target == null || target.IsFate ||
                Vector3.Distance(target.Location, defensiveCombatAnchor.Value) <= DefensiveCombatLeash)
            {
                return;
            }

            var targetId = target.ObjectId;
            Poi.Clear($"Defensive target exceeded the {DefensiveCombatLeash:0.#}-yalm leash");
            if (Core.Player.CurrentTargetId == targetId)
            {
                Core.Player.ClearTarget();
            }

            Navigator.Stop();
            MovementManager.MoveStop();
            Log.Information($"Released incidental attacker \"{target.Name}\" after it crossed the defensive-combat leash.");
        }

        /// <summary>
        /// Retires any active-FATE combat POI that leaves the event's actual boundary. Ordinary
        /// overworld attackers can aggro during an event and are just as capable of dragging the
        /// routine away, so both FATE and defensive targets share the same geometric leash.
        /// </summary>
        private void ValidateCombatPoiFateLeash(BattleCharacter target)
        {
            if (target == null ||
                postFateCombatLeashAnchor.HasValue || fateid == 0)
            {
                return;
            }

            var isInsideBoundary = target.IsFate
                ? IsInsideTrackedFateTargetBoundary(target)
                : IsInsideTrackedFateGeometry(target);
            if (isInsideBoundary)
            {
                return;
            }

            var targetId = target.ObjectId;
            Poi.Clear($"Active-FATE target exceeded the event-radius leash: {target.Name}");
            if (Core.Player != null && Core.Player.CurrentTargetId == targetId)
            {
                Core.Player.ClearTarget();
            }

            Navigator.Stop();
            MovementManager.MoveStop();
            Log.Information($"Released combat target \"{target.Name}\" after it moved outside the active FATE boundary.");
        }

        /// <summary>
        /// Retires an LLFate-owned kill objective that remains obstructed. Combat routines continue
        /// acting on an existing POI even after the targeting provider stops returning that unit,
        /// so provider filtering alone cannot recover from an enemy behind permanent geometry.
        /// </summary>
        private void ValidateCombatPoiLineOfSight(BattleCharacter target)
        {
            if (target == null)
            {
                obstructedPoiObjectId = 0;
                obstructedPoiSinceUtc = default(DateTime);
                return;
            }

            try
            {
                var targetObjectId = target.ObjectId;
                var isFateTarget = target.IsFate;
                var isAttacker = GameObjectManager.Attackers.Any(
                    attacker => attacker.ObjectId == targetObjectId);
                var isRetiringAttacker = postFateCombatLeashAnchor.HasValue && Core.Me.InCombat && isAttacker;
                var isActiveFateAttacker = fateid != 0 && !isFateTarget && isAttacker;
                var isDefensiveAttacker = defensiveCombatAnchor.HasValue && !isFateTarget && isAttacker;
                var isOwnedTarget = (fateid != 0 && isFateTarget && target.FateId == fateid) ||
                                    (huntedTargetObjectId != 0 && targetObjectId == huntedTargetObjectId) ||
                                    isActiveFateAttacker ||
                                    isDefensiveAttacker ||
                                    isRetiringAttacker;
                if (!isOwnedTarget || target.IsDead || target.InLineOfSight())
                {
                    obstructedPoiObjectId = 0;
                    obstructedPoiSinceUtc = default(DateTime);
                    return;
                }

                var now = DateTime.UtcNow;
                if (obstructedPoiObjectId != targetObjectId)
                {
                    obstructedPoiObjectId = targetObjectId;
                    obstructedPoiSinceUtc = now;
                    return;
                }

                if (now - obstructedPoiSinceUtc < LineOfSightGracePeriod)
                {
                    return;
                }

                SuppressObstructedTarget(target, "target remained out of line of sight");
                obstructedPoiObjectId = 0;
                obstructedPoiSinceUtc = default(DateTime);
            }
            catch (Exception exception)
            {
                if (exception.GetType().Name != "ReadWriteMemoryException")
                {
                    throw;
                }

                // The object table can retire a FATE actor between reacquisition and the remaining
                // native reads in this pulse. GreyMagic exposes this exception as a private global
                // type in current RB builds, so it must be identified by its exact runtime name;
                // every other exception is rethrown instead of being hidden by this recovery path.
                ClearStaleCombatPoi("Combat target despawned during LLFate safety validation.");
            }
        }

        /// <summary>
        /// Reacquires a combat POI from the current game-object table before reading combat fields.
        /// POI retains the wrapper that existed when it was assigned, but RB invalidates wrappers
        /// when an actor despawns or its object-table slot is reused at a FATE transition.
        /// </summary>
        /// <param name="target">The current-frame battle-character wrapper when available.</param>
        /// <returns><c>true</c> only when the Kill POI still resolves to a valid live actor.</returns>
        private bool TryGetLiveCombatPoiTarget(out BattleCharacter target)
        {
            target = null;
            if (Poi.Current?.Type != PoiType.Kill)
            {
                return false;
            }

            try
            {
                var cachedTarget = Poi.Current.BattleCharacter;
                if (cachedTarget == null)
                {
                    ClearStaleCombatPoi("LLFate Kill POI no longer has a combat actor.");
                    return false;
                }

                var objectId = cachedTarget.ObjectId;
                target = GameObjectManager.GetObjectByObjectId(objectId) as BattleCharacter;
                if (target != null && target.IsValid)
                {
                    return true;
                }

                ClearStaleCombatPoi("LLFate Kill POI actor is no longer present in the current object table.");
                target = null;
                return false;
            }
            catch (Exception exception)
            {
                if (exception.GetType().Name != "ReadWriteMemoryException")
                {
                    throw;
                }

                // ReadWriteMemoryException is private in GreyMagic.dll and therefore cannot be
                // referenced in source. Matching its exact runtime name keeps this guard narrow.
                ClearStaleCombatPoi("LLFate Kill POI wrapper expired before it could be reacquired.");
                target = null;
                return false;
            }
        }

        /// <summary>
        /// Releases movement and targeting retained for an actor that no longer exists. This is
        /// intentionally quiet beyond the standard POI-clear diagnostic because despawns are a
        /// normal FATE transition and should not create recurring warning or error noise.
        /// </summary>
        /// <param name="reason">Diagnostic reason recorded by RebornBuddy's POI manager.</param>
        private void ClearStaleCombatPoi(string reason)
        {
            Poi.Clear(reason);
            Core.Player.ClearTarget();
            Navigator.Stop();
            MovementManager.MoveStop();
            obstructedPoiObjectId = 0;
            obstructedPoiSinceUtc = default(DateTime);
        }

        /// <summary>
        /// Releases one obstructed target from every LLFate-owned control surface and delays its
        /// eligibility. Both the active-POI validator and the pre-combat approach path use this
        /// shared cleanup so neither can immediately reacquire the same unreachable actor.
        /// </summary>
        /// <param name="target">Live actor that failed the LOS policy.</param>
        /// <param name="reason">Concise diagnostic explanation for the suppression.</param>
        private void SuppressObstructedTarget(BattleCharacter target, string reason)
        {
            lineOfSightSuppressedUntil[target.ObjectId] = DateTime.UtcNow + LineOfSightSuppressionPeriod;
            if (Poi.Current?.Type == PoiType.Kill && Poi.Current.BattleCharacter?.ObjectId == target.ObjectId)
            {
                Poi.Clear($"LLFate {reason}: {target.Name}");
            }
            if (Core.Player != null && Core.Player.CurrentTargetId == target.ObjectId)
            {
                Core.Player.ClearTarget();
            }

            Navigator.Stop();
            MovementManager.MoveStop();
            Log.Information(
                $"Skipping obstructed target \"{target.Name}\" for {LineOfSightSuppressionPeriod.TotalSeconds:0} seconds ({reason}).");
        }

        /// <summary>
        /// Determines whether an actor has a usable combat ray and is not cooling down after a
        /// persistent obstruction. Expired entries are removed lazily because object IDs are
        /// short-lived and the list remains bounded to targets encountered by this tag run.
        /// </summary>
        /// <param name="unit">Candidate combat actor.</param>
        /// <returns><c>true</c> when LLFate may select or admit the actor.</returns>
        private bool IsLineOfSightCandidate(BattleCharacter unit)
        {
            return unit != null && unit.InLineOfSight() && !IsLineOfSightSuppressed(unit.ObjectId);
        }

        /// <summary>Checks the temporary obstruction cache and lazily removes expired entries.</summary>
        private bool IsLineOfSightSuppressed(uint objectId)
        {
            DateTime suppressedUntil;
            if (!lineOfSightSuppressedUntil.TryGetValue(objectId, out suppressedUntil))
            {
                return false;
            }

            if (DateTime.UtcNow < suppressedUntil)
            {
                return true;
            }

            lineOfSightSuppressedUntil.Remove(objectId);
            return false;
        }

        /// <summary>
        /// Supplies the targeting provider with the same live safety bounds used by LLFate's
        /// behavior tree. RebornBuddy pulses targeting independently during scheduling, so this
        /// admission check must enforce leashes before a unit can be promoted back into a kill POI.
        /// </summary>
        /// <param name="unit">The unit CombatTargeting is considering.</param>
        /// <returns><c>true</c> when the unit remains inside every active leash.</returns>
        private bool IsInsideActiveTargetingLeashes(BattleCharacter unit)
        {
            if (!IsLineOfSightCandidate(unit))
            {
                return false;
            }

            if (IsUnsupportedOrUnresolvedFateActor(unit))
            {
                return false;
            }

            // Clearing the POI and current target alone is insufficient because CombatTargeting
            // can immediately promote the same attacker again. The suppression is deliberately
            // temporary because the client may later reuse this transient object ID.
            if (!unit.IsFate && IsIdleHuntTargetAbandoned(unit.ObjectId))
            {
                return false;
            }

            if (postFateCombatLeashAnchor.HasValue)
            {
                if (!Core.Me.InCombat)
                {
                    ClearCombatRetirementState();
                }
                else if (Vector3.Distance(unit.Location, postFateCombatLeashAnchor.Value) > PostFateCombatLeash)
                {
                    return false;
                }
            }

            if (!postFateCombatLeashAnchor.HasValue && fateid != 0 &&
                (unit.IsFate
                    ? !IsInsideTrackedFateTargetBoundary(unit)
                    : !IsInsideTrackedFateGeometry(unit)))
            {
                // Outside the short defensive handoff, every combat target must remain inside the
                // selected event. FATE actors additionally require the matching FateId; ordinary
                // attackers use the same geometry so they cannot drag combat across the zone.
                return false;
            }

            if (!postFateCombatLeashAnchor.HasValue && fateid == 0 && unit.IsFate)
            {
                // An active event must be selected through GetFates before its actors are eligible.
                // This prevents overlapping or policy-rejected FATEs from bypassing selection while idle.
                return false;
            }

            if (!unit.IsFate && idleHuntAnchor.HasValue && !IsInsideIdleHuntLeash(unit))
            {
                return false;
            }

            if (!unit.IsFate && fateid == 0 && !postFateCombatLeashAnchor.HasValue &&
                !idleHuntAnchor.HasValue)
            {
                if (!Core.Me.InCombat)
                {
                    return false;
                }

                if (!defensiveCombatAnchor.HasValue)
                {
                    defensiveCombatAnchor = Core.Player.Location;
                }

                if (Vector3.Distance(unit.Location, defensiveCombatAnchor.Value) > DefensiveCombatLeash)
                {
                    return false;
                }
            }

            return huntedTargetObjectId == 0 || unit.IsFate || IsInsideIdleHuntLeash(unit);
        }

        /// <summary>
        /// Converts a FATE's reported radius into a reachable ground-travel threshold while
        /// preserving the established ten-yalm inset for ordinary events.
        /// </summary>
        /// <param name="radius">The live radius reported by the selected FATE.</param>
        /// <returns>A strictly positive distance at which travel is considered complete.</returns>
        private static float GetFateArrivalDistance(float radius)
        {
            return Math.Max(MinimumFateArrivalDistance, radius - 10f);
        }

        /// <summary>
        /// Applies the selected event's live geometry to every FATE combat path. The small tolerance
        /// absorbs actors fighting on the boundary without restoring the old unbounded chase.
        /// Cached geometry is intentionally used during brief FateData refresh gaps.
        /// </summary>
        private bool IsInsideTrackedFateTargetBoundary(BattleCharacter unit)
        {
            return unit != null && unit.IsFate && fateid != 0 && unit.FateId == fateid &&
                   IsInsideTrackedFateGeometry(unit);
        }

        /// <summary>
        /// Applies the selected event's geometric leash to actors that do not carry its FateId.
        /// Ambient enemies can join combat inside a FATE, but they must not be allowed to pull the
        /// routine farther than an event enemy could travel.
        /// </summary>
        /// <param name="unit">The live combat actor whose position should be bounded.</param>
        /// <returns><c>true</c> when the actor remains inside the selected event radius and tolerance.</returns>
        private bool IsInsideTrackedFateGeometry(BattleCharacter unit)
        {
            return unit != null && fateid != 0 && Position != Vector3.Zero && fateRadius > 0 &&
                   Vector3.Distance(unit.Location, Position) <= fateRadius + FateTargetLeashTolerance;
        }

        /// <summary>
        /// Rejects hand-in actors and unknown FATE actors whose owning event cannot be resolved.
        /// Previously validated supported IDs remain eligible only for bounded defensive cleanup;
        /// every genuinely unknown ID stays fail-closed when its wrapper is unavailable.
        /// </summary>
        private bool IsUnsupportedOrUnresolvedFateActor(BattleCharacter unit)
        {
            if (unit == null || !unit.IsFate)
            {
                return false;
            }

            if (unit.FateId == 0 || unsupportedFateIds.Contains(unit.FateId))
            {
                return true;
            }

            var owner = FateManager.GetFateById(unit.FateId);
            FateSnapshot ownerSnapshot;
            if (!TryReadFateSnapshot(owner, out ownerSnapshot))
            {
                return !supportedFateIds.Contains(unit.FateId);
            }

            // A copied row is not necessarily the row requested: the native FATE table can reuse
            // its slot during the same scheduling pass. Never let an invalid or mismatched row
            // poison the session-long support cache used during defensive combat retirement.
            if (!ownerSnapshot.IsValid || ownerSnapshot.Id != unit.FateId)
            {
                return true;
            }

            if (ownerSnapshot.Icon == FateIconType.KillHandIn)
            {
                unsupportedFateIds.Add(unit.FateId);
                supportedFateIds.Remove(unit.FateId);
                return true;
            }

            supportedFateIds.Add(unit.FateId);
            return false;
        }

        private void UpdateFateData()
        {
            foreach (var item in FateManager.ActiveFates)
            {
                FateSnapshot snapshot;
                if (TryReadFateSnapshot(item, out snapshot) && snapshot.Id == fateid)
                {
                    Position = snapshot.Location;
                    fateName = snapshot.Name;
                    fateStatus = snapshot.Status.ToString();
                    // RB commonly removes the FateData wrapper before exposing COMPLETE. Record
                    // that the player entered this exact event so its disappearance can still be
                    // included in the session report instead of silently losing the count.
                    if (FateManager.WithinFate && Core.Me.Distance(snapshot.Location) <= snapshot.Radius)
                    {
                        participatedInTrackedFate = true;
                    }
                }
            }
        }

        private void IsFateStillActive()
        {
            if (currentstep <= 0 || fateid == 0)
            {
                return;
            }

            var trackedFate = FateManager.GetFateById(fateid);
            FateSnapshot snapshot;
            var hasSnapshot = TryReadFateSnapshot(trackedFate, out snapshot);
            // GetFateById can retain a wrapper whose native table slot has already been reused.
            // Only a valid row that still carries the selected ID may prove activity or completion;
            // every other readable row follows the same debounce as a temporarily missing row.
            var hasMatchingSnapshot = hasSnapshot && snapshot.IsValid && snapshot.Id == fateid;
            if (hasMatchingSnapshot && snapshot.Status == FateStatus.ACTIVE &&
                snapshot.TimeLeft > TimeSpan.Zero)
            {
                trackedFateUnavailableSinceUtc = default(DateTime);
                return;
            }

            var completionStatusObserved = hasMatchingSnapshot && snapshot.Status == FateStatus.COMPLETE;
            if (!completionStatusObserved && !HasSnapshotTransitionSettled(ref trackedFateUnavailableSinceUtc))
            {
                // The grace period protects against one-frame table refreshes, but movement must
                // not resume toward an event whose live row has disappeared.
                Flightor.Clear();
                Navigator.Stop();
                MovementManager.MoveStop();
                return;
            }

            var endedFateId = fateid;
            var endedFateName = !string.IsNullOrWhiteSpace(fateName) ? fateName : $"FATE {endedFateId}";

            RetireTrackedFateState($"FATE {endedFateId} ended; clearing stale movement");

            if (completionStatusObserved && participatedInTrackedFate)
            {
                fatesDone++;
                Log.Information($"Completed FATE \"{endedFateName}\" ({endedFateId}; completion status observed). Confirmed session total: {fatesDone}.");
            }
            else if (completionStatusObserved)
            {
                Log.Information($"FATE \"{endedFateName}\" ({endedFateId}) completed before LLFate observed participation; it was not added to the session completion total.");
            }
            else if (participatedInTrackedFate)
            {
                participatedFateEndings++;
                Log.Information($"Observed FATE \"{endedFateName}\" ({endedFateId}) end after participation; the client did not expose a completion result. Observed-ending total: {participatedFateEndings}.");
            }
            else
            {
                Log.Information($"FATE \"{endedFateName}\" ({endedFateId}) ended before LLFate observed participation or a completion status.");
            }

            participatedInTrackedFate = false;
        }

        /// <summary>
        /// Prevents the profile tree from acting on cached event state while the safety pulse is
        /// confirming a transient missing or malformed FATE row.
        /// </summary>
        /// <returns><c>true</c> while either tracked transition grace period is active.</returns>
        private bool IsTrackedFateTransitionPending()
        {
            return currentstep > 0 && fateid != 0 &&
                   (trackedFateUnavailableSinceUtc != default(DateTime) ||
                    trackedFateInvalidSinceUtc != default(DateTime));
        }

        /// <summary>
        /// Confirms a transient FateData transition across several bot pulses. The timestamp is
        /// reset by the corresponding healthy path, allowing separate unavailable and malformed
        /// states to recover independently without losing the selected event.
        /// </summary>
        private static bool HasSnapshotTransitionSettled(ref DateTime transitionStartedUtc)
        {
            if (transitionStartedUtc == default(DateTime))
            {
                transitionStartedUtc = DateTime.UtcNow;
                return false;
            }

            return DateTime.UtcNow - transitionStartedUtc >= FateSnapshotTransitionGracePeriod;
        }

        /// <summary>
        /// Releases all navigation, targeting, and cached destination state for a selected FATE.
        /// Combat retirement is armed before provider/POI cleanup so RB cannot reacquire a distant
        /// attacker later in the same scheduling pass.
        /// </summary>
        private void RetireTrackedFateState(string reason)
        {
            defensiveCombatAnchor = null;
            if (Core.Me.InCombat)
            {
                postFateCombatLeashAnchor = Core.Player.Location;
                combatRetirementDeadlineUtc = DateTime.UtcNow + CombatRetirementTimeout;
            }
            else
            {
                ClearCombatRetirementState();
            }

            ReleaseOwnedControl(reason);
            // Fate IDs identify definitions, not individual spawns. Retiring one generation must
            // discard its terrain verdicts so a later recurrence cannot inherit exhausted retries.
            ClearLandingRecoveryState();
            Position = Vector3.Zero;
            currentstep = 0;
            fateid = 0;
            fateRadius = 0;
            fateIcon = default(FateIconType);
            fateMaxLevel = 0;
            currentfate = null;
            trackedFateUnavailableSinceUtc = default(DateTime);
            trackedFateInvalidSinceUtc = default(DateTime);
        }

        /// <summary>
        /// Stops LLFate rather than allowing a malformed destination to carry the character
        /// through a map exit. Profiles own inter-zone travel; LLFate is intentionally bounded to
        /// the zone in which its tag started.
        /// </summary>
        private void EnforceZoneBoundary()
        {
            if (zoneSafetyTriggered || CommonBehaviors.IsLoading || WorldManager.ZoneId == llFateZoneId)
            {
                return;
            }

            zoneSafetyTriggered = true;
            Navigator.Stop();
            Poi.Clear("LLFate left its starting zone");
            _done = true;
            Log.Error($"LLFate unexpectedly left zone {llFateZoneId} and entered {WorldManager.ZoneId}; stopping to prevent cross-zone navigation.");
            TreeRoot.Stop("LLFate left its configured zone unexpectedly.");
        }

        /// <summary>
        /// Re-resolves the selected FATE by ID every pulse. The client reuses FATE table slots,
        /// so retaining a live wrapper after an event ends can expose a different event's partial
        /// data even though the wrapper itself still reports <c>IsValid</c>.
        /// </summary>
        private void ValidateTrackedFate()
        {
            if (currentstep <= 0 || fateid == 0)
            {
                return;
            }

            var trackedFate = FateManager.GetFateById(fateid);
            FateSnapshot snapshot;
            var hasSnapshot = TryReadFateSnapshot(trackedFate, out snapshot);
            if (hasSnapshot && IsUsableFateSnapshot(snapshot, fateid, true))
            {
                trackedFateInvalidSinceUtc = default(DateTime);
                Position = snapshot.Location;
                fateRadius = snapshot.Radius;
                fateIcon = snapshot.Icon;
                fateMaxLevel = snapshot.MaxLevel;
                return;
            }

            // Missing and non-active wrappers are owned by IsFateStillActive, which distinguishes
            // explicit completion from a frame-cached disappearance. ACTIVE snapshots whose timer
            // reached zero use that same ending debounce because zero is a normal expiry boundary,
            // not malformed geometry. Only other sustained malformed ACTIVE data is abandoned here.
            if (!hasSnapshot || snapshot.Status != FateStatus.ACTIVE ||
                snapshot.TimeLeft <= TimeSpan.Zero ||
                !HasSnapshotTransitionSettled(ref trackedFateInvalidSinceUtc))
            {
                return;
            }

            var rejectedFateId = fateid;
            RetireTrackedFateState($"FATE {rejectedFateId} remained incomplete after the snapshot grace period");
            participatedInTrackedFate = false;
            Log.Warning($"Abandoned FATE {rejectedFateId} because its active snapshot remained incomplete for {FateSnapshotTransitionGracePeriod.TotalSeconds:0.#} seconds.");
        }

        /// <summary>
        /// Verifies the semantic fields required for navigation, rather than trusting pointer
        /// validity alone. Empty names, zero geometry, expired timers, mismatched IDs, and event
        /// types that LLFate cannot safely automate are rejected before selection or travel.
        /// </summary>
        /// <param name="fate">The live RebornBuddy FATE wrapper to inspect.</param>
        /// <param name="expectedId">The selected ID that the live wrapper must continue to represent, or zero for selection.</param>
        /// <param name="logRejected">Whether to report the first rejected snapshot for this ID during the session.</param>
        /// <returns><c>true</c> only when the snapshot contains a safe, active navigation target.</returns>
        private bool IsUsableFate(FateData fate, uint expectedId = 0, bool logRejected = false)
        {
            FateSnapshot snapshot;
            return TryReadFateSnapshot(fate, out snapshot) &&
                   IsUsableFateSnapshot(snapshot, expectedId, logRejected);
        }

        /// <summary>
        /// Copies all native FATE fields needed by LLFate under one guarded read. A slot can retire
        /// between any two property accesses; a failed copy is therefore treated as an unavailable
        /// snapshot and handled by the existing transition grace period rather than escaping as a
        /// GreyMagic memory exception.
        /// </summary>
        /// <param name="fate">Live RebornBuddy wrapper to copy.</param>
        /// <param name="snapshot">Primitive snapshot when every required field was read.</param>
        /// <returns><c>true</c> when the complete snapshot was captured.</returns>
        private static bool TryReadFateSnapshot(FateData fate, out FateSnapshot snapshot)
        {
            snapshot = default(FateSnapshot);
            if (fate == null)
            {
                return false;
            }

            try
            {
                snapshot = new FateSnapshot
                {
                    IsValid = fate.IsValid,
                    Id = fate.Id,
                    Name = fate.Name,
                    Location = fate.Location,
                    Radius = fate.Radius,
                    Level = Convert.ToInt32(fate.Level),
                    MaxLevel = Convert.ToInt32(fate.MaxLevel),
                    Status = fate.Status,
                    TimeLeft = fate.TimeLeft,
                    Icon = fate.Icon,
                    Progress = Convert.ToInt32(fate.Progress)
                };
                return true;
            }
            catch (Exception exception)
            {
                if (exception.GetType().Name != "ReadWriteMemoryException")
                {
                    throw;
                }

                // GreyMagic's exception is private in current RB builds, so exact runtime-name
                // matching is the narrowest source-compatible guard available to this tag.
                snapshot = default(FateSnapshot);
                return false;
            }
        }

        /// <summary>Applies LLFate's semantic safety policy to a stable primitive snapshot.</summary>
        /// <param name="snapshot">Complete FATE fields captured by <see cref="TryReadFateSnapshot"/>.</param>
        /// <param name="expectedId">Selected ID that the snapshot must represent, or zero during selection.</param>
        /// <param name="logRejected">Whether to report the first rejected snapshot for this ID.</param>
        /// <returns><c>true</c> only when the snapshot is a safe active navigation target.</returns>
        private bool IsUsableFateSnapshot(FateSnapshot snapshot, uint expectedId, bool logRejected)
        {
            var id = snapshot.Id;
            var name = snapshot.Name;
            var location = snapshot.Location;
            var radius = snapshot.Radius;
            var level = snapshot.Level;
            var maxLevel = snapshot.MaxLevel;
            var status = snapshot.Status;
            var timeLeft = snapshot.TimeLeft;

            // TODO: Revisit KillHandIn support when LLFate has a reliable way to identify the
            // event-specific key item, select the correct receiver, and recover every dialog state.
            // Until then, rejecting the icon here protects open, focused-ID, refresh, and tracked
            // FATE paths from entering the old unsafe hand-in behavior.
            if (snapshot.Icon == FateIconType.KillHandIn)
            {
                unsupportedFateIds.Add(id);
                supportedFateIds.Remove(id);
                if (logRejected && reportedPolicySkipFateIds.Add(id))
                {
                    Log.Information($"Skipping unsupported hand-in FATE \"{name}\" ({id}).");
                }

                return false;
            }

            var usable = snapshot.IsValid &&
                         id != 0 &&
                         (expectedId == 0 || id == expectedId) &&
                         status == FateStatus.ACTIVE &&
                         !string.IsNullOrWhiteSpace(name) &&
                         location != Vector3.Zero &&
                         IsFinite(location.X) && IsFinite(location.Y) && IsFinite(location.Z) &&
                         radius > 0 &&
                         level > 0 &&
                         maxLevel > 0 &&
                         timeLeft > TimeSpan.Zero;

            if (!usable && logRejected && rejectedFateIds.Add(id))
            {
                Log.Warning($"Rejected incomplete FATE snapshot: Id={id}, Name=\"{name}\", Location={location}, Radius={radius}, Level={level}, MaxLevel={maxLevel}, Status={status}, TimeLeft={timeLeft}.");
            }

            if (usable)
            {
                // A validated non-hand-in definition remains safe for the short defensive handoff
                // after its FateData wrapper disappears; distance is still bounded separately.
                supportedFateIds.Add(id);
            }

            return usable;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void CountDeath()
        {
            var isDead = Core.Me.IsDead;
            if (isDead && !wasDead)
            {
                died++;
            }

            wasDead = isDead;
        }

        [Obsolete]
        private async Task<bool> EscortFate()
        {
            Log.Information("Escort FATE");

            if (fateIcon == FateIconType.ProtectNPC ||
                fateIcon == FateIconType.ProtectNPC2)
            {
                var npc = GameObjectManager
                    .GetObjectsOfType<BattleCharacter>()
                    .FirstOrDefault(b => b.IsFate && !b.CanAttack && b.FateId == fateid);
                Log.Information($"NPC = {npc}");
                if (npc != null && npc.IsValid && (npc.IsBehind || npc.IsFlanking) &&
                    Core.Me.Distance(npc) > 7)
                {
                    Log.Information("Moving using escort FATE logic.");
                    Navigator.Stop();

                    bool IsInFront()
                    {
                        return !npc.IsBehind;
                    }

                    Log.Verbose("M1");
                    await Coroutine.Sleep(500);
                    Log.Verbose("M2");
                    MovementManager.MoveForwardStart();
                    while (npc.IsValid && (Core.Me.Distance(npc) > 1 || !IsInFront()))
                    {
                        Core.Me.Face(npc);
                        if (!MovementManager.IsMoving)
                        {
                            MovementManager.MoveForwardStart();
                        }

                        await Coroutine.Sleep(200);
                    }

                    Log.Verbose("M3");
                    await Coroutine.Sleep(700);
                    MovementManager.MoveForwardStop();

                    Log.Information("Reached destination, moving stopped.");
                }
                else
                {
                    if (FateManager.WithinFate)
                    {
                        Log.Information("Idle in escort fate.");
                    }
                }
            }
            else
            {
                TrySetFateCombatPoi();
            }

            return true;
        }

        /// <summary>
        /// Checks whether a hunt target is still in its post-abandonment cooldown. Entries expire
        /// lazily so the provider remains inexpensive while avoiding permanent rejection of a
        /// recycled game-object ID.
        /// </summary>
        /// <param name="objectId">Transient game-object ID being considered for targeting.</param>
        /// <returns><c>true</c> while the actor remains temporarily suppressed.</returns>
        private bool IsIdleHuntTargetAbandoned(uint objectId)
        {
            DateTime suppressedUntil;
            if (!abandonedIdleHuntTargetsUntil.TryGetValue(objectId, out suppressedUntil))
            {
                return false;
            }

            if (DateTime.UtcNow < suppressedUntil)
            {
                return true;
            }

            abandonedIdleHuntTargetsUntil.Remove(objectId);
            return false;
        }

        /// <summary>
        /// Releases every movement and combat objective owned by LLFate before profile control is
        /// returned. Navigator and Flightor are independent movement systems, while the current
        /// target can outlive a cleared POI, so all four inputs must be retired together.
        /// </summary>
        /// <param name="reason">Diagnostic reason recorded with the POI cleanup.</param>
        private static void ReleaseOwnedControl(string reason)
        {
            Flightor.Clear();
            Navigator.Stop();
            MovementManager.MoveStop();
            Poi.Clear(reason);
            // OnDone can run during a loading transition where RB has temporarily released the
            // local player wrapper. Movement cleanup is still required, but target cleanup must
            // not turn a normal profile stop into a null-reference failure.
            if (Core.Player != null)
            {
                Core.Player.ClearTarget();
            }
        }

        [Obsolete]
        private void OnTimeout()
        {
            _done = true;
            Log.Information("Timeout we are done for now.");
            // A completed tag must not leave an aetheryte movement request racing the next profile
            // behavior. The profile owns any desired timeout recovery or inter-zone travel.
            ReleaseOwnedControl("LLFate timeout completed");
            ReportSessionSummary("timeout");
        }

        [Obsolete]
        private void OnDoneWhile()
        {
            _done = true;
            Log.Information("Completed While Condition");
            // Completion hands control back to the owning profile immediately. Starting another
            // movement here races its teleport/next-step logic and was the source of visible
            // travel after the requested reward count had already been reached.
            ReleaseOwnedControl("LLFate While condition completed");
            ReportSessionSummary("while condition completed");
        }

        // A tag can reach OnDone after its normal stop path, so this guard keeps the
        // user-facing totals visible without printing the same report twice.
        private void ReportSessionSummary(string reason)
        {
            if (sessionSummaryReported)
            {
                return;
            }

            sessionSummaryReported = true;
            Log.Information("--------------------------------------");
            Log.Information($"LLFate session ended: {reason}.");
            Log.Information($"Confirmed FATE completions: {fatesDone}.");
            Log.Information($"Participated FATE endings without a client completion result: {participatedFateEndings}.");
            Log.Information($"Confirmed idle-hunt kills: {mobsHunted}.");
            Log.Information($"Deaths: {died}.");
            Log.Information("--------------------------------------");
        }

        /// <summary>
        /// Attempts to publish a combat POI for the currently tracked FATE. FATE waves can have
        /// short periods with no targetable actors, and flight can leave the player above a FATE
        /// whose actors are not yet loaded. A null Kill POI is invalid in RebornBuddy and is
        /// immediately cleared by combat scheduling, so waiting quietly here prevents a set/clear
        /// loop on every bot pulse.
        /// </summary>
        /// <returns><c>true</c> when a valid target was assigned; otherwise <c>false</c>.</returns>
        private bool TrySetFateCombatPoi()
        {
            // CombatTargeting owns target ranking once it has published a safe Kill POI. Replacing
            // that POI here with the nearest actor made the two selectors alternate targets on
            // every scheduling pulse, which could starve navigation and trigger anti-stuck logic.
            // Forlorn actors remain an intentional exception because the tag has always promoted
            // those time-sensitive bonus targets ahead of ordinary FATE enemies.
            var existingTarget = Poi.Current?.BattleCharacter;
            var target = GetFateTargets();
            var priorityTargetAvailable = target != null &&
                                          (target.NpcId == forelornMaiden || target.NpcId == theForlorn);
            var existingTargetHasPriority = existingTarget != null &&
                                            (existingTarget.NpcId == forelornMaiden || existingTarget.NpcId == theForlorn);
            if (Poi.Current?.Type == PoiType.Kill &&
                IsEligibleTrackedFateCombatTarget(existingTarget) &&
                (!priorityTargetAvailable || existingTargetHasPriority))
            {
                return true;
            }

            if (target == null || !target.IsValid)
            {
                return false;
            }

            Poi.Current = new Poi(target, PoiType.Kill);
            return true;
        }

        /// <summary>
        /// Finds the nearest visible, attackable actor with a clear combat ray in the currently
        /// tracked FATE. Restricting by the selected ID prevents overlapping events from donating
        /// an unrelated target while LLFate is waiting for the selected event's next wave.
        /// </summary>
        /// <returns>A valid target candidate, or <c>null</c> when the FATE has no active target.</returns>
        public GameObject GetFateTargets()
        {
            if (fateid == 0)
            {
                return null;
            }

            var target = GameObjectManager.GameObjects
                .Select(unit => new { unit, bc = unit as BattleCharacter })
                .Where(x => IsEligibleTrackedFateCombatTarget(x.bc))
                .OrderByDescending(x => x.bc.NpcId == forelornMaiden || x.bc.NpcId == theForlorn)
                .ThenBy(x => x.unit.Distance(Core.Player.Location))
                .Select(x => x.unit)
                .FirstOrDefault();

            // CombatTargeting calls this provider repeatedly, including while no FATE is
            // active. Do not log each query; selection and completion transitions already
            // provide the useful user-facing diagnostics without idle-period log spam.
            return target;
        }

        /// <summary>
        /// Applies one eligibility policy to both direct POI publication and retention. Keeping
        /// these checks identical prevents a target accepted by one LLFate path from being
        /// immediately rejected or replaced by the other.
        /// </summary>
        /// <param name="unit">The live FATE combat actor to validate.</param>
        /// <returns><c>true</c> when the actor is safe to retain or publish as the Kill POI.</returns>
        private bool IsEligibleTrackedFateCombatTarget(BattleCharacter unit)
        {
            return unit != null &&
                   unit.IsValid &&
                   unit.IsFate &&
                   unit.CanAttack &&
                   unit.IsTargetable &&
                   unit.IsVisible &&
                   !unit.IsDead &&
                   IsLineOfSightCandidate(unit) &&
                   !IsUnsupportedOrUnresolvedFateActor(unit) &&
                   IsInsideTrackedFateTargetBoundary(unit);
        }

        /// <summary>
        /// Finds the nearest selected-FATE actor that is temporarily hidden but still suitable for
        /// a bounded ground approach. This method deliberately does not create a combat POI; the
        /// caller must first acquire line of sight so combat routines cannot cast through geometry.
        /// </summary>
        /// <returns>An approach candidate, or <c>null</c> when no unsuppressed actor is available.</returns>
        private BattleCharacter GetFateApproachTarget()
        {
            if (fateid == 0)
            {
                return null;
            }

            return GameObjectManager.GetObjectsOfType<BattleCharacter>()
                .Where(unit => unit.IsValid &&
                               unit.IsFate &&
                               unit.CanAttack &&
                               unit.IsTargetable &&
                               unit.IsVisible &&
                               !unit.IsDead &&
                               !IsUnsupportedOrUnresolvedFateActor(unit) &&
                               IsInsideTrackedFateTargetBoundary(unit) &&
                               !IsLineOfSightSuppressed(unit.ObjectId))
                .OrderByDescending(unit => unit.NpcId == forelornMaiden || unit.NpcId == theForlorn)
                .ThenBy(unit => unit.Distance(Core.Player.Location))
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds the nearest conservative non-FATE target for optional downtime hunting.
        /// The radius and health ceiling prevent LLFate from roaming away or selecting an
        /// obviously unsafe target while it waits for an eligible FATE.
        /// </summary>
        /// <returns>The nearest eligible battle character, or <c>null</c> when none is safe.</returns>
        public GameObject GetNormalTargets()
        {
            return GameObjectManager.GetObjectsOfType<BattleCharacter>()
                .Where(unit => unit.CanAttack &&
                               unit.IsTargetable &&
                               unit.IsVisible &&
                               IsLineOfSightCandidate(unit) &&
                               !unit.IsDead &&
                               unit.FateId == 0 &&
                               unit.NpcId != 541 &&
                               !IsIdleHuntTargetAbandoned(unit.ObjectId) &&
                               unit.Distance(Core.Player.Location) <= HuntRadius &&
                               unit.MaxHealth <= Core.Me.CurrentHealth * 3)
                .OrderBy(unit => unit.Distance(Core.Player.Location))
                .FirstOrDefault();
        }

        /// <summary>Applies LLFate's configured validity, progress, and blacklist rules.</summary>
        /// <param name="List">Active FATE snapshots to evaluate.</param>
        /// <returns>The eligible snapshots, with normal selection diagnostics written to the log.</returns>
        public List<FateData> MyFilter(List<FateData> List)
        {
            return FilterFateCandidates(List, logCandidates: true);
        }

        /// <summary>
        /// Applies the shared candidate rules with optional diagnostics. Post-combat re-selection
        /// uses the quiet path because the same candidates were already reported during the initial
        /// scan and logging them again would obscure the meaningful destination-switch message.
        /// </summary>
        private List<FateData> FilterFateCandidates(List<FateData> candidates, bool logCandidates)
        {
            var ReturnList = new List<FateData>();
            foreach (var f in candidates)
            {
                FateSnapshot snapshot;
                if (!TryReadFateSnapshot(f, out snapshot) || !IsUsableFateSnapshot(snapshot, 0, true))
                {
                    continue;
                }
                if (IsLandingFailureCoolingDown(snapshot.Id))
                {
                    continue;
                }
                if ((hasExplicitMinLevel && snapshot.Level < _min) ||
                    (hasExplicitMaxLevel && snapshot.Level > _max))
                {
                    // Open mode follows the same optional-bound contract as focused IDs. Applying
                    // an omitted bound as zero makes every real FATE ineligible even though the XML
                    // intentionally requested no level restriction.
                    if (logCandidates && reportedPolicySkipFateIds.Add(snapshot.Id))
                    {
                        Log.Information($"Skipping FATE \"{snapshot.Name}\". Level {snapshot.Level} is outside the explicitly configured level bounds.");
                    }
                    continue;
                }
                if (snapshot.Progress < MinProgress)
                {
                    if (logCandidates && reportedPolicySkipFateIds.Add(snapshot.Id))
                    {
                        Log.Information($"Skipping FATE \"{snapshot.Name}\". Progress {snapshot.Progress}% is below the configured {MinProgress}% minimum.");
                    }
                }
                else if (snapshot.Icon.ToString() == "Boss" && snapshot.Progress > 85)
                {
                    if (logCandidates && reportedPolicySkipFateIds.Add(snapshot.Id))
                    {
                        Log.Information($"Skipping FATE \"{snapshot.Name}\". Boss FATE progress is greater than 85%.");
                    }
                }
                else if (IsBlacklistedFate(snapshot.Name, snapshot.Id))
                {
                    //Log.Information($"Skipping FATE {f.Name}. FATE is blacklisted");
                }
                else
                {
                    ReturnList.Add(f);

                    if (logCandidates)
                    {
                        Log.Information($"Adding FATE \"{snapshot.Name}\". Distance is {Core.Me.Distance(snapshot.Location)}.");
                    }
                }
            }

            return ReturnList;
        }

        /// <summary>
        /// Checks whether a FATE is still inside its landing-recovery cooldown and lazily expires
        /// old entries so recurring events become eligible again without an unbounded cache.
        /// </summary>
        private bool IsLandingFailureCoolingDown(uint fateId)
        {
            DateTime retryAfter;
            if (!landingFailureCooldowns.TryGetValue(fateId, out retryAfter))
            {
                return false;
            }

            if (DateTime.UtcNow < retryAfter)
            {
                return true;
            }

            landingFailureCooldowns.Remove(fateId);
            return false;
        }

        /// <summary>
        /// Applies the same built-in name and profile ID blacklist to open and focused selection.
        /// Focused IDs narrow the candidate set but must not override explicit safety exclusions.
        /// </summary>
        /// <param name="name">Candidate event name copied from its guarded snapshot.</param>
        /// <param name="id">Candidate event ID copied from its guarded snapshot.</param>
        /// <returns><c>true</c> when either configured blacklist excludes the event.</returns>
        private bool IsBlacklistedFate(string name, uint id)
        {
            return fatebotInstance.BlackListedFates.Contains(name) ||
                   (BlacklistIds ?? Array.Empty<int>()).Contains((int)id);
        }

        /// <summary>
        /// Selects the best currently eligible event without mutating LLFate's tracked destination.
        /// Events already containing the player outrank ordinary distance so chain FATEs that spawn
        /// at the previous event's location are not abandoned for an older distant candidate.
        /// </summary>
        /// <param name="logCandidates">Whether to emit the normal candidate-discovery messages.</param>
        /// <returns>The best live FATE snapshot, or <c>null</c> when none is eligible.</returns>
        private FateData SelectBestEligibleFate(bool logCandidates)
        {
            if (FateIds.Length > 0)
            {
                var focusedFate = IsFateActive(FateIds);
                if (logCandidates && focusedFate != null)
                {
                    FateSnapshot focusedSnapshot;
                    if (TryReadFateSnapshot(focusedFate, out focusedSnapshot))
                    {
                        Log.Information($"Adding Focused FATE: {focusedSnapshot.Name}. Distance is {Core.Me.Distance(focusedSnapshot.Location)}.");
                    }
                }

                return focusedFate;
            }

            var candidates = new List<KeyValuePair<FateData, FateSnapshot>>();
            foreach (var fate in FilterFateCandidates(FateManager.ActiveFates.ToList(), logCandidates))
            {
                FateSnapshot snapshot;
                // Filtering and distance ordering are separate operations over live native rows.
                // Reapply every selection rule to the snapshot that will actually be sorted so a
                // FATE cannot complete, expire, or cross a policy threshold between those reads.
                if (TryReadFateSnapshot(fate, out snapshot) &&
                    IsUsableFateSnapshot(snapshot, 0, false) &&
                    !IsLandingFailureCoolingDown(snapshot.Id) &&
                    (!hasExplicitMinLevel || snapshot.Level >= _min) &&
                    (!hasExplicitMaxLevel || snapshot.Level <= _max) &&
                    snapshot.Progress >= MinProgress &&
                    !(snapshot.Icon.ToString() == "Boss" && snapshot.Progress > 85) &&
                    !IsBlacklistedFate(snapshot.Name, snapshot.Id))
                {
                    candidates.Add(new KeyValuePair<FateData, FateSnapshot>(fate, snapshot));
                }
            }

            var selected = candidates
                .OrderByDescending(candidate => Core.Me.Distance(candidate.Value.Location) <= candidate.Value.Radius)
                .ThenBy(candidate => Core.Me.Distance(candidate.Value.Location))
                .FirstOrDefault();
            return selected.Key;
        }

        public async Task<bool> GetFates()
        {
            if (ShouldStop())
            {
                currentfate = null;
                return false;
            }

            if (SharedFate)
            {
                await LlamaLibrary.ScriptConditions.Extras.UpdateSharedFates();
                if (ShouldStop())
                {
                    currentfate = null;
                    return false;
                }
            }

            currentfate = SelectBestEligibleFate(logCandidates: true);
            return currentfate != null;
        }

        /// <summary>
        /// Selects an active event from an explicit focused-ID profile. Containing the player and
        /// then proximity determine priority so focused chain profiles receive the same post-combat
        /// behavior as open-zone selection while retaining their historical optional level bounds.
        /// </summary>
        /// <param name="ids">Allowed FATE IDs from the profile.</param>
        /// <returns>The best eligible focused event, or <c>null</c> when none is active.</returns>
        public FateData IsFateActive(int[] ids)
        {
            var candidates = new List<KeyValuePair<FateData, FateSnapshot>>();
            foreach (var fate in FateManager.ActiveFates)
            {
                FateSnapshot snapshot;
                if (!TryReadFateSnapshot(fate, out snapshot) ||
                    !ids.Contains((int)snapshot.Id) ||
                    snapshot.Progress < MinProgress ||
                    // Focused FateId profiles historically did not require level attributes. Apply
                    // each bound only when its XML attribute was supplied, preserving those profiles
                    // while still honoring intentional one-sided or two-sided constraints.
                    (hasExplicitMinLevel && snapshot.Level < _min) ||
                    (hasExplicitMaxLevel && snapshot.Level > _max) ||
                    IsLandingFailureCoolingDown(snapshot.Id) ||
                    IsBlacklistedFate(snapshot.Name, snapshot.Id) ||
                    !IsUsableFateSnapshot(snapshot, 0, true))
                {
                    continue;
                }

                candidates.Add(new KeyValuePair<FateData, FateSnapshot>(fate, snapshot));
            }

            var selected = candidates
                .OrderByDescending(candidate => Core.Me.Distance(candidate.Value.Location) <= candidate.Value.Radius)
                .ThenBy(candidate => Core.Me.Distance(candidate.Value.Location))
                .FirstOrDefault();
            return selected.Key;
        }

        protected override void OnResetCachedDone()
        {
            _done = false;
        }

        private ITargetingProvider cachedProvider;
        private MySuperAwesomeTargetingProvider installedProvider;

        protected override void OnStart()
        {
            hasExplicitMinLevel = !string.IsNullOrWhiteSpace(MinLevel);
            hasExplicitMaxLevel = !string.IsNullOrWhiteSpace(MaxLevel);
            _min = hasExplicitMinLevel ? Convert.ToInt32(MinLevel) : 0;
            _max = hasExplicitMaxLevel ? Convert.ToInt32(MaxLevel) : 0;
            _timeout = Convert.ToInt32(Timeout);
            currentstep = 0;
            fatesDone = 0;
            participatedFateEndings = 0;
            mobsHunted = 0;
            died = 0;
            wasDead = false;
            sessionSummaryReported = false;
            huntedTargetObjectId = 0;
            idleHuntAnchor = null;
            defensiveCombatAnchor = null;
            postFateCombatLeashAnchor = null;
            combatRetirementDeadlineUtc = default(DateTime);
            abandonedIdleHuntTargetsUntil.Clear();
            rejectedCenterLandingFateId = 0;
            landingRecoveryFateId = 0;
            centerLandingAssessmentFailures = 0;
            landingPointScanFailures = 0;
            landingFailureCooldowns.Clear();
            llFateZoneId = WorldManager.ZoneId;
            zoneSafetyTriggered = false;
            participatedInTrackedFate = false;
            rejectedFateIds.Clear();
            reportedPolicySkipFateIds.Clear();
            supportedFateIds.Clear();
            unsupportedFateIds.Clear();
            lineOfSightSuppressedUntil.Clear();
            obstructedPoiObjectId = 0;
            obstructedPoiSinceUtc = default(DateTime);
            trackedFateUnavailableSinceUtc = default(DateTime);
            trackedFateInvalidSinceUtc = default(DateTime);
            Log.Information(HuntBetweenFates ? "Doing FATEs and hunting nearby targets in between." : "Doing FATEs; downtime hunting is disabled.");
            Log.Information($"Stats: MinFate level={_min} MaxFatelvl={_max}");

            // ProfileOrderBehavior_Hook is skipped while any Kill POI exists. PoiAction is the
            // earliest ordinary brain slot that continues to pulse through combat, allowing the
            // tag to retire ended FATEs and enforce target leashes without claiming the tick.
            if (safetyPulseHook != null)
            {
                TreeHooks.Instance.RemoveHook("PoiAction", safetyPulseHook);
            }
            safetyPulseHook = new Action(_ => RunSafetyPulse());
            // Safety retirement must precede any optional PoiAction hook that may claim the tick
            // with Success or Running; appending here would make the leashes silently order-dependent.
            TreeHooks.Instance.InsertHook("PoiAction", 0, safetyPulseHook);

            // MaxLevel = "34";
            // MinLevel = "25";
            // Profile behaviors can be restarted after an interrupted scheduling lifecycle without
            // receiving OnDone. If our prior provider is still installed, restore the original
            // provider before caching state; otherwise OnDone would restore LLFate's stale callback.
            if (installedProvider != null &&
                ReferenceEquals(CombatTargeting.Instance.Provider, installedProvider))
            {
                CombatTargeting.Instance.Provider = cachedProvider;
            }

            installedProvider = null;
            cachedProvider = CombatTargeting.Instance.Provider;
            installedProvider = new MySuperAwesomeTargetingProvider(IsInsideActiveTargetingLeashes);
            CombatTargeting.Instance.Provider = installedProvider;
            currentfate = null;
            fateid = 0;
            fateName = string.Empty;
            fateRadius = 0;
            fateIcon = default(FateIconType);
            fateMaxLevel = 0;
            Position = Vector3.Zero;
            Poi.Clear("Clearing POI");
            saveNow = DateTime.Now;
        }

        protected override void OnDone()
        {
            if (safetyPulseHook != null)
            {
                TreeHooks.Instance.RemoveHook("PoiAction", safetyPulseHook);
                safetyPulseHook = null;
            }

            ReportSessionSummary("tag stopped");
            ReleaseOwnedControl("LLFate tag stopped");
            currentstep = 0;
            idleHuntAnchor = null;
            defensiveCombatAnchor = null;
            ClearCombatRetirementState();
            rejectedCenterLandingFateId = 0;

            // Do not overwrite a provider another component deliberately installed while LLFate
            // was active; restore only when the provider is still the instance owned by this tag.
            if (ReferenceEquals(CombatTargeting.Instance.Provider, installedProvider))
            {
                CombatTargeting.Instance.Provider = cachedProvider;
            }

            installedProvider = null;
        }
    }

    //----------------------------------------------------------------------------------

    public class MySuperAwesomeTargetingProvider : ITargetingProvider
    {
        // Standalone legacy callers do not provide LLFate's live radius callback. Preserve their
        // historical 50-yalm behavior while the tag-owned provider delegates to the selected
        // event's real radius and tolerance through admissionConstraint.
        private const float LegacyFateTargetRadius = 50f;

        public HashSet<uint> IgnoreNpcIds = new HashSet<uint>()
        {
            1201
        };

        private BattleCharacter[] _aggroedBattleCharacters;
        private uint currentKillPoiObjectId;
        private readonly Func<BattleCharacter, bool> admissionConstraint;

        /// <summary>
        /// Creates LLFate's targeting provider with an optional owner-supplied admission policy.
        /// The callback lets runtime leashes constrain RebornBuddy's independent targeting pulse
        /// instead of relying on POI cleanup after an unsafe unit was already admitted.
        /// </summary>
        /// <param name="admissionConstraint">A live unit predicate, or <c>null</c> for legacy admission behavior.</param>
        public MySuperAwesomeTargetingProvider(Func<BattleCharacter, bool> admissionConstraint = null)
        {
            this.admissionConstraint = admissionConstraint;
        }

        /// <summary> Gets the objects by weight. </summary>
        /// <remarks> Nesox, 2013-06-29. </remarks>
        /// <returns> The objects by weight. </returns>
        public List<BattleCharacter> GetObjectsByWeight()
        {
            // CombatTargeting can pulse before LLFate's PoiAction safety hook. Resolve the retained
            // POI once from the current object table so ranking and idle-hunt admission never read
            // combat fields from the stale wrapper stored when the POI was originally assigned.
            currentKillPoiObjectId = ResolveCurrentKillPoiObjectId();
            var allUnits = GameObjectManager.GetObjectsOfType<BattleCharacter>().ToArray();

            _aggroedBattleCharacters = GameObjectManager.Attackers.ToArray();
            var inCombat = Core.Player.InCombat;
            var hostileUnits = allUnits.Where(r => IsValidUnit(inCombat, r))
                .Select(n => new Score
                {
                    Unit = n,
                    Weight = GetScoreForUnit(n)
                }).ToArray();

            // A valid Kill POI is already the target chosen for this combat engagement. Preserve
            // it ahead of score ordering so repeated CombatTargeting pulses cannot oscillate
            // between similarly ranked actors and starve movement. Invalid, dead, obstructed, or
            // out-of-leash POIs never enter hostileUnits and therefore receive no sticky priority.
            return hostileUnits
                // Preserve LLFate's established priority for the two time-sensitive Forlorn bonus
                // actors; stickiness applies within that priority tier, not ahead of it.
                .OrderByDescending(s => s.Unit.NpcId == 6737 || s.Unit.NpcId == 6738)
                .ThenByDescending(s => currentKillPoiObjectId != 0 && s.Unit.ObjectId == currentKillPoiObjectId)
                .ThenByDescending(s => s.Weight)
                .Select(s => s.Unit)
                .ToList();
        }

        /// <summary> Query if 'unit' is valid unit. </summary>
        /// <remarks> Nesox, 2013-06-29. </remarks>
        private bool IsValidUnit(bool incombat, BattleCharacter unit)
        {
            if (!unit.IsValid || !unit.CanAttack || !unit.IsTargetable || !unit.InLineOfSight() ||
                unit.IsDead || !unit.IsVisible || unit.CurrentHealthPercent <= 0)
            {
                return false;
            }

            // Selection rejects hand-in events, but CombatTargeting pulses independently and can
            // otherwise reacquire an enemy from an overlapping FATE. Enforce the unsupported icon
            // here as well so no LLFate-owned targeting path can engage a KillHandIn event.
            if (unit.IsFate && admissionConstraint == null)
            {
                var owner = unit.FateId == 0 ? null : FateManager.GetFateById(unit.FateId);
                if (IsMissingOrUnsupportedFateOwner(owner))
                {
                    // Legacy standalone callers lack LLFate's supported/unsupported caches, so
                    // unknown ownership must remain fail-closed for them. Tag-owned providers use
                    // admissionConstraint, which permits cached supported retirement targets only.
                    return false;
                }
            }

            if (IgnoreNpcIds.Contains(unit.NpcId))
            {
                return false;
            }

            if (admissionConstraint != null && !admissionConstraint(unit))
            {
                return false;
            }

            // Ignore blacklisted mobs if they're in combat with us!
            if (Blacklist.Contains(unit.ObjectId, BlacklistFlags.Combat))
            {
                return false;
            }

            var fategone = unit.IsFateGone;
            if (fategone)
            {
                return false;
            }

            // The custom provider historically admitted only FATE units and attackers. Explicitly
            // retaining the tag's kill POI is what lets optional downtime hunting enter combat.
            var isSelectedIdleHuntTarget = currentKillPoiObjectId != 0 &&
                                           currentKillPoiObjectId == unit.ObjectId &&
                                           !unit.IsFate;

            return _aggroedBattleCharacters.Contains(unit) ||
                   (unit.CanAttack && !incombat &&
                    (unit.IsFate &&
                     (admissionConstraint != null ||
                      Vector3.Distance(unit.Location, LLFate.Position) <= LegacyFateTargetRadius) ||
                     isSelectedIdleHuntTarget));
        }

        /// <summary>
        /// Reads the one owner field needed by the legacy provider under a narrow native-wrapper
        /// guard. A FATE row disappearing during CombatTargeting must reject the actor, not fault
        /// the provider and leave RB acting on its previous selection.
        /// </summary>
        /// <param name="owner">Live owner row for a FATE actor.</param>
        /// <returns><c>true</c> when ownership cannot be verified or belongs to a hand-in FATE.</returns>
        private static bool IsMissingOrUnsupportedFateOwner(FateData owner)
        {
            if (owner == null)
            {
                return true;
            }

            try
            {
                return !owner.IsValid || owner.Icon == FateIconType.KillHandIn;
            }
            catch (Exception exception)
            {
                if (exception.GetType().Name != "ReadWriteMemoryException")
                {
                    throw;
                }

                return true;
            }
        }

        /// <summary>
        /// Resolves the retained Kill POI to a current-frame actor and returns its stable object ID.
        /// GreyMagic's memory exception is a private global type in current RB builds, so the narrow
        /// recovery path identifies it by exact runtime name and rethrows every unrelated failure.
        /// </summary>
        /// <returns>The live POI actor ID, or zero when no valid Kill POI remains.</returns>
        private static uint ResolveCurrentKillPoiObjectId()
        {
            if (Poi.Current?.Type != PoiType.Kill)
            {
                return 0;
            }

            try
            {
                var cachedTarget = Poi.Current.BattleCharacter;
                if (cachedTarget == null)
                {
                    Poi.Clear("LLFate targeting provider found an empty Kill POI.");
                    return 0;
                }

                var objectId = cachedTarget.ObjectId;
                var liveTarget = GameObjectManager.GetObjectByObjectId(objectId) as BattleCharacter;
                if (liveTarget != null && liveTarget.IsValid)
                {
                    return objectId;
                }

                Poi.Clear("LLFate targeting provider retired a stale Kill POI.");
                return 0;
            }
            catch (Exception exception)
            {
                if (exception.GetType().Name != "ReadWriteMemoryException")
                {
                    throw;
                }

                Poi.Clear("LLFate targeting provider retired an expired Kill POI wrapper.");
                return 0;
            }
        }

        /// <summary> Gets score for a unit. </summary>
        /// <remarks> Nesox, 2013-06-29. </remarks>
        /// <param name="unit"> The unit. </param>
        /// <returns> The score for unit. </returns>
        private double GetScoreForUnit(BattleCharacter unit)
        {
            double weight = 200 - (2 * unit.Distance());

            weight += unit.MaxHealth; // asuming that bosses have huge health this should make us target a boss

            if (unit.ObjectId == Core.Player.CurrentTargetId)
            {
                // Keep the player's selected unit stable instead of comparing what two unrelated
                // actors happen to be targeting.
                weight += 120;
            }

            //weight -= (int)npc.Toughness * 50;

            // Force 100 weight on any in-combat NPCs.
            if (_aggroedBattleCharacters.Contains(unit))
            {
                weight += 100;
            }

            // Forlorn Maiden
            if (unit.NpcId == 6737 || unit.NpcId == 6738)
            {
                weight += 100000;
            }

            //Units that are targeting the player, focus on low health ones so that we can reduce the incoming damage
            if (unit.CurrentTargetId == Core.Player.ObjectId)
            {
                weight += 100 - unit.CurrentHealthPercent;
            }

            // Less weight on out of combat targets.
            if (!unit.InCombat)
            {
                weight -= 100;
            }

            return weight;
        }

        private class Score
        {
            public BattleCharacter Unit;
            public double Weight;
        }
    }
}
