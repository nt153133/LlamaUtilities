using System.Threading.Tasks;
using Clio.XmlEngine;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("GCExpertTurnin")]
    public class GCExpertTurnin : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public GCExpertTurnin() : base() { }

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
            return new ActionRunCoroutine(r => DoGCExpertTurnin());
        }

        private async Task DoGCExpertTurnin()
        {
            await LlamaLibrary.Helpers.GrandCompanyHelper.GCHandInExpert();

            _isDone = true;
        }
    }
}