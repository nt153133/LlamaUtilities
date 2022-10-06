using System.Linq;
using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot.Managers;
using LlamaLibrary.Utilities;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLDesynth")]
    public class LLDesynth : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("ItemIds")]
        public int[] ItemIds { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LLDesynth() : base() { }

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
            return new ActionRunCoroutine(r => DesynthItems(ItemIds));
        }

        private async Task DesynthItems(int[] itemId)
        {
            var itemsToDesynth = InventoryManager.FilledSlots
                .Where(bs => bs.IsDesynthesizable && itemId.Contains((int)bs.RawItemId));

            await Inventory.Desynth(itemsToDesynth);

            _isDone = true;
        }
    }
}