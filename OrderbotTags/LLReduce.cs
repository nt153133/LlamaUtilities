using System.Threading.Tasks;

using Clio.XmlEngine;

using LlamaLibrary.Utilities;

using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLReduce")]
    public class LLReduce : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LLReduce() : base() { }

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
            return new ActionRunCoroutine(r => ReduceItems());
        }

        private async Task ReduceItems()
        {
            await Inventory.ReduceAll();

            _isDone = true;
        }
    }
}