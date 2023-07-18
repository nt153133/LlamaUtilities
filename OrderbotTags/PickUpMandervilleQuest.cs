using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("PickUpMandervilleQuest")]
    public class PickUpMandervilleQuest : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("QuestId")]
        [XmlAttribute("QuestID")]
        public int QuestId { get; set; }

        [XmlAttribute("NpcId")]
        [XmlAttribute("NpcID")]
        [XmlAttribute("NPCID")]
        public int NpcId { get; set; }

        [XmlAttribute("XYZ")]
        public Vector3 Location { get; set; }

        [XmlAttribute("ZoneId")]
        public ushort ZoneId { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public PickUpMandervilleQuest() : base()
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
            return new ActionRunCoroutine(r => PickUpMandervilleQuestTask());
        }

        private async Task PickUpMandervilleQuestTask()
        {

            LlamaLibrary.Helpers.NPC.Npc npc = new((uint)NpcId, ZoneId, Location);

            if (QuestLogManager.HasQuest(QuestId))
            {
                Logging.WriteDiagnostic($"We already have quest {QuestName}, exiting'.");
                _isDone = true;
                return;
            }

            if (!await LlamaLibrary.Helpers.Navigation.GetToInteractNpcSelectString(npc))
            {
                Logging.WriteDiagnostic($"Failed to get to {DataManager.GetLocalizedNPCName(NpcId)}");
                return;
            }

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