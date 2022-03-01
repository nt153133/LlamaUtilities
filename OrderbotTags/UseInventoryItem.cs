using System.Collections;
using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Enums;
using ff14bot.Managers;
using TreeSharp;
using System.Linq;
using static LlamaLibrary.Helpers.GeneralFunctions;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("UseInventoryItem")]
    public class UseInventoryItem : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("itemIDs")]
        [XmlAttribute("ItemIDs")]
        [XmlAttribute("itemID")]
        [XmlAttribute("ItemID")]
        public int[] Item { get; set; }


        public override bool HighPriority => true;
        public override bool IsDone => _isDone;

        public UseInventoryItem() : base() { }

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
            return new ActionRunCoroutine(r => RunUseItem(Item));
        }

        private async Task RunUseItem(int[] useItem)
        {
            foreach (BagSlot slot in InventoryManager.FilledSlots)
            {
                if (!useItem.Contains((int) slot.RawItemId))
                {
                    continue;
                }

                for (int i = 0; i < slot.Count; i++)
                {
                    Log.Information($"Using {slot.Item.CurrentLocaleName} {i}.");
                    slot.UseItem();
                    await Coroutine.Sleep(4000);
                }
            }

            _isDone = true;
        }
    }
}