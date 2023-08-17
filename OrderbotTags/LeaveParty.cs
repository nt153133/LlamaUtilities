using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.Utilities;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LeaveParty")]
    public class LeaveParty : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LeaveParty() : base() { }

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
            return new ActionRunCoroutine(r => LeavePartyTask());
        }

        private async Task LeavePartyTask()
        {
            while (PartyManager.IsInParty)
            {
                ChatManager.SendChat("/pcmd leave");
                await Coroutine.Wait(5000, () => !PartyManager.IsInParty);
            }

            _isDone = true;
        }
    }
}