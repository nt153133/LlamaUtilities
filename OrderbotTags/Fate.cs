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
        private uint lastFateId = 0;

        private DateTime saveNow = DateTime.Now;
        public override bool IsDone => _done;

        //some Statistics
        private FateData currentfate;
        private FateData fateImIn;

        private int fatesDone;
        private int mobsHunted;
        private int died;
        private bool wasDead;
        private bool sessionSummaryReported;
        private uint huntedTargetObjectId;
        private Vector3? idleHuntAnchor;
        private Vector3? postFateCombatLeashAnchor;
        private readonly HashSet<uint> abandonedIdleHuntTargetIds = new HashSet<uint>();
        private uint llFateZoneId;
        private bool zoneSafetyTriggered;
        private bool participatedInTrackedFate;
        private uint rejectedCenterLandingFateId;
        private readonly HashSet<uint> rejectedFateIds = new HashSet<uint>();

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
                                        // Session tracking must run before the stop condition. Reward-based while
                                        // conditions commonly turn false on the same pulse that the FATE completes.
                                        new Sequence(new Action(r => CountDeath()),
                                                     new Action(r => UpdateHuntStatistics()),
                                                     new Action(r => EnforceZoneBoundary()),
                                                     new Action(r => IsFateStillActive()),
                                                     new Action(r => ValidateTrackedFate()),
                                                     new Action(r => UpdateFateData()),
                                                     new ActionAlwaysFail() //always fail that the rest of the tree is traveresd
                                                    ),
                                        new Decorator(ret => ShouldStop(),
                                                      new Action(r => OnDoneWhile())),
                                        new Decorator(ret => DateTime.Now > saveNow + TimeSpan.FromSeconds(_timeout) && currentstep == 0,
                                                      new Action(r => OnTimeout())),

                                        // Start fighting Fate Mobs but only when we are in close range to the fate position.
                                        new Decorator(r => currentfate != null && FateManager.WithinFate && Core.Me.ElementalLevel > 0 && currentfate.MaxLevel < Core.Me.ElementalLevel,
                                                      new ActionRunCoroutine(async r =>
                                                      {
                                                          Log.Information("Applying Eureka Level Sync.");

                                                          ToDoList.LevelSync();

                                                          await Coroutine.Sleep(500);

                                                          return false;
                                                      })),
                                        // State-only diagnostics do not belong in this per-pulse path: log only
                                        // the level-sync action below so ordinary FATE participation stays quiet.
                                        new Decorator(r => currentfate != null && FateManager.WithinFate && currentfate.MaxLevel < Core.Player.ClassLevel && !Core.Me.IsLevelSynced,
                                                      new ActionRunCoroutine(async r =>
                                                      {
                                                          Log.Information("Applying Level Sync.");

                                                          ToDoList.LevelSync();

                                                          await Coroutine.Sleep(500);

                                                          return false;
                                                      })),
                                        new Decorator(ret => !ShouldStop() && currentstep == 1 &&
                                                             (Vector3.Distance(Core.Player.Location, Position) > (fateRadius - 10) ||
                                                              (MovementManager.IsFlying && Poi.Current?.Type != PoiType.Kill)),
                                                      new PrioritySelector(
                                                                           new Decorator(ret => UseFlight && AetherCurrentManager.FinishedZones.Contains(WorldManager.ZoneId),
                                                                                         new ActionRunCoroutine(obj => FlyToFateAndLand(() => currentfate))),
                                                                           // LLFate is already bounded to its starting zone, so ground travel must
                                                                           // remain pulse-driven to recheck While instead of entering GetTo's
                                                                           // uninterruptible navigation-graph coroutine.
                                                                           new Decorator(ret => WorldManager.ZoneId == llFateZoneId &&
                                                                                                IsUsableFate(FateManager.GetFateById(fateid), fateid, false),
                                                                                         new ActionRunCoroutine(obj => GroundMoveToFate())))),
                                        //Find fates
                                        new Decorator(r => currentfate != null && fateid != 0 && Poi.Current.Type != PoiType.Kill,
                                                      new ActionRunCoroutine(r =>
                                                      {
                                                          MoveToFocusedFate();
                                                          return Task.CompletedTask;
                                                      })),
                                        new Decorator(ret => currentfate == null && currentstep == 0,
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

        private async Task<bool> FlyToFateAndLand(Func<FateData> getCurrentFate)
        {
            var fate = getCurrentFate();

            if (!IsUsableFate(fate, fateid, false))
                return false;

            // FateData is a live wrapper over a reusable client slot. Cache the validated target
            // before yielding so a slot refresh cannot redirect an in-progress flight elsewhere.
            var selectedFateId = fate.Id;
            var selectedFateName = fate.Name;
            var selectedFateLocation = fate.Location;
            var selectedFateRadius = fate.Radius;

            bool AbortFlight()
            {
                var activeFate = FateManager.GetFateById(selectedFateId);
                return WorldManager.ZoneId != llFateZoneId ||
                       !IsUsableFate(activeFate, selectedFateId, false) ||
                       ShouldStop();
            }

            // Flight cannot begin while combat is still active. Anchor this wait so a retreating
            // enemy from the previous FATE cannot drag the combat routine across the zone while
            // LLFate is waiting to mount for the newly selected event.
            var combatWaitAnchor = Core.Player.Location;
            var combatWaitAnnounced = false;
            var combatWaitMovementCleared = false;
            while (Core.Me.InCombat && !Core.Me.IsDead)
            {
                if (ShouldStop() ||
                    WorldManager.ZoneId != llFateZoneId ||
                    !IsUsableFate(FateManager.GetFateById(selectedFateId), selectedFateId, false))
                {
                    Navigator.Stop();
                    MovementManager.MoveStop();
                    return false;
                }

                if (!combatWaitAnnounced)
                {
                    Log.Information("Waiting to leave combat before flying...");
                    combatWaitAnnounced = true;
                }

                var target = GameObjectManager.Attackers
                    .Where(a => a.IsValid &&
                                !a.IsDead &&
                                Vector3.Distance(a.Location, combatWaitAnchor) <= PostFateCombatLeash)
                    .OrderBy(a => a.Distance())
                    .FirstOrDefault();
                if (target != null)
                {
                    var existingTarget = Poi.Current?.BattleCharacter;
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

            // Try the inexpensive historical destination once per FATE. If its exact centroid is
            // over water, a prop, or other non-dismountable collision, remember that rejection so
            // later pulses rescan nearby enemies without repeating the same failed landing/log.
            if (rejectedCenterLandingFateId != selectedFateId)
            {
                var centerResult = await CommonTasks.FlyToAndLandAsync(
                                                                            selectedFateLocation.Add(0f, FateLandingHeight, 0f),
                                                                            abortCondition: AbortFlight,
                                                                            destinationName: $"FlyToFateAndLand:{selectedFateName}");
                if (centerResult)
                {
                    return true;
                }

                if (AbortFlight())
                {
                    return false;
                }

                rejectedCenterLandingFateId = selectedFateId;
                Log.Information($"The center of FATE \"{selectedFateName}\" is not landable; looking for a nearby FATE enemy on valid terrain.");
            }

            var scanDeadline = DateTime.UtcNow.AddMilliseconds(FateEnemyLandingScanMilliseconds);
            do
            {
                if (AbortFlight())
                {
                    return false;
                }

                var enemyLandingPoint = FindFateEnemyLandingPoint(
                                                                       selectedFateId,
                                                                       selectedFateLocation,
                                                                       selectedFateRadius);
                if (enemyLandingPoint.HasValue)
                {
                    Log.Information($"Using a validated enemy position to land for FATE \"{selectedFateName}\".");
                    var enemyResult = await CommonTasks.FlyToAndLandAsync(
                                                                               enemyLandingPoint.Value,
                                                                               abortCondition: AbortFlight,
                                                                               destinationName: $"FateEnemyLanding:{selectedFateName}");
                    if (enemyResult)
                    {
                        rejectedCenterLandingFateId = 0;
                    }

                    return enemyResult;
                }

                await Coroutine.Sleep(250);
            }
            while (DateTime.UtcNow < scanDeadline);

            return false;
        }

        /// <summary>
        /// Finds an airborne landing point directly above a live enemy in the selected FATE. The
        /// enemy location is snapshotted before any coroutine yields, and RebornBuddy's native
        /// landing query rejects flying actors, water, props, and other non-dismountable geometry.
        /// </summary>
        /// <param name="selectedFateId">Stable ID of the FATE that owns the recovery attempt.</param>
        /// <param name="selectedFateLocation">Snapshotted event center used to enforce its radius.</param>
        /// <param name="selectedFateRadius">Snapshotted event radius.</param>
        /// <returns>A validated flight position, or <c>null</c> until a suitable enemy is loaded.</returns>
        private static Vector3? FindFateEnemyLandingPoint(
                                                               uint selectedFateId,
                                                               Vector3 selectedFateLocation,
                                                               float selectedFateRadius)
        {
            var candidates = GameObjectManager.GetObjectsOfType<BattleCharacter>()
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
                .ToArray();

            foreach (var groundLocation in candidates)
            {
                var flightLocation = groundLocation.Add(0f, FateLandingHeight, 0f);
                if (CommonTasks.CanLand(flightLocation) == CanLandResult.Yes)
                {
                    return flightLocation;
                }
            }

            return null;
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
            while (!ShouldStop() && WorldManager.ZoneId == llFateZoneId && !Core.Me.IsDead)
            {
                var fate = FateManager.GetFateById(selectedFateId);
                if (!IsUsableFate(fate, selectedFateId, false))
                {
                    break;
                }

                Position = fate.Location;
                fateRadius = fate.Radius;
                if (Vector3.Distance(Core.Player.Location, Position) <= fateRadius - 10)
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
                    if (ShouldStop())
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
        private Task MoveToFocusedFate()
        {
            Vector3 currentMove;
            if (currentfate.Icon == FateIconType.ProtectNPC || currentfate.Icon == FateIconType.ProtectNPC2)
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
                                   ((bc.IsFate && bc.FateId == currentfate.Id && !bc.CanAttack) || bc.Type == GameObjectType.Pc) &&
                                   bc.Location.Distance(currentfate.Location) < currentfate.Radius)
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
                TrySetFateCombatPoi();
            }

            return Task.CompletedTask;
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
                participatedInTrackedFate = false;
                var idleHuntTarget = Poi.Current?.BattleCharacter;
                if (huntedTargetObjectId != 0 && idleHuntTarget?.ObjectId == huntedTargetObjectId)
                {
                    // A newly eligible FATE always outranks optional downtime hunting.
                    Poi.Clear("Stopping idle hunt for an eligible FATE.");
                    huntedTargetObjectId = 0;
                }
                idleHuntAnchor = null;

                Log.Information($"Fate Details: {currentfate}");
                Position = currentfate.Location;
                fateRadius = currentfate.Radius;
                fateid = currentfate.Id;
                fateName = currentfate.Name;
                currentstep = 1;
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
            if (refreshedFate == null || refreshedFate.Id == selectedFateId)
            {
                return false;
            }

            var selectedDistance = Vector3.Distance(Core.Player.Location, selectedFateLocation);
            var refreshedDistance = Core.Me.Distance(refreshedFate.Location);
            var playerInsideSelected = selectedDistance <= selectedFateRadius;
            var playerInsideRefreshed = refreshedDistance <= refreshedFate.Radius;
            if ((!playerInsideRefreshed || playerInsideSelected) && refreshedDistance >= selectedDistance)
            {
                return false;
            }

            var refreshedName = refreshedFate.Name;
            Log.Information(
                $"A closer FATE appeared during the combat wait; switching from \"{selectedFateName}\" to \"{refreshedName}\".");
            Poi.Clear("Switching to a closer FATE that appeared during combat wait");
            Navigator.Stop();
            MovementManager.MoveStop();
            currentfate = refreshedFate;
            rejectedCenterLandingFateId = 0;
            GoFate();
            return currentfate != null && fateid == refreshedFate.Id;
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

        private void UpdateHuntStatistics()
        {
            if (huntedTargetObjectId == 0)
            {
                return;
            }

            var target = GameObjectManager.GetObjectByObjectId(huntedTargetObjectId) as BattleCharacter;
            if (target != null && target.IsValid && !target.IsDead && !IsInsideIdleHuntLeash(target))
            {
                var abandonedTargetId = huntedTargetObjectId;
                abandonedIdleHuntTargetIds.Add(abandonedTargetId);
                if (Poi.Current?.Type == PoiType.Kill && Poi.Current.BattleCharacter?.ObjectId == abandonedTargetId)
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
                idleHuntAnchor = null;
                return;
            }

            if (target != null && target.IsValid && target.IsDead)
            {
                mobsHunted++;
                Log.Information($"Completed idle hunt. Session hunt total: {mobsHunted}.");
                huntedTargetObjectId = 0;
                idleHuntAnchor = null;
            }
            else if (target == null && !Core.Me.InCombat)
            {
                // A missing object is not enough to claim a kill: it may have phased or left object range.
                huntedTargetObjectId = 0;
                idleHuntAnchor = null;
            }
        }

        private bool IsInsideIdleHuntLeash(BattleCharacter unit)
        {
            return !idleHuntAnchor.HasValue ||
                   Vector3.Distance(unit.Location, idleHuntAnchor.Value) <= HuntRadius;
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
            if (postFateCombatLeashAnchor.HasValue)
            {
                if (!Core.Me.InCombat)
                {
                    postFateCombatLeashAnchor = null;
                }
                else if (Vector3.Distance(unit.Location, postFateCombatLeashAnchor.Value) > PostFateCombatLeash)
                {
                    return false;
                }
            }

            return huntedTargetObjectId == 0 || unit.IsFate || IsInsideIdleHuntLeash(unit);
        }

        private void SetLastFate()
        {
            lastFateId = currentfate.Id;
            Log.Information($"Setting last Fate to {currentfate.Name}.");
        }

        private void UpdateFateData()
        {
            foreach (var item in FateManager.ActiveFates)
            {
                if (item.Id == fateid)
                {
                    Position = item.Location;
                    fateName = item.Name;
                    fateStatus = item.Status.ToString();
                    // RB commonly removes the FateData wrapper before exposing COMPLETE. Record
                    // that the player entered this exact event so its disappearance can still be
                    // included in the session report instead of silently losing the count.
                    if (FateManager.WithinFate && Core.Me.Distance(item.Location) <= item.Radius)
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
            if (trackedFate != null && trackedFate.Status == FateStatus.ACTIVE)
            {
                return;
            }

            var endedFateId = fateid;
            var endedFateName = !string.IsNullOrWhiteSpace(fateName) ? fateName : currentfate?.Name ?? $"FATE {endedFateId}";
            var completionStatusObserved = trackedFate != null && trackedFate.Status == FateStatus.COMPLETE;
            var finishedAfterParticipation = completionStatusObserved || participatedInTrackedFate;

            if (currentfate != null)
            {
                SetLastFate();
            }

            // Retiring the FATE must also retire every movement owner. When no replacement FATE
            // was available, the prior navigator/Flightor command otherwise kept driving in a
            // straight line indefinitely because the disabled idle-hunt path issued no command
            // to supersede it. Stop both navigation layers, release any FATE combat POI, and
            // invalidate the cached destination before the same pulse searches for new work.
            // Keep the combat anchor active in the provider before clearing POI. RB may pulse
            // CombatTargeting later in this scheduling pass and must not reacquire distant attackers.
            postFateCombatLeashAnchor = Core.Player.Location;
            Navigator.Stop();
            Flightor.Clear();
            MovementManager.MoveStop();
            Poi.Clear($"FATE {endedFateId} ended; clearing stale movement");
            Position = Vector3.Zero;
            currentstep = 0;
            fateid = 0;
            fateRadius = 0;
            currentfate = null;

            if (finishedAfterParticipation)
            {
                fatesDone++;
                var evidence = completionStatusObserved ? "completion status observed" : "participated through event end";
                Log.Information($"Finished FATE \"{endedFateName}\" ({endedFateId}; {evidence}). Session total: {fatesDone}.");
            }
            else
            {
                Log.Information($"FATE \"{endedFateName}\" ({endedFateId}) ended before LLFate observed participation or a completion status.");
            }

            participatedInTrackedFate = false;
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
            if (IsUsableFate(trackedFate, fateid, true))
            {
                currentfate = trackedFate;
                Position = trackedFate.Location;
                return;
            }

            Navigator.Stop();
            Poi.Clear("Rejected incomplete FATE snapshot");
            currentstep = 0;
            fateid = 0;
            fateRadius = 0;
            currentfate = null;
            Position = Vector3.Zero;
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
            if (fate == null)
            {
                return false;
            }

            var id = fate.Id;
            var name = fate.Name;
            var location = fate.Location;
            var radius = fate.Radius;
            var level = fate.Level;
            var maxLevel = fate.MaxLevel;
            var status = fate.Status;
            var timeLeft = fate.TimeLeft;

            // TODO: Revisit KillHandIn support when LLFate has a reliable way to identify the
            // event-specific key item, select the correct receiver, and recover every dialog state.
            // Until then, rejecting the icon here protects open, focused-ID, refresh, and tracked
            // FATE paths from entering the old unsafe hand-in behavior.
            if (fate.Icon == FateIconType.KillHandIn)
            {
                if (logRejected && rejectedFateIds.Add(id))
                {
                    Log.Information($"Skipping unsupported hand-in FATE \"{name}\" ({id}).");
                }

                return false;
            }

            var usable = fate.IsValid &&
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

            if (currentfate.Icon == FateIconType.ProtectNPC ||
                currentfate.Icon == FateIconType.ProtectNPC2)
            {
                var npc = GameObjectManager
                    .GetObjectsOfType<BattleCharacter>()
                    .FirstOrDefault(b => b.IsFate && !b.CanAttack && b.FateId == currentfate.Id);
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

        [Obsolete]
        private void OnTimeout()
        {
            _done = true;
            Log.Information("Timeout we are done for now.");
            Log.Information("Moving to the nearest Aetheryte.");
            var destination = WorldManager.AetheryteIdsForZone(WorldManager.ZoneId)
                .Select(a => a.Item2)
                .OrderBy(a => Core.Me.Distance(a))
                .FirstOrDefault();
            Navigator.MoveToPointWithin(destination, 30);
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
            Navigator.Stop();
            Poi.Clear("LLFate While condition completed");
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
            Log.Information($"FATEs finished after observed participation: {fatesDone}.");
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
            var target = GetFateTargets();
            if (target == null || !target.IsValid)
            {
                return false;
            }

            Poi.Current = new Poi(target, PoiType.Kill);
            return true;
        }

        /// <summary>
        /// Finds the nearest visible, attackable actor belonging to the currently tracked FATE.
        /// Restricting by the selected ID prevents overlapping events from donating an unrelated
        /// target while LLFate is waiting for the selected event's next wave.
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
                .Where(x =>
                           x.bc != null &&
                           x.unit.CanAttack &&
                           x.unit.IsTargetable &&
                           x.unit.IsVisible &&
                           x.bc.FateId == fateid &&
                           !x.bc.IsDead)
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
                               !unit.IsDead &&
                               unit.FateId == 0 &&
                               unit.NpcId != 541 &&
                               !abandonedIdleHuntTargetIds.Contains(unit.ObjectId) &&
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
                if (!IsUsableFate(f, 0, true))
                {
                    continue;
                }
                if (f.Icon.ToString() == "Boss" && f.Progress > 85)
                {
                    if (logCandidates)
                    {
                        Log.Information($"Skipping FATE \"{f.Name}\". Boss FATE progress is greater than 85%.");
                    }
                }
                else if (fatebotInstance.BlackListedFates.Contains(f.Name) || BlacklistIds.Contains((int)f.Id))
                {
                    //Log.Information($"Skipping FATE {f.Name}. FATE is blacklisted");
                }
                else
                {
                    ReturnList.Add(f);

                    if (logCandidates)
                    {
                        Log.Information($"Adding FATE \"{f.Name}\". Distance is {Core.Me.Distance(f.Location)}.");
                    }
                }
            }

            return ReturnList;
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
                    Log.Information($"Adding Focused FATE: {focusedFate.Name}. Distance is {Core.Me.Distance(focusedFate.Location)}.");
                }

                return focusedFate;
            }

            return FilterFateCandidates(FateManager.ActiveFates.ToList(), logCandidates)
                .Where(fate => fate.Level <= _max && fate.Level >= _min)
                .OrderByDescending(fate => Core.Me.Distance(fate.Location) <= fate.Radius)
                .ThenBy(fate => Core.Me.Distance(fate.Location))
                .FirstOrDefault();
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
            var _fate = FateManager.ActiveFates.Where(fate =>
                ids.Contains((int)fate.Id) &&
                fate.Progress >= MinProgress &&
                // Focused FateId profiles historically did not require level attributes. Apply
                // each bound only when its XML attribute was supplied, preserving those profiles
                // while still honoring intentional one-sided or two-sided constraints.
                (!hasExplicitMinLevel || fate.Level >= _min) &&
                (!hasExplicitMaxLevel || fate.Level <= _max) &&
                IsUsableFate(fate, 0, true))
                .OrderByDescending(fate => Core.Me.Distance(fate.Location) <= fate.Radius)
                .ThenBy(fate => Core.Me.Distance(fate.Location))
                .Take(1);
            var fateArray = _fate as FateData[] ?? _fate.ToArray();

            return fateArray.Length > 0 ? fateArray[0] : null;
        }

        protected override void OnResetCachedDone()
        {
            _done = false;
        }

        private async Task<bool> CheckLevelSync()
        {
            fateImIn = FateManager.ActiveFates.OrderBy(fate => Core.Me.Distance(fate.Location)).FirstOrDefault(fate => fate.Level <= _max && fate.Level >= _min);
            if (fateImIn != null && FateManager.WithinFate && fateImIn.MaxLevel < Core.Player.ClassLevel && !Core.Me.IsLevelSynced)
            {
                ToDoList.LevelSync();
                return false;
            }

            return false;
        }

        private ITargetingProvider cachedProvider;

        private Composite _coroutine;

        protected override void OnStart()
        {
            hasExplicitMinLevel = !string.IsNullOrWhiteSpace(MinLevel);
            hasExplicitMaxLevel = !string.IsNullOrWhiteSpace(MaxLevel);
            _min = hasExplicitMinLevel ? Convert.ToInt32(MinLevel) : 0;
            _max = hasExplicitMaxLevel ? Convert.ToInt32(MaxLevel) : 0;
            _coroutine = new ActionRunCoroutine(r => CheckLevelSync());
            AddHooks();
            _timeout = Convert.ToInt32(Timeout);
            currentstep = 0;
            fatesDone = 0;
            mobsHunted = 0;
            died = 0;
            wasDead = false;
            sessionSummaryReported = false;
            huntedTargetObjectId = 0;
            idleHuntAnchor = null;
            postFateCombatLeashAnchor = null;
            abandonedIdleHuntTargetIds.Clear();
            rejectedCenterLandingFateId = 0;
            llFateZoneId = WorldManager.ZoneId;
            zoneSafetyTriggered = false;
            participatedInTrackedFate = false;
            rejectedFateIds.Clear();
            Log.Information(HuntBetweenFates ? "Doing FATEs and hunting nearby targets in between." : "Doing FATEs; downtime hunting is disabled.");
            Log.Information($"Stats: MinFate level={_min} MaxFatelvl={_max}");

            // MaxLevel = "34";
            // MinLevel = "25";
            cachedProvider = CombatTargeting.Instance.Provider;
            CombatTargeting.Instance.Provider = new MySuperAwesomeTargetingProvider(IsInsideActiveTargetingLeashes);
            currentfate = null;
            Poi.Clear("Clearing POI");
            saveNow = DateTime.Now;
        }

        private void AddHooks()
        {
            Log.Information($"Adding LevelSync Hook");

            TreeHooks.Instance.AddHook("TreeStart", _coroutine);
        }

        protected override void OnDone()
        {
            ReportSessionSummary("tag stopped");
            RemoveHooks();
            currentstep = 0;
            idleHuntAnchor = null;
            postFateCombatLeashAnchor = null;
            rejectedCenterLandingFateId = 0;

            CombatTargeting.Instance.Provider = cachedProvider;
        }

        private void RemoveHooks()
        {
            Log.Information($"Removing LevelSync Hook");
            TreeHooks.Instance.RemoveHook("TreeStart", _coroutine);
        }
    }

    //----------------------------------------------------------------------------------

    public class MySuperAwesomeTargetingProvider : ITargetingProvider
    {
        public HashSet<uint> IgnoreNpcIds = new HashSet<uint>()
        {
            1201
        };

        private BattleCharacter[] _aggroedBattleCharacters;
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
            var allUnits = GameObjectManager.GetObjectsOfType<BattleCharacter>().ToArray();

            _aggroedBattleCharacters = GameObjectManager.Attackers.ToArray();
            var inCombat = Core.Player.InCombat;
            var hostileUnits = allUnits.Where(r => IsValidUnit(inCombat, r))
                .Select(n => new Score
                {
                    Unit = n,
                    Weight = GetScoreForUnit(n)
                }).ToArray();

            // Order by weight (descending).
            return hostileUnits.OrderByDescending(s => s.Weight).Select(s => s.Unit).ToList();
        }

        /// <summary> Query if 'unit' is valid unit. </summary>
        /// <remarks> Nesox, 2013-06-29. </remarks>
        private bool IsValidUnit(bool incombat, BattleCharacter unit)
        {
            if (!unit.IsValid || !unit.InLineOfSight() || unit.IsDead || !unit.IsVisible || unit.CurrentHealthPercent <= 0)
            {
                return false;
            }

            // Selection rejects hand-in events, but CombatTargeting pulses independently and can
            // otherwise reacquire an enemy from an overlapping FATE. Enforce the unsupported icon
            // here as well so no LLFate-owned targeting path can engage a KillHandIn event.
            if (unit.IsFate && unit.FateId != 0 &&
                FateManager.GetFateById(unit.FateId)?.Icon == FateIconType.KillHandIn)
            {
                return false;
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
            var isSelectedIdleHuntTarget = Poi.Current?.Type == PoiType.Kill &&
                                           Poi.Current.BattleCharacter?.ObjectId == unit.ObjectId &&
                                           !unit.IsFate;

            return _aggroedBattleCharacters.Contains(unit) ||
                   (unit.CanAttack && !incombat &&
                    (unit.IsFate && Vector3.Distance(unit.Location, LLFate.Position) <= 50 || isSelectedIdleHuntTarget));
        }

        /// <summary> Gets score for a unit. </summary>
        /// <remarks> Nesox, 2013-06-29. </remarks>
        /// <param name="unit"> The unit. </param>
        /// <returns> The score for unit. </returns>
        private double GetScoreForUnit(BattleCharacter unit)
        {
            double weight = 200 - (2 * unit.Distance());

            weight += unit.MaxHealth; // asuming that bosses have huge health this should make us target a boss

            if (unit.CurrentTargetId == Core.Player.CurrentTargetId)
            {
                // Little extra weight on current targets.
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
