using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using LlamaLibrary.Helpers;
using TreeSharp;
using static LlamaLibrary.Helpers.GeneralFunctions;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("StopBusy")]
    public class StopBusy : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public StopBusy() : base() { }

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
            return new ActionRunCoroutine(r => GeneralStopBusy());
        }

        private async Task GeneralStopBusy()
        {
            if (_isDone)
            {
                await Coroutine.Yield();
                return;
            }

            await GeneralFunctions.StopBusy();

            _isDone = true;
        }
    }
}