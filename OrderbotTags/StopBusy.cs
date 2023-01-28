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

        [XmlAttribute("LeaveDuty")]
        [DefaultValue(true)]
        public bool LeaveDuty { get; set; }

        [XmlAttribute("StopFishing")]
        [DefaultValue(true)]
        public bool StopFishing { get; set; }

        [XmlAttribute("Dismount")]
        [DefaultValue(true)]
        public bool Dismount { get; set; }

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

            await GeneralFunctions.StopBusy(LeaveDuty,StopFishing,Dismount);

            _isDone = true;
        }
    }
}