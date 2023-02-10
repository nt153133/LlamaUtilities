using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("PickupAnimaQuest")]
    public class PickupAnimaQuest : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("QuestId")]
        [XmlAttribute("QuestID")]
        public int QuestId { get; set; }

        [XmlAttribute("NpcId")]
        [XmlAttribute("NpcID")]
        [XmlAttribute("NPCID")]
        public int NpcId { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public PickupAnimaQuest() : base()
        {
        }

        protected override void OnStart()
        {
        }

        protected override void OnDone()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => PickUpAnimeWeaponQuestTask());
        }

        private async Task PickUpAnimeWeaponQuestTask()
        {
            var npcId = GameObjectManager.GetObjectByNPCId((uint) NpcId);
            var QuestName = DataManager.GetLocalizedQuestName(QuestId);

            if (npcId == null)
            {
                Logging.WriteDiagnostic($"Couldn't find {npcId.Name}, exiting'.");
                _isDone = true;
                return;
            }

            if (QuestLogManager.HasQuest(QuestId))
            {
                Logging.WriteDiagnostic($"We already have quest {QuestName}, exiting'.");
                _isDone = true;
                return;
            }

            if (!npcId.IsWithinInteractRange)

            {
                var _target = npcId.Location;
                Navigator.PlayerMover.MoveTowards(_target);
                while (_target.Distance2D(Core.Me.Location) >= 4)
                {
                    Navigator.PlayerMover.MoveTowards(_target);
                    await Coroutine.Sleep(100);
                }

                Navigator.PlayerMover.MoveStop();
            }

            npcId.Interact();

            await Coroutine.Wait(10000, () => SelectIconString.IsOpen);

            if (!SelectIconString.IsOpen)
            {
                npcId.Interact();
                await Coroutine.Wait(10000, () => SelectIconString.IsOpen);
                if (!SelectIconString.IsOpen)
                {
                    Logging.WriteDiagnostic($"Interacting with {npcId.Name} didn't happen, exiting'.");
                    _isDone = true;
                    return;
                }
            }

            if (QuestLogManager.IsQuestCompleted((uint) QuestId))
            {
                if (SelectIconString.IsOpen)
                {
                    Logging.WriteDiagnostic($"Choosing '{LlamaLibrary.Helpers.Translator.AnimaWeaponRecreation}'.");
                    if (!SelectIconString.ClickLineContains(LlamaLibrary.Helpers.Translator.AnimaWeaponRecreation))
                    {
                        Logging.WriteDiagnostic($"We can't find '{LlamaLibrary.Helpers.Translator.AnimaWeaponRecreation}', exiting'.");
                        _isDone = true;
                        return;
                    }

                    await Coroutine.Wait(10000, () => !SelectIconString.IsOpen);

                    await Coroutine.Wait(10000, () => SelectString.IsOpen);
                    {
                        Logging.WriteDiagnostic($"Choosing '{QuestName}'.");
                        if (!SelectString.ClickLineContains($"{QuestName}"))
                        {
                            Logging.WriteDiagnostic($"We can't find quest {QuestName}, exiting'.");
                            _isDone = true;
                            return;
                        }

                        await Coroutine.Wait(10000, () => !SelectString.IsOpen);
                    }
                }
            }
            else
            {
                if (SelectIconString.IsOpen)
                {
                    Logging.WriteDiagnostic($"Choosing '{QuestName}'.");
                    if (!SelectIconString.ClickLineContains($"{QuestName}"))
                    {
                        Logging.WriteDiagnostic($"We can't find quest {QuestName}, exiting'.");
                        _isDone = true;
                        return;
                    }

                    await Coroutine.Wait(10000, () => !SelectIconString.IsOpen);
                }
            }

            Logging.WriteDiagnostic("Waiting for dialog to open.");
            await Coroutine.Wait(10000, () => Talk.DialogOpen);
            while (!SelectString.IsOpen)
            {
                Talk.Next();
                await Coroutine.Yield();
                await Coroutine.Sleep(500);
            }

            Logging.WriteDiagnostic("Choosing current class.");
            ff14bot.RemoteWindows.SelectString.ClickSlot(1);
            await Coroutine.Wait(10000, () => JournalAccept.IsOpen);
            {
                Logging.WriteDiagnostic($"Accepting {QuestName}.");
                JournalAccept.Accept();
            }
            while (!QuestLogManager.HasQuest(QuestId))
            {
                if (QuestLogManager.InCutscene)
                {
                    TreeRoot.StatusText = "InCutscene";
                    if (ff14bot.RemoteAgents.AgentCutScene.Instance != null)
                    {
                        ff14bot.RemoteAgents.AgentCutScene.Instance.PromptSkip();
                        await Coroutine.Wait(2000, () => SelectString.IsOpen || SelectYesno.IsOpen);

                        if (SelectString.IsOpen)
                        {
                            SelectString.ClickSlot(0);
                        }

                        if (SelectYesno.IsOpen)
                        {
                            SelectYesno.Yes();
                        }
                    }
                }

                Talk.Next();
                await Coroutine.Wait(1000, () => !Talk.DialogOpen);
                await Coroutine.Wait(1000, () => Talk.DialogOpen);
                await Coroutine.Yield();
            }

            _isDone = true;
        }
    }
}