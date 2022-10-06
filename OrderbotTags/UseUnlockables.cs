using System.Threading.Tasks;
using Clio.XmlEngine;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("UseUnlockables")]
    public class UseUnlockables : LLProfileBehavior
    {
        private bool _isDone;


        public override bool HighPriority => true;
        public override bool IsDone => _isDone;

        public UseUnlockables() : base() { }

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
            return new ActionRunCoroutine(r => RunUseUnlockables());
        }

        private async Task RunUseUnlockables()
        {

            await LlamaLibrary.Utilities.Inventory.UseUnlockablesAsync();

            _isDone = true;
        }
    }
}