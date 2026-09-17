#if !RB_TC
using System;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
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
using Gauge = ff14bot.Managers.ActionResourceManager.BeastMaster;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>
    /// Completes A Beastmaster's Path's dummy objective with Cu Sith, independent of rotation settings.
    /// Assign Cu Sith with SetBeastmasterPet first. This tag summons that slot and exclusively owns
    /// combat until quest step 1 advances; it never treats an accepted action request as quest success.
    /// </summary>
    [XmlElement("BeastmasterInfinitiveCombo")]
    public sealed class BeastmasterInfinitiveCombo : LLProfileBehavior
    {
        // Global 7.56 actions: Cu Sith Trick (Rake, Rampant) -> Mistral Axe (Durant) opens
        // Moonstalker. Three Mastered Instinct stacks make Rally restore 250 TP; Risen Fall
        // (Sunstrider) then completes Universality. Wait for compass changes, not fixed cast delays.
        private const uint Trick = 47093, Mistral = 44887, Rally = 44905, RisenFall = 44932;
        private static readonly uint[] Horns = { 44881, 44892, 44894 };
        private Composite _hook;
        private bool _done;
        private int _phase;
        private DateTime _phaseAt, _started, _lastDiagnostic;
        private uint _targetId;
        private string _lastStatus;

        /// <summary>Gets or sets the local training dummy NPC ID; defaults to the quest dummy, 541.</summary>
        [XmlAttribute("NpcId")]
        public uint NpcId { get; set; } = 541;

        /// <summary>Gets or sets the total attempt budget in seconds; includes TP buildup and Rally cooldown.</summary>
        [XmlAttribute("Timeout")]
        public int Timeout { get; set; } = 600;

        /// <summary>Gets true only after the quest's combo objective has advanced.</summary>
        public override bool IsDone => _done;

        /// <summary>Gets priority over ordinary profile movement.</summary>
        public override bool HighPriority => true;

        /// <summary>Installs a scoped combat owner so a routine cannot spend the reserved TP or swap familiars.</summary>
        protected override void OnStart()
        {
            Cleanup();
            _done = false;
            _targetId = 0;
            _lastStatus = null;
            _lastDiagnostic = DateTime.MinValue;
            _started = DateTime.UtcNow;
            Phase(0);
            if (NpcId != 541 || Timeout <= 0 || Core.Me.CurrentJob != ClassJobType.BeastMaster || Core.Me.ClassLevel < 50)
            {
                Fail("Requires level 50 Beastmaster and the training dummy (NpcId 541).");
                return;
            }
            if (QuestLogManager.IsQuestCompleted(71034) || QuestLogManager.GetQuestStep(71034) > 1)
            {
                _done = true;
                return;
            }
            if (!QuestLogManager.HasQuest(71034) || QuestLogManager.GetQuestStep(71034) != 1 || WorldManager.ZoneId != 148)
            {
                Fail("Requires A Beastmaster's Path step 1 in Central Shroud; use GetTo before this tag.");
                return;
            }
            _hook = new ActionRunCoroutine(_ => Pulse());
            TreeRoot.OnStop += OnStopped;
            TreeHooks.Instance.InsertHook("TreeStart", 0, _hook);
        }

        /// <summary>Creates a placeholder; the scoped TreeStart coroutine performs the sequence.</summary>
        /// <returns>A successful placeholder.</returns>
        protected override Composite CreateBehavior() => new ActionAlwaysSucceed();

        /// <summary>Releases only this tag's hook on profile advancement.</summary>
        protected override void OnDone() => Cleanup();

        /// <summary>Releases transient ownership when a profile loop resets this tag.</summary>
        protected override void OnResetCachedDone() { Cleanup(); _done = false; }

        private void OnStopped(BotBase bot) => Cleanup();

        private void Cleanup()
        {
            TreeRoot.OnStop -= OnStopped;
            if (_hook == null) return;
            TreeHooks.Instance.RemoveHook("TreeStart", _hook);
            _hook = null;
            Navigator.PlayerMover.MoveStop();
        }

        private void Phase(int value) { _phase = value; _phaseAt = DateTime.UtcNow; }
        private double PhaseSeconds => (DateTime.UtcNow - _phaseAt).TotalSeconds;
        private void Status(string value)
        {
            if (_lastStatus == value && (DateTime.UtcNow - _lastDiagnostic).TotalSeconds < 15) return;
            _lastStatus = value;
            _lastDiagnostic = DateTime.UtcNow;
            Log.Information($"{value}; TP={Gauge.TP}, pet TP={Gauge.PetTP}, mastered={Gauge.MasteredInstinct}, compass={Gauge.InnerCompass}, phase={_phase}.");
        }

        private bool Fail(string message)
        {
            Log.Error(message);
            Cleanup();
            // This is a training dummy, not an instance. Do not let a failed objective silently
            // fall through to quest turn-in, or leave an unrestricted routine attacking forever.
            TreeRoot.Stop("BeastmasterInfinitiveCombo: " + message);
            return true;
        }

        private async Task<bool> Cast(uint action, GameObject target)
        {
            if (!ActionManager.CanCast(action, target)) return false;
            if (!ActionManager.DoAction(action, target)) return false;
            Log.Information("Requested " + DataManager.GetSpellData(action).Name + " (" + action + ").");
            // Only the animation lock is padded; subsequent phases verify gauge/server state.
            await Coroutine.Sleep(750);
            return true;
        }

        private async Task<bool> Pulse()
        {
            if (_done) return false;
            if (QuestLogManager.IsQuestCompleted(71034) ||
                (QuestLogManager.HasQuest(71034) && QuestLogManager.GetQuestStep(71034) > 1))
            {
                Log.Information("Infinitive combo objective verified complete by quest progress.");
                _done = true;
                Navigator.PlayerMover.MoveStop();
                return true;
            }
            if (!QuestLogManager.HasQuest(71034) || WorldManager.ZoneId != 148 ||
                Core.Me.CurrentJob != ClassJobType.BeastMaster || Core.Me.CurrentHealth == 0)
                return Fail("Quest, zone, job or player state changed during the dummy sequence.");
            if ((DateTime.UtcNow - _started).TotalSeconds > Timeout)
                return Fail("Combo objective did not advance within the configured timeout; see phase/gauge diagnostics.");

            var target = GameObjectManager.GetObjectsOfType<BattleCharacter>()
                .Where(x => x.NpcId == NpcId && x.IsValid && x.IsTargetable && x.Distance2D() < 30)
                .OrderBy(x => x.ObjectId == _targetId ? 0 : 1).ThenBy(x => x.Distance2D()).FirstOrDefault();
            if (target == null) { Status("Waiting for the nearby quest dummy"); return true; }
            if (_targetId != target.ObjectId) { _targetId = target.ObjectId; Phase(0); }
            target.Target();
            if (Core.Me.IsMounted) { await CommonTasks.StopAndDismount(); return true; }
            if (target.Distance2D() > 3)
            {
                Navigator.MoveTo(new MoveToParameters(target.Location, "Infinitive combo dummy") { DistanceTolerance = 3f, UseMount = false });
                return true;
            }
            Navigator.PlayerMover.MoveStop();
            if (Core.Me.IsCasting) return true;

            var slots = PetManager.BeastmasterPetSlots;
            int slot = Array.IndexOf(slots, BeastmasterPet.Cu_Sith);
            if (slot < 0 || slot >= Horns.Length)
                return Fail("Assign Cu Sith first with <SetBeastmasterPet PetId=\"1\" Battlehorn=\"1\" Summon=\"true\" />.");
            if (Core.Me.Pet == null || !Core.Me.Pet.IsValid || Gauge.ActiveBattlehorn != slot + 1)
            {
                Status("Summoning Cu Sith");
                Phase(0);
                uint horn = Horns[slot];
                // Older RB CanCast rejects usable Battlehorns; use the existing pet tag's
                // cooldown/readback approach, and never issue another horn during its cast.
                if (DataManager.GetSpellData(horn).Cooldown <= TimeSpan.Zero && ActionManager.DoAction(horn, Core.Me))
                    await Coroutine.Wait(10000, () => Core.Me.Pet != null && Core.Me.Pet.IsValid && Gauge.ActiveBattlehorn == slot + 1);
                return true;
            }

            Status("Performing Cu Sith infinitive combo");
            var compass = Gauge.InnerCompass;
            switch (_phase)
            {
                case 0:
                    // Let old Hearts expire before starting a fresh, reproducible two-action pair.
                    if (compass != Gauge.InnerCompassState.None) return true;
                    // At 250 TP the normal axes transform. Spend a full bar before attempting
                    // the Rampant -> Durant pair, rather than accidentally casting its transformed form.
                    if (Gauge.TP >= 250) { await Cast(RisenFall, target); return true; }
                    if (Gauge.TP >= 100 && Gauge.PetTP >= 100)
                    {
                        if (await Cast(Trick, target)) Phase(1);
                        return true;
                    }
                    uint basic = ActionManager.LastSpellId == 44879 ? 44883u :
                        ActionManager.LastSpellId == 44883 ? 44885u : 44879u;
                    await Cast(basic, target);
                    return true;
                case 1:
                    // Trick is a pet order; TP consumption alone does not mean Rake has landed.
                    if (compass == Gauge.InnerCompassState.Rampant)
                    {
                        if (await Cast(Mistral, target)) Phase(2);
                    }
                    else if (PhaseSeconds > 10) { Status("Rake Heart missing; rebuilding the pair"); Phase(0); }
                    return true;
                case 2:
                    if (compass == Gauge.InnerCompassState.Moonstalker)
                    {
                        if (Gauge.MasteredInstinct >= 3 && DataManager.GetSpellData(Rally).Cooldown <= TimeSpan.Zero)
                            Phase(3);
                        else { Status("Building three Mastered Instinct stacks / waiting for Rally"); Phase(0); }
                    }
                    else if (PhaseSeconds > 12) { Status("Moonstalker not observed; retrying"); Phase(0); }
                    return true;
                case 3:
                    if (compass != Gauge.InnerCompassState.Moonstalker) { Phase(0); return true; }
                    if (Gauge.TP >= 250) { Phase(4); return true; }
                    if (await Cast(Rally, Core.Me)) Phase(4);
                    return true;
                case 4:
                    if (compass != Gauge.InnerCompassState.Moonstalker) { Status("Combo window expired; retrying"); Phase(0); return true; }
                    if (Gauge.TP >= 250 && await Cast(RisenFall, target)) Phase(5);
                    return true;
                case 5:
                    // The client may acknowledge damage before updating the quest log. Preserve
                    // a quiet registration window; never return success just because DoAction did.
                    if (PhaseSeconds > 10) { Status("Quest has not acknowledged Universality; retrying"); Phase(0); }
                    return true;
                default:
                    return Fail("Unknown combo phase.");
            }
        }
    }
}
#endif

