using System.Threading.Tasks;
using Clio.XmlEngine;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("GCRankUp")]
    public class GCRankUp : LLProfileBehavior
    {
        private bool _isDone;



        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public GCRankUp() : base() { }

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
            return new ActionRunCoroutine(r => DoGCRankUp());
        }

        private async Task DoGCRankUp()
        {
            await LlamaLibrary.Helpers.GrandCompanyHelper.GoGCRankUp();

            _isDone = true;

        }
    }
}