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

        private readonly BattleCharacter npc;
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
        private uint llFateZoneId;
        private bool zoneSafetyTriggered;
        private bool participatedInTrackedFate;
        private readonly HashSet<uint> rejectedFateIds = new HashSet<uint>();

        //----------------------
        public bool IsCompleted = false;

        //Fate class variables
        public static Vector3 Position = new Vector3(0f, 0f, 0f);

        private uint fateid = 0;
        private float fateRadius;
        private string fateName = "";
        private string fateStatus = "";
        private ITargetingProvider tempProvider;
        private int forelornMaiden = 6737;
        private int theForlorn = 6738;

        //-------
        public static int currentstep = 0; //currentstep 1 we are in a fate / currentstep 0 we are not in a fate

        private static readonly Stopwatch ClusterTimer = Stopwatch.StartNew();

        // After a FATE retires, combat can linger briefly even though its enemies are already
        // retreating. Keep defensive cleanup local to the handoff point; following a retreating
        // attacker here makes the combat routine run indefinitely away from the completed FATE.
        private const float PostFateCombatLeash = 20f;
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
                                        new Decorator(ret => !ShouldStop() && currentstep == 1 && Vector3.Distance(Core.Player.Location, Position) > (fateRadius - 10),
                                                      new PrioritySelector(
                                                                           new Decorator(ret => UseFlight && AetherCurrentManager.FinishedZones.Contains(WorldManager.ZoneId),
                                                                                         new ActionRunCoroutine(obj => FlyToFateAndLand(() => currentfate))),
                                                                           // LLFate is already bounded to its starting zone, so ground travel must
                                                                           // remain pulse-driven to recheck While instead of entering GetTo's
                                                                           // uninterruptible navigation-graph coroutine.
                                                                           new Decorator(ret => WorldManager.ZoneId == llFateZoneId &&
                                                                                                IsUsableFate(FateManager.GetFateById(fateid), fateid, false),
                                                                                         new ActionRunCoroutine(obj => GroundMoveToFate())))),
                                        new Decorator(r => currentfate != null && FateManager.WithinFate && currentfate.Icon == FateIconType.KillHandIn && currentfate.TimeLeft.Minutes <= 8,
                                                      new Sequence(new ActionRunCoroutine(async r =>
                                                                   {
                                                                       Poi.Clear("Handing in items.");
                                                                       Log.Information("Hand-in Fate");

                                                                       var fateNpcs = GameObjectManager
                                                                           .GetObjectsOfType<BattleCharacter>()
                                                                           .Where(bc => bc.IsFate && !bc.CanAttack && bc.FateId == currentfate.Id)
                                                                           .GroupBy(bc => bc)
                                                                           .OrderByDescending(group => group.Count())
                                                                           .Select(group => group.Key);

                                                                       var handinNpc = fateNpcs.LastOrDefault();

                                                                       if (handinNpc == null)
                                                                       {
                                                                           Log.Error("Could not find handin NPC. Something is wrong.");
                                                                           return;
                                                                       }

                                                                       tempProvider = CombatTargeting.Instance.Provider;
                                                                       CombatTargeting.Instance.Provider = new NullTargetingProvider();

                                                                       await MoveTo(handinNpc.Location);

                                                                       GameObjectManager.GetObjectByNPCId(handinNpc.NpcId).Interact();
                                                                       Talk.Next();
                                                                       InventoryManager.GetBagByInventoryBagId(InventoryBagId.KeyItems).FilledSlots.LastOrDefault().Handover();
                                                                       Request.HandOver();

                                                                       CombatTargeting.Instance.Provider = tempProvider;
                                                                   }),
                                                                   new ActionAlwaysFail() //always fail that the rest of the tree is traveresd
                                                                  )),
                                        new Decorator(ret => Talk.DialogOpen,
                                                      new Action(r => { Talk.Next(); })),
                                        new Decorator(ret => Request.IsOpen,
                                                      new Action(r =>
                                                      {
                                                          GameObjectManager.GetObjectByNPCId(npc.NpcId).Interact();
                                                          InventoryManager.GetBagByInventoryBagId(InventoryBagId.KeyItems).FilledSlots.LastOrDefault().Handover();
                                                          Request.HandOver();
                                                      })),

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
            }

            var result = await CommonTasks.FlyToAndLandAsync(
                                                             selectedFateLocation.Add(0f, 15, 0f),
                                                             abortCondition: () =>
                                                             {
                                                                 var f = FateManager.GetFateById(selectedFateId);
                                                                 return WorldManager.ZoneId != llFateZoneId ||
                                                                        !IsUsableFate(f, selectedFateId, false) ||
                                                                        ShouldStop();
                                                             },
                                                             destinationName: $"FlyToFateAndLand:{selectedFateName}");

            return result;
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
                Poi.Current = new Poi(GetFateTargets(), PoiType.Kill);
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

                Log.Information($"Fate Details: {currentfate}");
                Position = currentfate.Location;
                fateRadius = currentfate.Radius;
                fateid = currentfate.Id;
                fateName = currentfate.Name;
                currentstep = 1;
            }
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
            if (target != null && target.IsValid && target.IsDead)
            {
                mobsHunted++;
                Log.Information($"Completed idle hunt. Session hunt total: {mobsHunted}.");
                huntedTargetObjectId = 0;
            }
            else if (target == null && !Core.Me.InCombat)
            {
                // A missing object is not enough to claim a kill: it may have phased or left object range.
                huntedTargetObjectId = 0;
            }
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
        /// validity alone. Empty names, zero geometry, expired timers, and mismatched IDs identify
        /// the transient slot-reuse state observed after a FATE completes.
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
                Poi.Current = new Poi(GetFateTargets(), PoiType.Kill);
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

        public GameObject GetFateTargets()
        {
            var target = GameObjectManager.GameObjects
                .Select(unit => new { unit, bc = unit as BattleCharacter })
                .Where(x =>
                           x.bc != null &&
                           x.unit.CanAttack &&
                           x.unit.IsTargetable &&
                           x.unit.IsVisible &&
                           x.bc.FateId != 0 &&
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
                               unit.Distance(Core.Player.Location) <= HuntRadius &&
                               unit.MaxHealth <= Core.Me.CurrentHealth * 3)
                .OrderBy(unit => unit.Distance(Core.Player.Location))
                .FirstOrDefault();
        }

        public List<FateData> MyFilter(List<FateData> List)
        {
            var ReturnList = new List<FateData>();
            foreach (var f in List)
            {
                if (!IsUsableFate(f, 0, true))
                {
                    continue;
                }
                if (f.Icon.ToString() == "Boss" && f.Progress > 85)
                {
                    Log.Information($"Skipping FATE \"{f.Name}\". Boss FATE progress is greater than 85%.");
                }
                else if (fatebotInstance.BlackListedFates.Contains(f.Name) || BlacklistIds.Contains((int)f.Id))
                {
                    //Log.Information($"Skipping FATE {f.Name}. FATE is blacklisted");
                }
                else
                {
                    ReturnList.Add(f);

                    Log.Information($"Adding FATE \"{f.Name}\". Distance is {Core.Me.Distance(f.Location)}.");
                }
            }

            return ReturnList;
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

            if (FateIds.Length > 0)
            {
                currentfate = IsFateActive(FateIds);
                if (currentfate == null)
                {
                    return false;
                }
                else
                {
                    Log.Information($"Adding Focused FATE: {currentfate.Name}. Distance is {Core.Me.Distance(currentfate.Location)}.");
                    return true;
                }
            }

            var FateCandidates = FateManager.ActiveFates.ToList();
            var FateList = MyFilter(FateCandidates);

            currentfate = FateList.OrderBy(fate => Core.Me.Distance(fate.Location)).FirstOrDefault(fate => fate.Level <= _max && fate.Level >= _min);

            return currentfate != null;
        }

        // check all fates and return the FateData with the given Ids or null
        public FateData IsFateActive(int[] ids)
        {
            var _fate = FateManager.ActiveFates.Where(fate =>
                ids.Contains((int)fate.Id) &&
                fate.Progress >= MinProgress &&
                fate.Level >= _min &&
                fate.Level <= _max &&
                IsUsableFate(fate, 0, true)).Take(1);
            var fateArray = _fate as FateData[] ?? _fate.ToArray();

            return fateArray.Length > 0 ? fateArray[0] : null;
        }

        [Obsolete]
        public static async Task<bool> MoveTo(Vector3 location)
        {
            var goalReached = false;

            var distance = Core.Me.Location.Distance(location);
            if (distance < 3f)
            {
                return true;
            }

            while (Core.Me.IsAlive && !goalReached)
            {
                Navigator.MoveTo(location);
                distance = Core.Me.Location.Distance(location);
                goalReached = distance < 3f;

                if (MovementManager.IsMoving && !Core.Me.IsMounted)
                {
                    if (ActionManager.IsSprintReady && WorldManager.InSanctuary)
                    {
                        ActionManager.Sprint();
                    }
                    else if (ActionManager.IsSprintReady && !WorldManager.InSanctuary && Core.Me.InCombat)
                    {
                        ActionManager.Sprint();
                    }
                }

                await Coroutine.Yield();
            }

            return true;
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
            _min = Convert.ToInt32(MinLevel);
            _max = Convert.ToInt32(MaxLevel);
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
            llFateZoneId = WorldManager.ZoneId;
            zoneSafetyTriggered = false;
            participatedInTrackedFate = false;
            rejectedFateIds.Clear();
            Log.Information(HuntBetweenFates ? "Doing FATEs and hunting nearby targets in between." : "Doing FATEs; downtime hunting is disabled.");
            Log.Information($"Stats: MinFate level={_min} MaxFatelvl={_max}");

            // MaxLevel = "34";
            // MinLevel = "25";
            cachedProvider = CombatTargeting.Instance.Provider;
            CombatTargeting.Instance.Provider = new MySuperAwesomeTargetingProvider();
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

            if (IgnoreNpcIds.Contains(unit.NpcId))
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
