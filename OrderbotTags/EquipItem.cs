using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Objects;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("EquipItem")]
    public class EquipItem : LLProfileBehavior
    {
        // This doesn't support weapons. Use the EquipWeapons tag for that instead
        private bool _isDone;

        [XmlAttribute("itemIDs")]
        [XmlAttribute("ItemIDs")]
        [XmlAttribute("itemID")]
        [XmlAttribute("ItemID")]
        public int[] Item { get; set; }

        [XmlAttribute("UpdateGearSet")]
        [DefaultValue(false)]
        public bool UpdateGearSet { get; set; } = false;

        public override bool HighPriority => true;

        public EquipItem() : base()
        {
        }

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
            return new ActionRunCoroutine(r => EquipItems(Item));
        }

        private async Task EquipItems(int[] items)
        {
            foreach (var itemId in items)
            {
                var itemData = DataManager.GetItem((uint)itemId);

                if (itemData == null)
                {
                    Log.Warning($"Unable to find item data for {itemId}");
                    continue;
                }

                var slot = GetEquipmentSlot(itemData);

                if (slot == null)
                {
                    Log.Warning($"Could not determine equipment slot for {itemData.CurrentLocaleName}");
                    continue;
                }

                var equipSlot = InventoryManager
                    .GetBagByInventoryBagId(InventoryBagId.EquippedItems)[slot.Value];

                var item = InventoryManager.FilledInventoryAndArmory
                    .FirstOrDefault(i =>
                                        i.RawItemId == (uint)itemId &&
                                        i.BagId != InventoryBagId.EquippedItems);

                if (item == default(BagSlot))
                {
                    Log.Warning($"Could not find item {itemId} in inventory");
                    continue;
                }

                Log.Information($"Equipping {item.EnglishName} to {slot.Value}");

                item.Move(equipSlot);

                await Coroutine.Sleep(250);
            }

            if (UpdateGearSet)
            {
                Log.Information("Updating gearset");
                await LlamaLibrary.ScriptConditions.Helpers.UpdateGearSet();
            }

            _isDone = true;
        }

        private EquipmentSlot? GetEquipmentSlot(Item item)
        {
            if (item == null)
                return null;

            var esc = item.EquipmentCatagory;

            if (esc == ItemUiCategory.Head)
                return EquipmentSlot.Head;

            if (esc == ItemUiCategory.Body)
                return EquipmentSlot.Body;

            if (esc == ItemUiCategory.Hands)
                return EquipmentSlot.Hands;

            if (esc == ItemUiCategory.Legs)
                return EquipmentSlot.Legs;

            if (esc == ItemUiCategory.Feet)
                return EquipmentSlot.Feet;

            if (esc == ItemUiCategory.Earrings)
                return EquipmentSlot.Earring;

            if (esc == ItemUiCategory.Necklace)
                return EquipmentSlot.Necklace;

            if (esc == ItemUiCategory.Bracelets)
                return EquipmentSlot.Bracelet;

            if (esc == ItemUiCategory.Ring)
            {
                var leftRing = InventoryManager
                    .GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.Ring1];

                // Place into the empty ring slot if possible; otherwise replace left ring first.
                return leftRing.IsFilled ? EquipmentSlot.Ring2 : EquipmentSlot.Ring1;
            }

            if (esc == ItemUiCategory.Soul_Crystal)
                return EquipmentSlot.SoulCrystal;

            // Unknown / unsupported slot
            return null;
        }

        public override bool IsDone => _isDone;
    }
}