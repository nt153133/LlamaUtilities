using System.Threading.Tasks;
using Clio.XmlEngine;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLOpenChest")]
    public class LLOpenChest : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LLOpenChest() : base() { }

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
            return new ActionRunCoroutine(r => OpenChestTask());
        }

        private async Task OpenChestTask()
        {
            await LlamaLibrary.Helpers.GeneralFunctions.OpenChests();

            _isDone = true;
        }
    }
}