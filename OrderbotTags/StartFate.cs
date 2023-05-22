using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("StartFate")]
    public class StartFate : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("NpcId")]
        [XmlAttribute("NpcID")]
        public int NpcId { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public StartFate() : base() { }

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
            return new ActionRunCoroutine(r => StartFateTask());
        }

        private async Task StartFateTask()
        {
            var npc = GameObjectManager.GetObjectByNPCId((uint)NpcId);

            while (!Conversation.IsOpen && !Talk.DialogOpen)
            {
                while (Core.Me.Distance(npc.Location) < 2f)
                {
                    await LlamaLibrary.Helpers.Navigation.FlightorMove(npc.Location);
                }
                npc.Interact();
                await Coroutine.Wait(10000, () => Conversation.IsOpen || Talk.DialogOpen);
                if (!Conversation.IsOpen && !Talk.DialogOpen)
                {
                    Logging.WriteDiagnostic("Interact failed, attempting again");
                }
            }

            while (!SelectYesno.IsOpen)
            {
                Talk.Next();
                await Coroutine.Yield();
                await Coroutine.Sleep(500);
            }

            if (SelectYesno.IsOpen)
            {
                SelectYesno.Yes();
            }

            await LlamaLibrary.Helpers.GeneralFunctions.StopBusy();
            _isDone = true;
        }
    }
}