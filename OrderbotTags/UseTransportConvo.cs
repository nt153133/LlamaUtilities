using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("UseTransportConvo")]
    public class UseTransportConvo : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("NpcId")]
        [XmlAttribute("NpcID")]
        public int ShortcutId { get; set; }

        [XmlAttribute("Distance")]
        [XmlAttribute("distance")]
        [DefaultValue(10)]
        public float Distance { get; set; }

        [XmlAttribute("Contains")]
        [XmlAttribute("contains")]
        public string LineContains { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public UseTransportConvo() : base()
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
            return new ActionRunCoroutine(r => UseShortcutTask());
        }

        private async Task UseShortcutTask()
        {
            uint[] npcIds = { (uint)ShortcutId };
            var shortcutNpc = GameObjectManager.GameObjects.Where(r => r.IsTargetable && Core.Me.Location.Distance2D(r.Location) <= Distance && npcIds.Contains(r.NpcId)).OrderBy(r => r.Distance()).FirstOrDefault();

            while (Core.Me.Location.Distance2D(shortcutNpc.Location) > 1.5f)
            {
                await Coroutine.Yield();
                Navigator.PlayerMover.MoveTowards(shortcutNpc.Location);
            }
            Navigator.PlayerMover.MoveStop();

            shortcutNpc.Interact();

            while (!CommonBehaviors.IsLoading)
            {
                await Coroutine.Wait(10000, () => SelectYesno.IsOpen || Talk.DialogOpen || Conversation.IsOpen || CommonBehaviors.IsLoading);

                if (Conversation.IsOpen)
                {
                    int test = 0;
                    foreach (var line in Conversation.GetConversationList)
                    {
                        if (line.Contains(LineContains))
                        {
                            break;
                        }

                        test++;
                    }

                    if (test != Conversation.GetConversationList.Count)
                    {
                        Logging.WriteDiagnostic($"Selecting {LineContains}.");
                        Conversation.SelectLine((uint)test);
                        await Coroutine.Wait(10000, () => !Conversation.IsOpen);
                    }
                }

                if (SelectYesno.IsOpen)
                {
                    Logging.WriteDiagnostic($"Selecting Yes.");
                    SelectYesno.ClickYes();
                    await Coroutine.Wait(10000, () => !SelectYesno.IsOpen);
                }

                if (Talk.DialogOpen)
                {
                    Logging.WriteDiagnostic($"Handling Dialog.");
                    Talk.Next();
                    await Coroutine.Wait(1000, () => !Talk.DialogOpen);
                    await Coroutine.Wait(1000, () => Talk.DialogOpen);
                    await Coroutine.Yield();
                }

                if (CommonBehaviors.IsLoading)
                {
                    break;
                }
            }

            while (CommonBehaviors.IsLoading)
            {
                Logging.WriteDiagnostic($"Waiting on loading.");
                await Coroutine.Wait(20000, () => !CommonBehaviors.IsLoading);
                await Coroutine.Sleep(500);
                Logging.WriteDiagnostic($"Done loading.");
            }

            _isDone = true;
        }
    }
}