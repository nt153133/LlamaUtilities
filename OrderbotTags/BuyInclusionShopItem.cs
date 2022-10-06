using System.ComponentModel;
using System.Threading.Tasks;
using Clio.XmlEngine;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("BuyInclusionShopItem")]
    public class BuyInclusionShopItem : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("ItemId")]
        public int ItemId { get; set; }

        [XmlAttribute("Count")]
        [DefaultValue(0)]
        public int Count { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public BuyInclusionShopItem() : base() { }

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
            return new ActionRunCoroutine(r => BuyKnownItem(ItemId, Count));
        }

        private async Task BuyKnownItem(int itemId, int Count)
        {
            await LlamaLibrary.Helpers.InclusionShopHelper.BuyItemGoToNpc((uint)itemId, Count);

            _isDone = true;
        }
    }
}