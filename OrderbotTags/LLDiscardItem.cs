using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Managers;
using LlamaLibrary.Extensions;
using TreeSharp;
using static LlamaLibrary.Helpers.GeneralFunctions;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLDiscardItem")]
    public class LLDiscardItem : LLProfileBehavior
    {
        [XmlAttribute("ItemIDs")]
        [XmlAttribute("ItemIds")]
        [XmlAttribute("ItemID")]
        [XmlAttribute("ItemId")]
        [DefaultValue(new int[0])]
        private int[] ItemIds { get; set; }

        [XmlAttribute("Armory")]
        [DefaultValue(false)]
        public bool IncludeArmory { get; set; }

        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LLDiscardItem() : base() { }

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
            return new ActionRunCoroutine(r => DiscardItems());
        }

        private async Task DiscardItems()
        {
            if (_isDone)
            {
                await Coroutine.Yield();
                return;
            }

            // Use FilledInventoryAndArmory if Armory is true, otherwise just FilledSlots
            var slots = (IncludeArmory ? InventoryManager.FilledInventoryAndArmory : InventoryManager.FilledSlots)
                .Where(x => ItemIds.Contains((int)x.RawItemId))
                .ToList();

            if (!slots.Any())
            {
                _isDone = true;
                return;
            }

            await StopBusy(leaveDuty: false, dismount: false);

            foreach (var slot in slots)
            {
                var name = slot.Name;
                slot.Discard();
                if (await Coroutine.Wait(5000, () => !slot.IsValid || !slot.IsFilled))
                {
                    Log.Information($"Discarded item: {name}.");
                    await Coroutine.Sleep(800);
                }
                else
                {
                    Log.Error($"Could not discard item: {name}.");
                }
            }

            _isDone = true;
        }
    }
}
