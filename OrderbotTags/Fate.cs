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

// Adapted from Y2krazy's Fate tag.

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>
    /// Runs eligible FATEs in the starting zone, with optional hunting between events.
    /// Live game state is read on the bot thread; snapshots protect travel from reused FATE slots.
    /// </summary>
    [XmlElement("LLFate")]
    public class LLFate : LLProfileBehavior
    {

        #region Profile configuration and compatibility


        /// <summary>Gets or sets the inclusive maximum FATE level; omitted values impose no upper bound.</summary>
        [XmlAttribute("MaxLevel")]
        public string MaxLevel { get; set; }


        /// <summary>Gets or sets the inclusive minimum FATE level; omitted values impose no lower bound.</summary>
        [XmlAttribute("MinLevel")]
        public string MinLevel { get; set; }


        /// <summary>Gets or sets the profile expression that keeps this tag running; omitted values impose no condition.</summary>
        [XmlAttribute("While")]
        [XmlAttribute("while")]
        public string WhileCondition { get; set; }


        /// <summary>Gets or sets whether to fly in unlocked zones. Ground travel uses GetTo for aetheryte routing.</summary>
        [XmlAttribute("UseFlight")]
        [XmlAttribute("useflight")]
        [DefaultValue(true)]
        public bool UseFlight { get; set; }


        /// <summary>Gets or sets allowed FATE IDs; an empty list enables normal zone-wide selection.</summary>
        [XmlAttribute("FateIDs")]
        [XmlAttribute("FateIds")]
        [XmlAttribute("FateID")]
        [XmlAttribute("FateId")]
        [DefaultValue(new int[0])]
        public int[] FateIds { get; set; }


        /// <summary>Gets or sets excluded FATE IDs, including exclusions from a focused ID list.</summary>
        [XmlAttribute("BlacklistID")]
        [XmlAttribute("BlacklistId")]
        [XmlAttribute("BlacklistIDs")]
        [XmlAttribute("BlacklistIds")]
        [XmlAttribute("Blacklist")]
        [XmlAttribute("blacklist")]
        [DefaultValue(new int[0])]
        public int[] BlacklistIds { get; set; }


        /// <summary>Gets or sets the idle timeout in seconds. Active FATE participation defers this timeout.</summary>
        [XmlAttribute("Timeout")]
        [DefaultValue("600")]
        public string Timeout { get; set; }


        /// <summary>Gets or sets the minimum completion percentage required to select a FATE.</summary>
        [XmlAttribute("MinProgress")]
        [DefaultValue(0)]
        public int MinProgress { get; set; }


        /// <summary>Gets or sets whether to refresh shared-FATE progress before selection for profile conditions.</summary>
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

        /// <summary>Gets whether the tag has finished or stopped for a safety condition.</summary>
        public override bool IsDone => isDone;

        /// <summary>Legacy compatibility field; tag completion is reported through <see cref="IsDone"/>.</summary>
        public bool IsCompleted = false;

        /// <summary>Selected FATE center, or zero when idle. Retained for legacy profile and provider access.</summary>
        public static Vector3 Position = Vector3.Zero;

        /// <summary>Legacy phase field: zero means idle; one means a FATE is selected, including travel.</summary>
        public static int currentstep = 0;

        /// <summary>Compiled While expression, cached for bot-thread evaluation; null until needed.</summary>
        protected Func<bool> condition;

        #endregion

        #region Travel and combat limits


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

        // Bonus enemies take priority over ordinary targets in both POI selection and provider ranking.
        internal const uint ForlornMaidenNpcId = 6737;

        internal const uint ForlornNpcId = 6738;


        // Recalculate the crowd position slowly, but service Navigator every tick. Throttling
        // MoveTo itself leaves the previous movement direction held while a path is pending.
        private readonly Stopwatch clusterTimer = Stopwatch.StartNew();
        private Vector3? clusterDestination;
        private const float ClusterArrivalDistance = 5f;
        private const float ClusterBoundaryInset = 10f;


        // After a FATE retires, combat can linger briefly even though its enemies are already
        // retreating. Keep defensive cleanup local to the handoff point; following a retreating
        // attacker here makes the combat routine run indefinitely away from the completed FATE.
        private const float PostFateCombatLeash = 20f;

        // FATE coordinates describe an event centroid, not guaranteed walkable terrain. Keep the
        // established travel altitude, then validate enemy positions only after reaching object range.
        private const float FateLandingHeight = 15f;

        private const int FateEnemyLandingScanMilliseconds = 3000;

        // Acquire a destination before reaching the overhead waypoint, while local terrain is loaded.
        // A low final waypoint lets the flight path descend diagonally instead of dropping 15 yalms.
        private const float EnemyLandingSearchDistance = 60f;

        private const float EnemyLandingStandOff = 5f;

        private const float EnemyLandingHeight = 2f;

        private const float EnemyLandingDriftTolerance = 8f;

        private static readonly TimeSpan EnemyLandingTimeout = TimeSpan.FromSeconds(30);

        #endregion

        #region Session state

        private bool isDone;

        private int minimumLevel;

        private int maximumLevel;

        private int idleTimeoutSeconds;

        private bool hasExplicitMinLevel;

        private bool hasExplicitMaxLevel;


        private readonly FatebotSettings fateSettings = FatebotSettings.Instance;

        private DateTime idleStartedAt = DateTime.Now;

        private FateData pendingFate;

        // FateData is a live wrapper over a reusable client-table slot. It is retained only long
        // enough to select an event; all state used across bot pulses is copied into primitives.
        private FateIconType fateIcon;

        private int fateMaxLevel;

        private int completedFates;

        private int participatedFateEndings;

        private int mobsHunted;

        private int deathCount;

        private bool wasDead;

        // Retain death across the loading screen: IsDead is already false at the home point.
        private bool recoveringFromDeath;

        private bool deathStateRetired;

        private Vector3 startingLocation;

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

        private uint startingZoneId;

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

        private uint landingRecoveryFateId;

        private int centerLandingAssessmentFailures;

        private int landingPointScanFailures;

        private readonly Dictionary<uint, DateTime> lineOfSightSuppressedUntil = new Dictionary<uint, DateTime>();

        private uint obstructedPoiObjectId;

        private DateTime obstructedPoiSinceUtc;

        private DateTime trackedFateUnavailableSinceUtc;

        private DateTime trackedFateInvalidSinceUtc;

        private Composite safetyPulseHook;

        private Composite deathObservationHook;


        private uint trackedFateId = 0;
        // Arrival is latched per selected event. Combat can legitimately move to the edge of
        // the target leash; reapplying the travel inset there causes repeated center flights.
        private bool hasArrivedAtFate;

        // Hover only after an event ends. A fixed rise and retry delay prevent repeated idle
        // scans from climbing indefinitely or spamming mount attempts under a low ceiling.
        private const float IdleHoverHeight = 15f;
        private bool canHoverWhileIdle;
        private bool ownsIdleFlight;
        private DateTime nextIdleFlightAttemptUtc;
        private DateTime idleLandingDeadlineUtc;

        private float fateRadius;

        private string fateName = "";

        private bool approachingEnemyLanding;


        private ITargetingProvider cachedProvider;

        private FateTargetingProvider installedProvider;

        #endregion

        #region Copied data and outcomes


        /// <summary>
        /// Primitive copy of the live FATE-table fields LLFate consumes in one pulse.
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


        private enum LandingApproachResult
        {
            UseTerrainFallback,
            Landed,
            Interrupted
        }

        #endregion

        #region Lifecycle


        protected override void OnResetCachedDone()
        {
            isDone = false;
        }


        protected override void OnStart()
        {
            hasExplicitMinLevel = !string.IsNullOrWhiteSpace(MinLevel);
            hasExplicitMaxLevel = !string.IsNullOrWhiteSpace(MaxLevel);
            minimumLevel = hasExplicitMinLevel ? Convert.ToInt32(MinLevel) : 0;
            maximumLevel = hasExplicitMaxLevel ? Convert.ToInt32(MaxLevel) : 0;
            idleTimeoutSeconds = Convert.ToInt32(Timeout);
            currentstep = 0;
            clusterDestination = null;
            canHoverWhileIdle = false;
            ownsIdleFlight = false;
            nextIdleFlightAttemptUtc = default(DateTime);
            completedFates = 0;
            participatedFateEndings = 0;
            mobsHunted = 0;
            deathCount = 0;
            wasDead = false;
            recoveringFromDeath = false;
            deathStateRetired = false;
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
            startingZoneId = WorldManager.ZoneId;
            startingLocation = Core.Player.Location;
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
            Log.Information($"Stats: MinFate level={minimumLevel} MaxFatelvl={maximumLevel}");

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

            // Death handling can consume the brain before PoiAction is reached. Observe at
            // TreeStart as well so the death flag survives a return to a different home zone.
            if (deathObservationHook != null)
            {
                TreeHooks.Instance.RemoveHook("TreeStart", deathObservationHook);
            }
            deathObservationHook = new PrioritySelector(new Action(_ =>
            {
                if (!isDone && !CommonBehaviors.IsLoading && Core.Player != null && Core.Player.IsValid)
                {
                    CountDeath();
                }
                return RunStatus.Failure;
            }), new ActionRunCoroutine(_ => HandleLevelSync()),
                new ActionRunCoroutine(_ => HandleIdleFlightCombat()));
            TreeHooks.Instance.InsertHook("TreeStart", 0, deathObservationHook);

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
            installedProvider = new FateTargetingProvider(IsInsideActiveTargetingLeashes);
            CombatTargeting.Instance.Provider = installedProvider;
            pendingFate = null;
            trackedFateId = 0;
            hasArrivedAtFate = false;
            fateName = string.Empty;
            fateRadius = 0;
            fateIcon = default(FateIconType);
            fateMaxLevel = 0;
            Position = Vector3.Zero;
            Poi.Clear("Clearing POI");
            idleStartedAt = DateTime.Now;
        }


        protected override void OnDone()
        {
            if (deathObservationHook != null)
            {
                TreeHooks.Instance.RemoveHook("TreeStart", deathObservationHook);
                deathObservationHook = null;
            }
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

        #endregion

        #region Profile scheduling


        private bool ShouldStop()
        {
            var compiledCondition = GetCondition();
            return compiledCondition != null && !compiledCondition();
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
                TreeRoot.Stop("Unable to compile LLFate While condition.");
                throw;
            }
        }


        protected override Composite CreateBehavior()
        {
            // Keep completion and snapshot recovery ahead of actions that can start movement.
            return new PrioritySelector(
                // Zone-specific While expressions would be false at the home point. Restore the
                // original zone before evaluating them or selecting FATEs in the revival zone.
                new Decorator(_ => recoveringFromDeath, new ActionRunCoroutine(_ => RecoverAfterDeath())),
                new Decorator(_ => ShouldStop(), new Action(_ => OnDoneWhile())),
                new Decorator(_ => currentstep == 0 && DateTime.Now > idleStartedAt.AddSeconds(idleTimeoutSeconds),
                    new Action(_ => OnTimeout())),
                new Decorator(_ => IsTrackedFateTransitionPending(), new Action(_ => RunStatus.Success)),
                new Decorator(_ => NeedsFateTravel(), CreateFateTravelBehavior()),
                new Decorator(_ => currentstep == 1 && trackedFateId != 0 && Poi.Current.Type != PoiType.Kill,
                    new ActionRunCoroutine(_ => MoveToFocusedFate())),
                new Decorator(_ => trackedFateId == 0 && currentstep == 0,
                    new Sequence(new ActionRunCoroutine(_ => SelectFateOrHunt()))),
                new ActionAlwaysSucceed());
        }


        /// <summary>
        /// Syncs before combat can claim the tick. Use live containing events rather than the
        /// selected event's cached level: aggro and overlapping FATEs can precede selection.
        /// RB exposes the sync action but no availability predicate, so the containing event's
        /// level cap determines whether an unsynced player needs it.
        /// </summary>
        private async Task<bool> HandleLevelSync()
        {
            if (isDone || CommonBehaviors.IsLoading || Core.Player == null || !Core.Player.IsValid ||
                Core.Me.IsDead || recoveringFromDeath || Core.Me.IsLevelSynced || !FateManager.WithinFate)
            {
                return false;
            }

            // Eureka's cap uses elemental level. Falling through to class level there would
            // request sync forever for players whose elemental level is already below the cap.
            var playerLevel = Core.Me.ElementalLevel > 0 ? Core.Me.ElementalLevel : Core.Player.ClassLevel;
            var needsSync = FateManager.ActiveFates.Any(eventData =>
                TryReadFateSnapshot(eventData, out var fate) && fate.IsValid &&
                fate.Status == FateStatus.ACTIVE && fate.MaxLevel > 0 && playerLevel > fate.MaxLevel &&
                Core.Player.Location.Distance(fate.Location) <= fate.Radius);
            if (!needsSync)
            {
                return false;
            }

            // Claim this tick while the client processes sync, preventing a Kill POI from
            // starting attacks first. Recheck on the next tick if the UI wasn't ready yet.
            ToDoList.LevelSync();
            await Coroutine.Wait(1000, () => CommonBehaviors.IsLoading || Core.Me.IsDead ||
                                           Core.Me.IsLevelSynced || !FateManager.WithinFate);
            if (!CommonBehaviors.IsLoading && Core.Me.IsLevelSynced)
            {
                Log.Information("Level sync confirmed before combat.");
            }
            return true;
        }


        private bool NeedsFateTravel()
        {
            if (ShouldStop() || currentstep != 1)
            {
                return false;
            }

            if (!MovementManager.IsFlying &&
                Vector3.Distance(Core.Player.Location, Position) <= GetFateArrivalDistance(fateRadius))
            {
                hasArrivedAtFate = true;
            }

            // An airborne player still needs landing, even after the initial arrival.
            return MovementManager.IsFlying || !hasArrivedAtFate;
        }


        private Composite CreateFateTravelBehavior()
        {
            return new PrioritySelector(
                // An airborne player must land through the flight path even if UseFlight is disabled.
                new Decorator(_ => MovementManager.IsFlying ||
                                   (UseFlight && AetherCurrentManager.FinishedZones.Contains(WorldManager.ZoneId)),
                    new ActionRunCoroutine(_ => FlyToFateAndLand())),
                // GetTo retains aetheryte routing. It cannot currently be interrupted by While.
                new Decorator(_ => !MovementManager.IsFlying && WorldManager.ZoneId == startingZoneId &&
                                   IsUsableFate(FateManager.GetFateById(trackedFateId), trackedFateId, false),
                    new ActionRunCoroutine(_ => GroundMoveToFate())));
        }


        private async Task SelectFateOrHunt()
        {
            // Finish incidental combat before selecting travel that could replace its target.
            if (Core.Me.InCombat || GameObjectManager.Attackers.Any())
            {
                return;
            }
            await GetFates();
            // Rewards can arrive while the scan yields; check again before adopting its result.
            if (ShouldStop())
            {
                pendingFate = null;
                Navigator.Stop();
                return;
            }

            if (pendingFate != null)
            {
                AdoptPendingFate();
            }
            else
            {
                TryStartIdleHunt();
                await TryHoverWhileIdle();
            }
        }

        /// <summary>
        /// Rises once above the last event's ground position, yielding to attackers and new FATEs.
        /// Optional hunting retains priority; failed takeoffs wait before retrying.
        /// </summary>
        private async Task TryHoverWhileIdle()
        {
            bool MustStop() => isDone || CommonBehaviors.IsLoading || Core.Me.IsDead ||
                               recoveringFromDeath || Core.Me.InCombat || ShouldStop() ||
                               WorldManager.ZoneId != startingZoneId || trackedFateId != 0 ||
                               Poi.Current.Type == PoiType.Kill || GameObjectManager.Attackers.Any();

            if (!canHoverWhileIdle || !UseFlight || MovementManager.IsFlying || MustStop() ||
                !AetherCurrentManager.FinishedZones.Contains(WorldManager.ZoneId) || !WorldManager.CanFly ||
                DateTime.UtcNow < nextIdleFlightAttemptUtc)
            {
                return;
            }

            nextIdleFlightAttemptUtc = DateTime.UtcNow.AddSeconds(30);
            var destination = Core.Player.Location + new Vector3(0, IdleHoverHeight, 0);
            // Check the entire vertical corridor before takeoff; a ceiling should leave us waiting
            // on the ground, not holding ascend against an obstruction.
            if (WorldManager.Raycast(Core.Player.Location, destination, out var hit))
            {
                return;
            }

            Navigator.Stop();
            if (!await CommonTasks.SummonFlyingMount() || MustStop())
            {
                return;
            }

            ownsIdleFlight = true;
            idleLandingDeadlineUtc = default(DateTime);
            if (!await CommonTasks.TakeOff() || MustStop())
            {
                return;
            }
            var deadline = DateTime.UtcNow.AddSeconds(10);
            try
            {
                MovementManager.StartAscending();
                while (!MustStop() && Core.Player.Location.Y < destination.Y && DateTime.UtcNow < deadline)
                {
                    // Selection is intentionally polled during ascent, rather than waiting until
                    // the full rise finishes before responding to a newly spawned event.
                    if (SelectBestEligibleFate(logCandidates: false) != null)
                    {
                        break;
                    }
                    await Coroutine.Yield();
                }
            }
            finally
            {
                MovementManager.StopAscending();
            }

            if (!MustStop() && MovementManager.IsFlying)
            {
                Log.Information("Waiting above the completed FATE for the next eligible event.");
            }
        }

        /// <summary>
        /// Lets idle-flight aggro return to ground before OrderBot's combat movement claims the tick.
        /// Only our own hover is handled here; normal FATE travel retains its landing behavior.
        /// </summary>
        private async Task<bool> HandleIdleFlightCombat()
        {
            if (!ownsIdleFlight)
            {
                return false;
            }

            if (isDone || CommonBehaviors.IsLoading || Core.Player == null || !Core.Player.IsValid ||
                Core.Me.IsDead || recoveringFromDeath || WorldManager.ZoneId != startingZoneId)
            {
                MovementManager.StopAscending();
                MovementManager.StopDescending();
                ownsIdleFlight = false;
                canHoverWhileIdle = false;
                return false;
            }

            // Once descent starts, finish or time out even if the attacker disappears midair;
            // returning early would leave the descending movement flag held.
            if (idleLandingDeadlineUtc == default(DateTime) && !Core.Me.InCombat &&
                !GameObjectManager.Attackers.Any() && Poi.Current.Type != PoiType.Kill)
            {
                return false;
            }

            MovementManager.StopAscending();
            if (MovementManager.IsFlying)
            {
                if (idleLandingDeadlineUtc == default(DateTime))
                {
                    idleLandingDeadlineUtc = DateTime.UtcNow.AddSeconds(10);
                    Log.Information("Combat target while waiting; landing before engaging.");
                }
                // Takeoff started over known ground, but never hold descent forever if terrain
                // or an external mover changed the position while we were waiting.
                if (DateTime.UtcNow < idleLandingDeadlineUtc && CommonTasks.CanLand(Core.Player.Location) == CanLandResult.Yes)
                {
                    MovementManager.StartDescending();
                    return true;
                }
            }

            MovementManager.StopDescending();
            ownsIdleFlight = false;
            if (!MovementManager.IsFlying && Core.Me.IsMounted)
            {
                await CommonTasks.StopAndDismount();
            }
            return false;
        }


        private void OnTimeout()
        {
            isDone = true;
            Log.Information("Timeout we are done for now.");
            // A completed tag must not leave an aetheryte movement request racing the next profile
            // behavior. The profile owns any desired timeout recovery or inter-zone travel.
            ReleaseOwnedControl("LLFate timeout completed");
            ReportSessionSummary("timeout");
        }


        private void OnDoneWhile()
        {
            isDone = true;
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
            Log.Information($"Confirmed FATE completions: {completedFates}.");
            Log.Information($"Participated FATE endings without a client completion result: {participatedFateEndings}.");
            Log.Information($"Confirmed idle-hunt kills: {mobsHunted}.");
            Log.Information($"Deaths: {deathCount}.");
            Log.Information("--------------------------------------");
        }

        #endregion

        #region FATE selection and tracking


        private void AdoptPendingFate()
        {
            // Selection and movement occur on different pulses. Never arm a destination after a
            // delayed reward has already satisfied the profile's completion condition.
            if (ShouldStop())
            {
                pendingFate = null;
                Navigator.Stop();
                return;
            }

            if (pendingFate != null)
            {
                FateSnapshot snapshot;
                if (!TryReadFateSnapshot(pendingFate, out snapshot) ||
                    !IsUsableFateSnapshot(snapshot, snapshot.Id, false))
                {
                    pendingFate = null;
                    return;
                }

                participatedInTrackedFate = false;
                canHoverWhileIdle = false;
                ownsIdleFlight = false;
                clusterDestination = null;
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
                trackedFateId = snapshot.Id;
                hasArrivedAtFate = false;
                fateName = snapshot.Name;
                fateIcon = snapshot.Icon;
                fateMaxLevel = snapshot.MaxLevel;
                Log.Information($"Fate Details: Name:{fateName} Id:{trackedFateId} Icon:{fateIcon} Location:{Position} Radius:{fateRadius} MaxLevel:{fateMaxLevel}");
                currentstep = 1;
                // A newly selected ID starts a fresh snapshot-observation window. Carrying a timer
                // from the prior event could otherwise make the first incomplete pulse terminal.
                trackedFateUnavailableSinceUtc = default(DateTime);
                trackedFateInvalidSinceUtc = default(DateTime);
                ResetLandingRecoveryState(trackedFateId);
                pendingFate = null;
            }
        }


        /// <summary>
        /// Rechecks selection once after combat ends, before flight starts. This lets a local chain FATE
        /// replace a distant pick without redirecting active travel.
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
            pendingFate = refreshedFate;
            rejectedCenterLandingFateId = 0;
            AdoptPendingFate();
            return currentstep == 1 && trackedFateId == refreshedSnapshot.Id;
        }


        private void UpdateFateData()
        {
            foreach (var item in FateManager.ActiveFates)
            {
                FateSnapshot snapshot;
                if (TryReadFateSnapshot(item, out snapshot) && snapshot.Id == trackedFateId)
                {
                    Position = snapshot.Location;
                    fateName = snapshot.Name;
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


        private void CheckTrackedFateCompletion()
        {
            if (currentstep <= 0 || trackedFateId == 0)
            {
                return;
            }

            var trackedFate = FateManager.GetFateById(trackedFateId);
            FateSnapshot snapshot;
            var hasSnapshot = TryReadFateSnapshot(trackedFate, out snapshot);
            // GetFateById can retain a wrapper whose native table slot has already been reused.
            // Only a valid row that still carries the selected ID may prove activity or completion;
            // every other readable row follows the same debounce as a temporarily missing row.
            var hasMatchingSnapshot = hasSnapshot && snapshot.IsValid && snapshot.Id == trackedFateId;
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

            var endedFateId = trackedFateId;
            var endedFateName = !string.IsNullOrWhiteSpace(fateName) ? fateName : $"FATE {endedFateId}";

            RetireTrackedFateState($"FATE {endedFateId} ended; clearing stale movement");
            canHoverWhileIdle = true;

            if (completionStatusObserved && participatedInTrackedFate)
            {
                completedFates++;
                Log.Information($"Completed FATE \"{endedFateName}\" ({endedFateId}; completion status observed). Confirmed session total: {completedFates}.");
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
            return currentstep > 0 && trackedFateId != 0 &&
                   (trackedFateUnavailableSinceUtc != default(DateTime) ||
                    trackedFateInvalidSinceUtc != default(DateTime));
        }


        /// <summary>
        /// Debounces a missing or malformed FATE row. Each observation timer resets independently when
        /// its healthy state returns.
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
        /// Clears the selected event, arming the combat-retirement leash before targeting can reacquire
        /// remaining attackers.
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
            clusterDestination = null;
            currentstep = 0;
            trackedFateId = 0;
            hasArrivedAtFate = false;
            fateRadius = 0;
            fateIcon = default(FateIconType);
            fateMaxLevel = 0;
            pendingFate = null;
            trackedFateUnavailableSinceUtc = default(DateTime);
            trackedFateInvalidSinceUtc = default(DateTime);
        }


        /// <summary>
        /// Stops if LLFate leaves its starting zone. The owning profile is responsible for inter-zone
        /// travel.
        /// </summary>
        private void EnforceZoneBoundary()
        {
            if (recoveringFromDeath || Core.Me.IsDead || zoneSafetyTriggered || CommonBehaviors.IsLoading || WorldManager.ZoneId == startingZoneId)
            {
                return;
            }

            zoneSafetyTriggered = true;
            Navigator.Stop();
            Poi.Clear("LLFate left its starting zone");
            isDone = true;
            Log.Error($"LLFate unexpectedly left zone {startingZoneId} and entered {WorldManager.ZoneId}; stopping to prevent cross-zone navigation.");
            TreeRoot.Stop("LLFate left its configured zone unexpectedly.");
        }


        /// <summary>
        /// Refreshes the selected event by ID. A valid native pointer alone cannot distinguish a reused
        /// FATE slot.
        /// </summary>
        private void ValidateTrackedFate()
        {
            if (currentstep <= 0 || trackedFateId == 0)
            {
                return;
            }

            var trackedFate = FateManager.GetFateById(trackedFateId);
            FateSnapshot snapshot;
            var hasSnapshot = TryReadFateSnapshot(trackedFate, out snapshot);
            if (hasSnapshot && IsUsableFateSnapshot(snapshot, trackedFateId, true))
            {
                trackedFateInvalidSinceUtc = default(DateTime);
                Position = snapshot.Location;
                fateRadius = snapshot.Radius;
                fateIcon = snapshot.Icon;
                fateMaxLevel = snapshot.MaxLevel;
                return;
            }

            // Missing and non-active wrappers are owned by CheckTrackedFateCompletion, which distinguishes
            // explicit completion from a frame-cached disappearance. ACTIVE snapshots whose timer
            // reached zero use that same ending debounce because zero is a normal expiry boundary,
            // not malformed geometry. Only other sustained malformed ACTIVE data is abandoned here.
            if (!hasSnapshot || snapshot.Status != FateStatus.ACTIVE ||
                snapshot.TimeLeft <= TimeSpan.Zero ||
                !HasSnapshotTransitionSettled(ref trackedFateInvalidSinceUtc))
            {
                return;
            }

            var rejectedFateId = trackedFateId;
            RetireTrackedFateState($"FATE {rejectedFateId} remained incomplete after the snapshot grace period");
            participatedInTrackedFate = false;
            Log.Warning($"Abandoned FATE {rejectedFateId} because its active snapshot remained incomplete for {FateSnapshotTransitionGracePeriod.TotalSeconds:0.#} seconds.");
        }


        /// <summary>
        /// Requires complete, active FATE data before selection or travel; pointer validity alone is
        /// insufficient.
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
        /// Copies required native fields under a memory-read guard. A row can disappear between reads,
        /// so incomplete copies use the transition grace period.
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
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
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


        /// <summary>Applies LLFate's configured validity, progress, and blacklist rules.</summary>
        /// <param name="List">Active FATE snapshots to evaluate.</param>
        /// <returns>The eligible snapshots, with normal selection diagnostics written to the log.</returns>
        public List<FateData> MyFilter(List<FateData> List)
        {
            return FilterFateCandidates(List, logCandidates: true);
        }


        /// <summary>
        /// Filters candidates with optional diagnostics. Re-selection after combat stays quiet because
        /// the initial scan already reported these events.
        /// </summary>
        private List<FateData> FilterFateCandidates(List<FateData> candidates, bool logCandidates)
        {
            var eligibleFates = new List<FateData>();
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
                if ((hasExplicitMinLevel && snapshot.Level < minimumLevel) ||
                    (hasExplicitMaxLevel && snapshot.Level > maximumLevel))
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
                else if (!IsBlacklistedFate(snapshot.Name, snapshot.Id))
                {
                    eligibleFates.Add(f);

                    if (logCandidates)
                    {
                        Log.Information($"Adding FATE \"{snapshot.Name}\". Distance is {Core.Me.Distance(snapshot.Location)}.");
                    }
                }
            }

            return eligibleFates;
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
            return fateSettings.BlackListedFates.Contains(name) ||
                   (BlacklistIds ?? Array.Empty<int>()).Contains((int)id);
        }


        /// <summary>
        /// Prefers eligible FATEs containing the player, then distance, so local chain events are not
        /// abandoned for distant ones.
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
                    (!hasExplicitMinLevel || snapshot.Level >= minimumLevel) &&
                    (!hasExplicitMaxLevel || snapshot.Level <= maximumLevel) &&
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


        /// <summary>Selects a pending FATE after refreshing shared progress when requested.</summary>
        /// <returns>True when a candidate was selected; false when none qualifies or While has completed.</returns>
        public async Task<bool> GetFates()
        {
            if (ShouldStop())
            {
                pendingFate = null;
                return false;
            }

            if (SharedFate)
            {
                await LlamaLibrary.ScriptConditions.Extras.UpdateSharedFates();
                if (ShouldStop())
                {
                    pendingFate = null;
                    return false;
                }
            }

            pendingFate = SelectBestEligibleFate(logCandidates: true);
            return pendingFate != null;
        }


        /// <summary>
        /// Selects from focused IDs using containment, then distance. Level bounds apply only when
        /// explicitly supplied by the profile.
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
                    (hasExplicitMinLevel && snapshot.Level < minimumLevel) ||
                    (hasExplicitMaxLevel && snapshot.Level > maximumLevel) ||
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

        #endregion

        #region Travel and landing


        private async Task<bool> FlyToFateAndLand()
        {
            var fate = FateManager.GetFateById(trackedFateId);
            FateSnapshot selectedSnapshot;
            if (!TryReadFateSnapshot(fate, out selectedSnapshot) ||
                !IsUsableFateSnapshot(selectedSnapshot, trackedFateId, false))
            {
                return false;
            }

            // FateData is a live wrapper over a reusable client slot. Cache the validated target
            // before yielding so a slot refresh cannot redirect an in-progress flight elsewhere.
            var selectedFateId = selectedSnapshot.Id;

            bool AbortFlight()
            {
                if (isDone || recoveringFromDeath || CommonBehaviors.IsLoading || Core.Player == null || !Core.Player.IsValid || Core.Me.IsDead)
                {
                    return true;
                }
                var activeFate = FateManager.GetFateById(selectedFateId);
                return currentstep != 1 || trackedFateId != selectedFateId ||
                       WorldManager.ZoneId != startingZoneId ||
                       !IsUsableFate(activeFate, selectedFateId, false) ||
                       ShouldStop();
            }

            if (!await WaitForCombatBeforeFlight(selectedSnapshot))
            {
                return false;
            }

            var enemyLanding = await ApproachFateEnemy(selectedSnapshot, AbortFlight);
            if (enemyLanding != LandingApproachResult.UseTerrainFallback)
            {
                var landed = enemyLanding == LandingApproachResult.Landed;
                if (landed && trackedFateId == selectedFateId)
                {
                    hasArrivedAtFate = true;
                }
                return landed;
            }

            var fallbackLanded = await LandAtFateFallback(selectedSnapshot, AbortFlight);
            if (fallbackLanded && !MovementManager.IsFlying && trackedFateId == selectedFateId)
            {
                hasArrivedAtFate = true;
            }
            return fallbackLanded;
        }


        /// <summary>
        /// Keeps post-FATE combat local while waiting to mount. A closer event may replace the
        /// selected FATE once combat ends, before flight takes ownership of movement.
        /// </summary>
        private async Task<bool> WaitForCombatBeforeFlight(FateSnapshot fate)
        {
            var selectedFateId = fate.Id;
            var selectedFateName = fate.Name;
            var selectedFateLocation = fate.Location;
            var selectedFateRadius = fate.Radius;

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
                    currentstep != 1 || trackedFateId != selectedFateId ||
                    WorldManager.ZoneId != startingZoneId ||
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
                    isDone = true;
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

            return true;
        }


        /// <summary>
        /// Tries the center, then nearby terrain, when targeted landing is unavailable. Separate
        /// flight and landing results preserve the bounded retry budget for unsafe terrain.
        /// </summary>
        private async Task<bool> LandAtFateFallback(FateSnapshot fate, Func<bool> abort)
        {
            var selectedFateId = fate.Id;
            var selectedFateName = fate.Name;
            var selectedFateLocation = fate.Location;
            var selectedFateRadius = fate.Radius;

            // Fly and land are evaluated separately because FlyToAndLandAsync returns false for
            // cancellation, route failure, and landing failure alike. Unknown terrain results and
            // failed positive probes receive a small bounded retry budget before alternate terrain
            // is used; otherwise the tag can remain airborne until the entire FATE expires.
            if (rejectedCenterLandingFateId != selectedFateId)
            {
                var centerFlightResult = await CommonTasks.FlyToAsync(
                                                                          selectedFateLocation.Add(0f, FateLandingHeight, 0f),
                                                                          abortCondition: abort,
                                                                          destinationName: $"FlyToFate:{selectedFateName}");
                if (centerFlightResult != MoveResult.ReachedDestination || abort())
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
                if (abort())
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
                                                                               abortCondition: abort,
                                                                               destinationName: $"FateEnemyLanding:{selectedFateName}");
                    if (enemyResult)
                    {
                        ClearLandingRecoveryState();
                        return true;
                    }

                    if (abort())
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

            if (abort())
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
        /// Changes the flight destination once, while approaching the FATE. Keeping one landing
        /// point avoids chasing enemy rotations; independent targeting waits until landing finishes.
        /// </summary>
        private async Task<LandingApproachResult> ApproachFateEnemy(FateSnapshot fate, Func<bool> abort)
        {
            approachingEnemyLanding = true;
            try
            {
                var cruisePoint = fate.Location.Add(0f, FateLandingHeight, 0f);
                var nextScan = DateTime.MinValue;
                var scanSummary = "the approach never entered enemy scan range";
                uint enemyId = 0;
                Vector3 enemyLocation = Vector3.Zero;
                Vector3 landingPoint = Vector3.Zero;

                while (!abort() && !Core.Me.IsDead && !Core.Me.InCombat)
                {
                    // Scanning at four Hz keeps terrain queries bounded without pausing flight.
                    if (DateTime.UtcNow >= nextScan &&
                        Core.Me.Location.Distance2D(fate.Location) <= fate.Radius + EnemyLandingSearchDistance)
                    {
                        nextScan = DateTime.UtcNow.AddMilliseconds(250);
                        if (TryFindEnemyLanding(fate, out enemyId, out enemyLocation, out landingPoint, out scanSummary))
                        {
                            break;
                        }
                    }

                    var result = FlightorV2.MoveTo(cruisePoint);
                    if (result == MoveResult.ReachedDestination)
                    {
                        // Empty waves and unlandable fronts retain the established terrain fallback.
                        Log.Information($"FATE \"{fate.Name}\" reached its overhead waypoint without a targeted landing: {scanSummary}. Using terrain fallback.");
                        return LandingApproachResult.UseTerrainFallback;
                    }
                    if (result == MoveResult.Failed || result == MoveResult.Failure || result == MoveResult.PathGenerationFailed)
                    {
                        Log.Information($"FATE \"{fate.Name}\" approach returned {result}: {scanSummary}. Using terrain fallback.");
                        return LandingApproachResult.UseTerrainFallback;
                    }
                    await Coroutine.Yield();
                }

                if (enemyId == 0 || abort() || Core.Me.IsDead || Core.Me.InCombat)
                {
                    return LandingApproachResult.Interrupted;
                }

                var deadline = DateTime.UtcNow + EnemyLandingTimeout;
                bool StopLanding() => abort() || Core.Me.IsDead || DateTime.UtcNow >= deadline;
                bool StopApproach() => StopLanding() || Core.Me.InCombat ||
                                       !IsLandingEnemyCurrent(enemyId, fate.Id, enemyLocation);

                Log.Information($"Approaching a landing point in front of FATE enemy {enemyId}.");
                while (!StopApproach())
                {
                    // Keep one final destination so FlightorV2 only replans once. Its path request
                    // may pause movement; subsequent ticks retain its normal obstacle avoidance.
                    var result = FlightorV2.MoveTo(landingPoint, 1f);
                    if (result == MoveResult.ReachedDestination)
                    {
                        if (await CompleteEnemyLanding(enemyId, StopApproach, StopLanding))
                        {
                            return LandingApproachResult.Landed;
                        }
                        break;
                    }
                    if (result == MoveResult.Failed || result == MoveResult.Failure || result == MoveResult.PathGenerationFailed)
                    {
                        break;
                    }
                    await Coroutine.Yield();
                }

                if (abort() || Core.Me.IsDead || Core.Me.InCombat)
                {
                    return LandingApproachResult.Interrupted;
                }

                // A target can move, despawn, or become untargetable during the final descent.
                // Once grounded, none of those events justifies taking off for the center again.
                if (!MovementManager.IsFlying && !MovementManager.IsSwimming)
                {
                    ClearLandingRecoveryState();
                    return LandingApproachResult.Landed;
                }

                return LandingApproachResult.UseTerrainFallback;
            }
            finally
            {
                approachingEnemyLanding = false;
                // V2 owns a nested coroutine separate from Flightor. An already-reached
                // destination invokes its public completion path and disposes that coroutine.
                if (Core.Player != null && Core.Player.IsValid && Core.Player.Location != Vector3.Zero)
                {
                    FlightorV2.MoveTo(Core.Player.Location, 1f);
                }
                Flightor.Clear();
                Navigator.Stop();
                MovementManager.MoveStop();
            }
        }


        /// <summary>
        /// Lands and dismounts before publishing a combat POI. Landing eligibility deliberately
        /// does not require CanAttack; normal combat checks apply only at this final handoff.
        /// </summary>
        private async Task<bool> CompleteEnemyLanding(uint enemyId, Func<bool> stopApproach, Func<bool> stopLanding)
        {
            if (stopApproach() || (MovementManager.IsFlying && CommonTasks.CanLand() != CanLandResult.Yes))
            {
                return false;
            }

            if (MovementManager.IsFlying && !await CommonTasks.Land())
            {
                return false;
            }
            if (stopLanding() || MovementManager.IsFlying || MovementManager.IsSwimming)
            {
                return false;
            }

            // After touchdown, only tag cancellation, death, and the deadline can stop dismount.
            // The original mob is merely a combat preference now, not a landing requirement.
            while (Core.Me.IsMounted && !stopLanding())
            {
                ActionManager.Dismount();
                await Coroutine.Sleep(250);
            }
            if (!Core.Me.IsMounted && !stopLanding())
            {
                ClearLandingRecoveryState();
                var target = GameObjectManager.GetObjectByObjectId(enemyId) as BattleCharacter;
                if (IsEligibleTrackedFateCombatTarget(target))
                {
                    Poi.Current = new Poi(target, PoiType.Kill);
                }
                Log.Information("Targeted landing complete; remaining on the ground for FATE combat.");
                return true;
            }
            return false;
        }


        /// <summary>
        /// Finds a landable point in a loaded enemy's facing direction. Raycast the actual floor
        /// so sloped terrain does not turn an enemy's Y coordinate into an underground waypoint.
        /// </summary>
        private bool TryFindEnemyLanding(FateSnapshot fate, out uint enemyId, out Vector3 enemyLocation, out Vector3 point, out string summary)
        {
            enemyId = 0;
            enemyLocation = point = Vector3.Zero;
            summary = "a native actor read expired during the scan";
            try
            {
                // Summarize the last scan only when falling back. Per-pulse diagnostics would
                // flood the flight log and still obscure which gate actually rejected the actors.
                var loaded = GameObjectManager.GetObjectsOfType<BattleCharacter>()
                    .Where(enemy => enemy.IsValid && enemy.IsFate && enemy.FateId == fate.Id && !enemy.IsDead)
                    .ToArray();
                var enemies = loaded
                    // CanAttack may be false before level sync, which happens after arrival.
                    // Landing near a FATE actor does not authorize combat: POI publication and
                    // the targeting provider still require CanAttack after landing and dismount.
                    .Where(enemy => enemy.IsTargetable && enemy.IsVisible &&
                                    !IsLineOfSightSuppressed(enemy.ObjectId) && enemy.Distance() <= EnemyLandingSearchDistance)
                    .OrderBy(enemy => enemy.Distance())
                    .Take(4).ToArray();
                var invalidCoordinates = 0;
                var noFloor = 0;
                var outsideFate = 0;
                var unlandable = 0;
                var obstructed = 0;
                foreach (var enemy in enemies)
                {
                    var location = enemy.Location;
                    var heading = enemy.Heading;
                    if (!IsFinite(heading) || !IsFinite(location.X) || !IsFinite(location.Y) || !IsFinite(location.Z))
                    {
                        invalidCoordinates++;
                        continue;
                    }
                    // Large enemies need clearance beyond their combat reach, not a point inside
                    // their model. Ordinary enemies retain the five-yalm minimum stand-off.
                    var standOff = Math.Max(EnemyLandingStandOff, enemy.CombatReach + 2f);
                    // RB's heading convention is X = sin(heading), Z = cos(heading).
                    var front = location.Add((float)Math.Sin(heading) * standOff, 0f,
                                             (float)Math.Cos(heading) * standOff);
                    Vector3 ground;
                    // Only accept a floor within eight yalms of the actor's elevation. This avoids
                    // selecting a lower storey or a cliff floor from an otherwise clear vertical ray.
                    if (!WorldManager.AdvancedRaycast(front.Add(0f, 8f, 0f), front.Add(0f, -8f, 0f),
                                                     RaycastFlags.PassthruBoundaries, RaycastFlags.PassthruBoundaries, out ground))
                    {
                        noFloor++;
                        continue;
                    }
                    if (Vector3.Distance(ground, fate.Location) > fate.Radius)
                    {
                        outsideFate++;
                        continue;
                    }
                    var airbornePoint = ground.Add(0f, EnemyLandingHeight, 0f);
                    Vector3 obstruction;
                    if (CommonTasks.CanLand(airbornePoint) != CanLandResult.Yes)
                    {
                        unlandable++;
                        continue;
                    }
                    if (WorldManager.AdvancedRaycast(ground.Add(0f, 1f, 0f), location.Add(0f, 1f, 0f),
                                                     RaycastFlags.PassthruBoundaries, RaycastFlags.PassthruBoundaries, out obstruction))
                    {
                        obstructed++;
                        continue;
                    }
                    enemyId = enemy.ObjectId;
                    enemyLocation = location;
                    point = airbornePoint;
                    summary = $"selected {enemy.Name} ({enemyId}) at {point}";
                    return true;
                }
                var nearest = loaded.OrderBy(enemy => enemy.Distance()).FirstOrDefault();
                var nearestDetails = nearest == null ? "none" :
                    $"{nearest.Name} ({nearest.ObjectId}), distance={nearest.Distance():0.0}, attackable={nearest.CanAttack}, targetable={nearest.IsTargetable}, visible={nearest.IsVisible}";
                summary = $"loaded={loaded.Length}, probed={enemies.Length}, invalid={invalidCoordinates}, noFloor={noFloor}, outsideFate={outsideFate}, unlandable={unlandable}, obstructed={obstructed}; nearest={nearestDetails}";
            }
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
                // Despawns during selection are normal; retry on the next scan with fresh wrappers.
            }
            return false;
        }


        private static bool IsLandingEnemyCurrent(uint enemyId, uint fateId, Vector3 originalLocation)
        {
            try
            {
                var enemy = GameObjectManager.GetObjectByObjectId(enemyId) as BattleCharacter;
                return enemy != null && enemy.IsValid && enemy.IsFate && enemy.FateId == fateId &&
                       // Match approach selection: combat eligibility must not cancel an otherwise
                       // valid landing while the player is still airborne and not level-synced.
                       enemy.IsTargetable && enemy.IsVisible && !enemy.IsDead &&
                       Vector3.Distance(enemy.Location, originalLocation) <= EnemyLandingDriftTolerance;
            }
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
                return false;
            }
        }


        /// <summary>
        /// Probes nearby enemies, then the FATE center, for terrain RB confirms is landable. Enemy
        /// positions are copied before travel can yield.
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
        /// Starts a new landing attempt when the selected FATE changes; retries and the rejected-center
        /// flag belong to that attempt.
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
        /// Clears the attempt owner and retry counts so a later spawn of the same FATE starts with a
        /// full landing budget.
        /// </summary>
        private void ClearLandingRecoveryState()
        {
            landingRecoveryFateId = 0;
            rejectedCenterLandingFateId = 0;
            centerLandingAssessmentFailures = 0;
            landingPointScanFailures = 0;
        }


        /// <summary>
        /// Applies a cooldown after landing recovery fails. Stay airborne rather than descend onto
        /// unverified terrain.
        /// </summary>
        private void RejectFateAfterLandingFailure(uint selectedFateId, string selectedFateName)
        {
            if (currentstep != 1 || trackedFateId != selectedFateId)
            {
                return;
            }

            landingFailureCooldowns[selectedFateId] = DateTime.UtcNow + LandingFailureCooldown;
            ClearLandingRecoveryState();
            RetireTrackedFateState($"FATE {selectedFateId} exhausted safe landing recovery");
            Log.Warning($"Skipping FATE \"{selectedFateName}\" ({selectedFateId}) for {LandingFailureCooldown.TotalMinutes:0} minutes because no safe landing point was found after {MaximumLandingPointScans} bounded scans. A later spawn will be retried.");
        }


        /// <summary>
        /// Uses the shared navigation graph so ground travel can take advantage of aetherytes.
        /// GetTo does not accept a stop predicate: preserve routing and check While before and
        /// after the awaited route rather than abandoning a still-running navigation coroutine.
        /// </summary>
        /// <returns>The GetTo result, or <c>false</c> when While has completed or the FATE is no longer usable.</returns>
        private async Task<bool> GroundMoveToFate()
        {
            if (ShouldStop() || currentstep != 1 || WorldManager.ZoneId != startingZoneId || Core.Me.IsDead)
            {
                return false;
            }

            FateSnapshot snapshot;
            if (!TryReadFateSnapshot(FateManager.GetFateById(trackedFateId), out snapshot) ||
                !IsUsableFateSnapshot(snapshot, trackedFateId, false))
            {
                return false;
            }

            Position = snapshot.Location;
            fateRadius = snapshot.Radius;
            var reachedDestination = await Navigation.GetTo(startingZoneId, snapshot.Location);
            if (ShouldStop())
            {
                OnDoneWhile();
                return false;
            }

            return reachedDestination;
        }


        private async Task MoveToFocusedFate()
        {
            // Protection events still have combat waves. Select their enemies before issuing
            // downtime movement so the crowd destination cannot compete with a fresh Kill POI.
            if (TrySetFateCombatPoi())
            {
                if (clusterDestination.HasValue)
                {
                    Navigator.Stop();
                    MovementManager.MoveStop();
                }
                clusterDestination = null;
                return;
            }

            if (fateIcon == FateIconType.ProtectNPC || fateIcon == FateIconType.ProtectNPC2)
            {
                MoveWithinProtectionFate();
            }
            else
            {
                await ApproachFateTargetForLineOfSight();
            }
        }

        /// <summary>
        /// Follows the protection event's crowd without letting an old heading or an escort's
        /// moving boundary carry the player away. Only destination sampling is throttled.
        /// </summary>
        private void MoveWithinProtectionFate()
        {
            if (ShouldStop() || Core.Me.IsDead || MovementManager.IsFlying ||
                !TryReadFateSnapshot(FateManager.GetFateById(trackedFateId), out var fate) ||
                !IsUsableFateSnapshot(fate, trackedFateId, false))
            {
                clusterDestination = null;
                Navigator.Stop();
                MovementManager.MoveStop();
                return;
            }

            var interiorRadius = Math.Max(1f, fate.Radius - ClusterBoundaryInset);
            var outsideFate = Core.Player.Location.Distance(fate.Location) > fate.Radius;
            if (outsideFate || (clusterDestination.HasValue &&
                                clusterDestination.Value.Distance(fate.Location) > interiorRadius))
            {
                // An escort can move while we fight or await a route. Recover on the ground
                // toward its current center rather than rearming the initial flight approach.
                clusterDestination = fate.Location;
                clusterTimer.Restart();
            }
            else if (!clusterDestination.HasValue || clusterTimer.ElapsedMilliseconds >= 5000)
            {
                var positions = GameObjectManager.GetObjectsOfType<BattleCharacter>()
                    .Where(actor => actor.IsValid && !actor.IsDead && actor.IsVisible &&
                                    actor.ObjectId != Core.Player.ObjectId &&
                                    ((actor.IsFate && actor.FateId == trackedFateId && !actor.CanAttack) ||
                                     actor.Type == GameObjectType.Pc))
                    .Select(actor => actor.Location)
                    .Where(location => location.Distance(fate.Location) <= interiorRadius)
                    .ToArray();

                // Keep the average inside an inset of the live event. Exclude ourselves so an
                // empty wave cannot gradually pull its own destination toward a drifting player.
                clusterDestination = positions.Length == 0 ? fate.Location :
                    new Vector3(positions.Average(location => location.X),
                                positions.Average(location => location.Y),
                                positions.Average(location => location.Z));
                clusterTimer.Restart();
            }

            var destination = clusterDestination.Value;
            if (Core.Player.Location.Distance(destination) <= ClusterArrivalDistance)
            {
                Navigator.Stop();
                MovementManager.MoveStop();
                return;
            }

            // MoveTo is a per-tick navigation operation, not a fire-and-forget route request.
            // Continuing to pulse it also lets the navigator turn and stop at intermediate nodes.
            var result = Navigator.MoveTo(new MoveToParameters(destination, $"Following protection FATE {trackedFateId}")
            {
                DistanceTolerance = ClusterArrivalDistance,
                // These are short adjustments among event actors, not a new travel leg.
                UseMount = false
            });
            if (result == MoveResult.Failed || result == MoveResult.PathGenerationFailed)
            {
                Navigator.Stop();
                MovementManager.MoveStop();
                clusterDestination = fate.Location;
            }
        }


        /// <summary>
        /// Approaches an obstructed enemy before assigning a Kill POI. Bounded attempts and temporary
        /// suppression prevent repeated casts through geometry.
        /// </summary>
        private async Task ApproachFateTargetForLineOfSight()
        {
            var target = GetFateApproachTarget();
            if (target == null)
            {
                return;
            }

            var targetObjectId = target.ObjectId;
            var selectedFateId = trackedFateId;
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

        #endregion

        #region Combat and target safety


        /// <summary>
        /// Runs safety checks from PoiAction because OrderBot skips profile behavior during combat.
        /// Returning Failure leaves ordinary scheduling in control.
        /// </summary>
        /// <returns><see cref="RunStatus.Failure"/> so ordinary OrderBot processing continues.</returns>
        private RunStatus RunSafetyPulse()
        {
            // PoiAction also pulses through loading and death transitions. Live wrappers are not
            // safe to dereference until RB has republished a valid local player for the new frame.
            if (isDone || CommonBehaviors.IsLoading || Core.Player == null || !Core.Player.IsValid)
            {
                return RunStatus.Failure;
            }

            // A POI keeps the wrapper that existed when it was assigned. Reacquire it once from
            // the current object table and share that wrapper across this pulse so independent
            // validators cannot observe different generations of the same transient object ID.
            BattleCharacter liveCombatPoiTarget;
            TryGetLiveCombatPoiTarget(out liveCombatPoiTarget);

            CountDeath();
            if (recoveringFromDeath)
            {
                // Let OrderBot own the death POI and resurrection. Do not retire it while dead.
                // After revival, discard old FATE targets before any normal safety checks run.
                if (!Core.Me.IsDead && !deathStateRetired)
                {
                    RetireTrackedFateState("LLFate recovering after death");
                    participatedInTrackedFate = false;
                    huntedTargetObjectId = 0;
                    idleHuntAnchor = null;
                    ClearCombatRetirementState();
                    deathStateRetired = true;
                }
                return RunStatus.Failure;
            }
            UpdateHuntStatistics(liveCombatPoiTarget);
            UpdateDefensiveCombatState();
            EnforceZoneBoundary();
            ValidateCombatRetirementDeadline();
            if (isDone)
            {
                return RunStatus.Failure;
            }
            InterruptIdleHuntForEligibleFate();
            CheckTrackedFateCompletion();
            ValidateTrackedFate();
            UpdateFateData();
            ValidateLiveCombatPoi(liveCombatPoiTarget);
            return RunStatus.Failure;
        }


        /// <summary>
        /// Validates all combat leashes against the same current-frame actor, treating a native read
        /// failure as a despawn.
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
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
                // The actor can retire after current-frame reacquisition but before the final
                // native property read. Treat only GreyMagic's precise memory fault as a despawn.
                ClearStaleCombatPoi("Combat target despawned during LLFate safety validation.");
            }
        }


        private void TryStartIdleHunt()
        {
            if (!HuntBetweenFates || pendingFate != null || Core.Me.InCombat)
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
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
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
            if (!Core.Me.InCombat || trackedFateId != 0 || idleHuntAnchor.HasValue || postFateCombatLeashAnchor.HasValue)
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
            if (huntedTargetObjectId == 0 || pendingFate != null || ShouldStop())
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

            // Hand any unavoidable combat to the existing bounded retirement path before AdoptPendingFate
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
            pendingFate = eligibleFate;
            AdoptPendingFate();
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
            isDone = true;
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
        /// Releases a retreating attacker outside the post-FATE leash. Provider filtering alone cannot
        /// clear an already assigned Kill POI.
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
        /// Releases incidental attackers that leave the fixed defensive area, matching the targeting
        /// provider's admission rule.
        /// </summary>
        private void ValidateDefensiveCombatLeash(BattleCharacter target)
        {
            if (!defensiveCombatAnchor.HasValue || !Core.Me.InCombat || trackedFateId != 0 ||
                idleHuntAnchor.HasValue || postFateCombatLeashAnchor.HasValue)
            {
                return;
            }

            if (target == null ||
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
        /// Keeps both FATE enemies and incidental attackers inside the selected event so neither can
        /// drag combat away.
        /// </summary>
        private void ValidateCombatPoiFateLeash(BattleCharacter target)
        {
            if (target == null ||
                postFateCombatLeashAnchor.HasValue || trackedFateId == 0)
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
        /// Releases owned targets after sustained obstruction; the combat routine can otherwise continue
        /// acting on a POI rejected by the provider.
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
                var isActiveFateAttacker = trackedFateId != 0 && !isFateTarget && isAttacker;
                var isDefensiveAttacker = defensiveCombatAnchor.HasValue && isAttacker;
                var isOwnedTarget = (trackedFateId != 0 && isFateTarget && target.FateId == trackedFateId) ||
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
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
                // The object table can retire a FATE actor between reacquisition and the remaining
                // native reads in this pulse. GreyMagic exposes this exception as a private global
                // type in current RB builds, so it must be identified by its exact runtime name;
                // every other exception is rethrown instead of being hidden by this recovery path.
                ClearStaleCombatPoi("Combat target despawned during LLFate safety validation.");
            }
        }


        /// <summary>
        /// Reacquires the POI actor from the current object table because retained wrappers may refer to
        /// despawned or reused slots.
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
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
                // ReadWriteMemoryException is private in GreyMagic.dll and therefore cannot be
                // referenced in source. Matching its exact runtime name keeps this guard narrow.
                ClearStaleCombatPoi("LLFate Kill POI wrapper expired before it could be reacquired.");
                target = null;
                return false;
            }
        }


        /// <summary>
        /// Stops movement and clears targeting for a despawned actor. Normal despawns need only the
        /// standard POI diagnostic.
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
        /// Clears an obstructed target and applies a cooldown shared by POI validation and pre-combat
        /// approach.
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
        /// Requires a clear combat ray and no active obstruction cooldown. Visibility alone does not
        /// establish line of sight.
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
        /// Enforces the active combat boundaries before independent targeting pulses can reacquire a
        /// rejected actor.
        /// </summary>
        /// <param name="unit">The unit CombatTargeting is considering.</param>
        /// <returns><c>true</c> when the unit remains inside every active leash.</returns>
        private bool IsInsideActiveTargetingLeashes(BattleCharacter unit)
        {
            if (recoveringFromDeath && !Core.Me.InCombat)
            {
                return false;
            }
            if (approachingEnemyLanding && !Core.Me.InCombat)
            {
                return false;
            }
            if (!IsLineOfSightCandidate(unit))
            {
                return false;
            }

            // Event selection limits proactive combat, not self-defense. An idle attacker may
            // still carry a retired or unselected FateId; admit it within a fixed local leash.
            if (trackedFateId == 0 && !idleHuntAnchor.HasValue &&
                (unit.IsFate || !IsIdleHuntTargetAbandoned(unit.ObjectId)) &&
                GameObjectManager.Attackers.Any(attacker => attacker.ObjectId == unit.ObjectId))
            {
                if (postFateCombatLeashAnchor.HasValue)
                {
                    return Vector3.Distance(unit.Location, postFateCombatLeashAnchor.Value) <= PostFateCombatLeash;
                }
                if (!defensiveCombatAnchor.HasValue)
                {
                    defensiveCombatAnchor = Core.Player.Location;
                }
                return Vector3.Distance(unit.Location, defensiveCombatAnchor.Value) <= DefensiveCombatLeash;
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

            if (!postFateCombatLeashAnchor.HasValue && trackedFateId != 0 &&
                (unit.IsFate
                    ? !IsInsideTrackedFateTargetBoundary(unit)
                    : !IsInsideTrackedFateGeometry(unit)))
            {
                // Outside the short defensive handoff, every combat target must remain inside the
                // selected event. FATE actors additionally require the matching FateId; ordinary
                // attackers use the same geometry so they cannot drag combat across the zone.
                return false;
            }

            if (!postFateCombatLeashAnchor.HasValue && trackedFateId == 0 && unit.IsFate)
            {
                // An active event must be selected through GetFates before its actors are eligible.
                // This prevents overlapping or policy-rejected FATEs from bypassing selection while idle.
                return false;
            }

            if (!unit.IsFate && idleHuntAnchor.HasValue && !IsInsideIdleHuntLeash(unit))
            {
                return false;
            }

            if (!unit.IsFate && trackedFateId == 0 && !postFateCombatLeashAnchor.HasValue &&
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
        /// Applies the selected event's live geometry to every FATE combat path. The small tolerance
        /// absorbs actors fighting on the boundary without restoring the old unbounded chase.
        /// Cached geometry is intentionally used during brief FateData refresh gaps.
        /// </summary>
        private bool IsInsideTrackedFateTargetBoundary(BattleCharacter unit)
        {
            return unit != null && unit.IsFate && trackedFateId != 0 && unit.FateId == trackedFateId &&
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
            return unit != null && trackedFateId != 0 && Position != Vector3.Zero && fateRadius > 0 &&
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


        /// <summary>
        /// Expires suppression lazily so abandoned actors cannot be reacquired immediately or block a
        /// recycled object ID forever.
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
        /// Clears both movement systems, the POI, and the player target before handing control back to
        /// the profile.
        /// </summary>
        /// <param name="reason">Diagnostic reason recorded with the POI cleanup.</param>
        private static void ReleaseOwnedControl(string reason)
        {
            Flightor.Clear();
            Navigator.Stop();
            MovementManager.MoveStop();
            Poi.Clear(reason);
            // Vertical idle movement is owned separately from Navigator and must not survive
            // timeout, death retirement, or a profile handoff.
            MovementManager.StopAscending();
            MovementManager.StopDescending();
            // OnDone can run during a loading transition where RB has temporarily released the
            // local player wrapper. Movement cleanup is still required, but target cleanup must
            // not turn a normal profile stop into a null-reference failure.
            if (Core.Player != null)
            {
                Core.Player.ClearTarget();
            }
        }


        /// <summary>
        /// Keeps a valid combat target or selects a new one. Waits quietly through empty waves rather
        /// than publishing a null Kill POI.
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
                                          (target.NpcId == ForlornMaidenNpcId || target.NpcId == ForlornNpcId);
            var existingTargetHasPriority = existingTarget != null &&
                                            (existingTarget.NpcId == ForlornMaidenNpcId || existingTarget.NpcId == ForlornNpcId);
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
        /// Selects a visible, attackable enemy with line of sight from the tracked FATE. Matching the
        /// event ID excludes overlapping FATEs.
        /// </summary>
        /// <returns>A valid target candidate, or <c>null</c> when the FATE has no active target.</returns>
        public GameObject GetFateTargets()
        {
            if (trackedFateId == 0)
            {
                return null;
            }

            var target = GameObjectManager.GameObjects
                .Select(unit => new { unit, bc = unit as BattleCharacter })
                .Where(x => IsEligibleTrackedFateCombatTarget(x.bc))
                .OrderByDescending(x => x.bc.NpcId == ForlornMaidenNpcId || x.bc.NpcId == ForlornNpcId)
                .ThenBy(x => x.unit.Distance(Core.Player.Location))
                .Select(x => x.unit)
                .FirstOrDefault();

            // CombatTargeting calls this provider repeatedly, including while no FATE is
            // active. Do not log each query; selection and completion transitions already
            // provide the useful user-facing diagnostics without idle-period log spam.
            return target;
        }


        /// <summary>
        /// Shares eligibility between POI selection and retention so one path cannot immediately reject
        /// the other's target.
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
        /// Finds an obstructed enemy for a bounded ground approach. The caller acquires line of sight
        /// before publishing a combat POI.
        /// </summary>
        /// <returns>An approach candidate, or <c>null</c> when no unsuppressed actor is available.</returns>
        private BattleCharacter GetFateApproachTarget()
        {
            if (trackedFateId == 0)
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
                .OrderByDescending(unit => unit.NpcId == ForlornMaidenNpcId || unit.NpcId == ForlornNpcId)
                .ThenBy(unit => unit.Distance(Core.Player.Location))
                .FirstOrDefault();
        }


        /// <summary>
        /// Selects a nearby non-FATE enemy for optional hunting. Distance and health limits keep
        /// downtime hunting local and conservative.
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

        #endregion

        #region Death recovery


        private void CountDeath()
        {
            var isDead = Core.Me.IsDead;
            if (isDead && !wasDead)
            {
                deathCount++;
                recoveringFromDeath = true;
                deathStateRetired = false;
            }

            wasDead = isDead;
        }


        /// <summary>
        /// Returns from the home point after OrderBot revives the player. Use the original tag
        /// location, not the dead FATE's possibly expired destination, and leave ordinary zone
        /// changes subject to the navigation guard. Ground routing retains aetheryte support.
        /// </summary>
        private async Task RecoverAfterDeath()
        {
            if (CommonBehaviors.IsLoading || Core.Player == null || !Core.Player.IsValid || Core.Me.IsDead)
            {
                await Coroutine.Yield();
                return;
            }

            if (WorldManager.ZoneId != startingZoneId)
            {
                Log.Information($"Returning to LLFate zone {startingZoneId} after revival.");
                await Navigation.GetTo(startingZoneId, startingLocation);
                if (CommonBehaviors.IsLoading || Core.Me.IsDead || WorldManager.ZoneId != startingZoneId)
                {
                    // A temporarily unavailable route must not turn death recovery into a bot stop
                    // or a tight stream of teleport attempts. Retry on the next recovery pass.
                    await Coroutine.Sleep(5000);
                    return;
                }
            }

            recoveringFromDeath = false;
            deathStateRetired = false;
            idleStartedAt = DateTime.Now;
            Log.Information("Death recovery complete; resuming LLFate selection.");
        }

        #endregion
    }

    // Keep targeting beside LLFate so its independent pulse rules can be maintained with the tag.
    /// <summary>
    /// Ranks FATE enemies and nearby attackers for LLFate. The owner supplies live combat
    /// boundaries because RebornBuddy pulses targeting independently of the profile behavior.
    /// </summary>
    public class FateTargetingProvider : ITargetingProvider
    {
        // Standalone legacy callers do not provide LLFate's live radius callback. Preserve their
        // historical 50-yalm behavior while the tag-owned provider delegates to the selected
        // event's real radius and tolerance through admissionConstraint.
        private const float LegacyFateTargetRadius = 50f;

        /// <summary>NPC IDs excluded from targeting. The legacy exclusion is retained for existing profiles.</summary>
        public HashSet<uint> IgnoreNpcIds = new HashSet<uint>()
        {
            1201
        };

        private BattleCharacter[] attackers;
        private uint currentKillPoiObjectId;
        private readonly Func<BattleCharacter, bool> admissionConstraint;

        /// <summary>
        /// Creates LLFate's targeting provider with an optional owner-supplied admission policy.
        /// The callback lets runtime leashes constrain RebornBuddy's independent targeting pulse
        /// instead of relying on POI cleanup after an unsafe unit was already admitted.
        /// </summary>
        /// <param name="admissionConstraint">A live unit predicate, or <c>null</c> for legacy admission behavior.</param>
        public FateTargetingProvider(Func<BattleCharacter, bool> admissionConstraint = null)
        {
            this.admissionConstraint = admissionConstraint;
        }

        /// <summary>Ranks eligible actors, prioritizing Forlorn bonuses and then the current combat POI.</summary>
        /// <returns>Eligible actors in descending combat priority.</returns>
        public List<BattleCharacter> GetObjectsByWeight()
        {
            // CombatTargeting can pulse before LLFate's PoiAction safety hook. Resolve the retained
            // POI once from the current object table so ranking and idle-hunt admission never read
            // combat fields from the stale wrapper stored when the POI was originally assigned.
            currentKillPoiObjectId = ResolveCurrentKillPoiObjectId();
            var allUnits = GameObjectManager.GetObjectsOfType<BattleCharacter>().ToArray();

            attackers = GameObjectManager.Attackers.ToArray();
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
                .OrderByDescending(s => s.Unit.NpcId == LLFate.ForlornMaidenNpcId || s.Unit.NpcId == LLFate.ForlornNpcId)
                .ThenByDescending(s => currentKillPoiObjectId != 0 && s.Unit.ObjectId == currentKillPoiObjectId)
                .ThenByDescending(s => s.Weight)
                .Select(s => s.Unit)
                .ToList();
        }

        private bool IsValidUnit(bool inCombat, BattleCharacter unit)
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

            if (Blacklist.Contains(unit.ObjectId, BlacklistFlags.Combat))
            {
                return false;
            }

            // A lingering FATE actor can still attack after its event disappears. Tag-owned
            // admission applies the defensive leash before allowing that exceptional target.
            if (unit.IsFateGone && !(admissionConstraint != null && attackers.Contains(unit)))
            {
                return false;
            }

            // The custom provider historically admitted only FATE units and attackers. Explicitly
            // retaining the tag's kill POI is what lets optional downtime hunting enter combat.
            var isSelectedIdleHuntTarget = currentKillPoiObjectId != 0 &&
                                           currentKillPoiObjectId == unit.ObjectId &&
                                           !unit.IsFate;

            return attackers.Contains(unit) ||
                   (unit.CanAttack && !inCombat &&
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
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
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
            catch (Exception exception) when (exception.GetType().Name == "ReadWriteMemoryException")
            {
                Poi.Clear("LLFate targeting provider retired an expired Kill POI wrapper.");
                return 0;
            }
        }

        // Preserve the original scoring weights: favor bosses, nearby attackers, and bonus enemies.
        // POI retention is ordered separately so these weights cannot cause target oscillation.
        private double GetScoreForUnit(BattleCharacter unit)
        {
            double weight = 200 - (2 * unit.Distance());

            weight += unit.MaxHealth;

            if (unit.ObjectId == Core.Player.CurrentTargetId)
            {
                // Keep the player's selected unit stable instead of comparing what two unrelated
                // actors happen to be targeting.
                weight += 120;
            }

            if (attackers.Contains(unit))
            {
                weight += 100;
            }

            if (unit.NpcId == LLFate.ForlornMaidenNpcId || unit.NpcId == LLFate.ForlornNpcId)
            {
                weight += 100000;
            }

            // Finish weakened attackers to reduce incoming damage.
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

    /// <summary>
    /// Preserves the original provider type name for existing profiles and compiled callers.
    /// New code should use <see cref="FateTargetingProvider"/>.
    /// </summary>
    public class MySuperAwesomeTargetingProvider : FateTargetingProvider
    {
        /// <summary>Creates a provider with the historical constructor contract.</summary>
        /// <param name="admissionConstraint">Owner-supplied target filter, or null for legacy radius checks.</param>
        public MySuperAwesomeTargetingProvider(Func<BattleCharacter, bool> admissionConstraint = null)
            : base(admissionConstraint)
        {
        }
    }
}
