using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.Extensions;
using LlamaLibrary.Helpers.NPC;
using LlamaLibrary.RemoteWindows;
using TreeSharp;
using FontFamily = System.Drawing.FontFamily;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("EquipOtherWeapon")]
    public class EquipOtherWeapon : LLProfileBehavior
    {
        private bool _isDone;

        public static readonly LlamaLibrary.Logging.LLogger Log = new("EquipOtherWeapon", Colors.MediumPurple);

        public override bool IsDone => _isDone;

        internal string Job;

        internal static int SquareMapleShield = 2219;

        private sealed class PurchaseLocation
        {
            public Npc Vendor { get; }
            public string[] MenuPath { get; }

            public PurchaseLocation(Npc vendor, params string[] menuPath)
            {
                Vendor = vendor;
                MenuPath = menuPath;
            }
        }

        private static readonly Npc Faezghim = new Npc(1001205, 129, new Vector3(-236.44193f, 16.2f, 39.911392f));
        private static readonly Npc Syneyhil = new Npc(1003254, 129, new Vector3(-246.66823f, 16.2f, 40.76276f));
        private static readonly Npc Itto = new Npc(1018990, 628, new Vector3(39.97506f, 4.000001f, 51.724182f));
        private static readonly Npc ShaiTistt = new Npc(1027991, 819, new Vector3(-124.95716f, -1.0881592f, 97.74429f));

        private static readonly HashSet<int> DiscipleOfWarWeapons = new HashSet<int>
        {
            1601, 1680, 1749, 1819, 1889, 7952, 10400, 10462, SquareMapleShield
        };

        private static readonly HashSet<int> DiscipleOfMagicWeapons = new HashSet<int>
        {
            1995, 2055, 2142, 10524
        };

        private static readonly HashSet<int> HandTools = new HashSet<int>
        {
            2314, 2340, 2366, 2391, 2416, 2442, 2467, 2493
        };

        private static readonly HashSet<int> LandTools = new HashSet<int>
        {
            2519, 2545, 2571
        };

        public override bool HighPriority => true;

        [XmlAttribute("UpdateGearSet")]
        [DefaultValue(false)]
        public bool UpdateGearSet { get; set; } = false;

        [XmlAttribute("RequiredItemLevel")]
        [DefaultValue(-1)]
        public int RequiredItemLevel { get; set; } = -1;

        public EquipOtherWeapon() : base()
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

        public static readonly Dictionary<ClassJobType, ClassJobType> ClassMap = new Dictionary<ClassJobType, ClassJobType>
        {
            { ClassJobType.Adventurer, ClassJobType.Adventurer },
            { ClassJobType.Gladiator, ClassJobType.Gladiator },
            { ClassJobType.Pugilist, ClassJobType.Pugilist },
            { ClassJobType.Marauder, ClassJobType.Marauder },
            { ClassJobType.Lancer, ClassJobType.Lancer },
            { ClassJobType.Archer, ClassJobType.Archer },
            { ClassJobType.Conjurer, ClassJobType.Conjurer },
            { ClassJobType.Thaumaturge, ClassJobType.Thaumaturge },
            { ClassJobType.Carpenter, ClassJobType.Carpenter },
            { ClassJobType.Blacksmith, ClassJobType.Blacksmith },
            { ClassJobType.Armorer, ClassJobType.Armorer },
            { ClassJobType.Goldsmith, ClassJobType.Goldsmith },
            { ClassJobType.Leatherworker, ClassJobType.Leatherworker },
            { ClassJobType.Weaver, ClassJobType.Weaver },
            { ClassJobType.Alchemist, ClassJobType.Alchemist },
            { ClassJobType.Culinarian, ClassJobType.Culinarian },
            { ClassJobType.Miner, ClassJobType.Miner },
            { ClassJobType.Botanist, ClassJobType.Botanist },
            { ClassJobType.Fisher, ClassJobType.Fisher },
            { ClassJobType.Paladin, ClassJobType.Gladiator },
            { ClassJobType.Monk, ClassJobType.Pugilist },
            { ClassJobType.Warrior, ClassJobType.Marauder },
            { ClassJobType.Dragoon, ClassJobType.Lancer },
            { ClassJobType.Bard, ClassJobType.Archer },
            { ClassJobType.WhiteMage, ClassJobType.Conjurer },
            { ClassJobType.BlackMage, ClassJobType.Thaumaturge },
            { ClassJobType.Arcanist, ClassJobType.Arcanist },
            { ClassJobType.Summoner, ClassJobType.Arcanist },
            { ClassJobType.Scholar, ClassJobType.Arcanist },
            { ClassJobType.Rogue, ClassJobType.Rogue },
            { ClassJobType.Ninja, ClassJobType.Rogue },
            { ClassJobType.Machinist, ClassJobType.Machinist },
            { ClassJobType.DarkKnight, ClassJobType.DarkKnight },
            { ClassJobType.Astrologian, ClassJobType.Astrologian },
            { ClassJobType.Samurai, ClassJobType.Samurai },
            { ClassJobType.RedMage, ClassJobType.RedMage },
            { ClassJobType.BlueMage, ClassJobType.BlueMage },
            { ClassJobType.Gunbreaker, ClassJobType.Gunbreaker },
            { ClassJobType.Dancer, ClassJobType.Dancer },
            { ClassJobType.Reaper, ClassJobType.Reaper },
            { ClassJobType.Sage, ClassJobType.Sage },
        };

        private async Task ChangeJob()
        {
            // List of the very first weapon available for each class
            var StarterWeapons = new List<KeyValuePair<ClassJobType, int>>()
            {
                new KeyValuePair<ClassJobType, int>(ClassJobType.Astrologian, 10524), // Star Globe
                new KeyValuePair<ClassJobType, int>(ClassJobType.Bard, 1889), // Weathered Shortbow
                new KeyValuePair<ClassJobType, int>(ClassJobType.BlackMage, 2055), // Weathered Scepter
                new KeyValuePair<ClassJobType, int>(ClassJobType.Dancer, 25644), // Deepgold War Quoits
                new KeyValuePair<ClassJobType, int>(ClassJobType.DarkKnight, 10400), // Steel Claymore
                new KeyValuePair<ClassJobType, int>(ClassJobType.Gunbreaker, 25643), // Deepgold Gunblade
                new KeyValuePair<ClassJobType, int>(ClassJobType.Paladin, 1601), // Weathered Shortsword
                new KeyValuePair<ClassJobType, int>(ClassJobType.Dragoon, 1819), // Weathered Spear
                new KeyValuePair<ClassJobType, int>(ClassJobType.Warrior, 1749), // Weathered War Axe
                new KeyValuePair<ClassJobType, int>(ClassJobType.Monk, 1680), // Weathered Hora
                new KeyValuePair<ClassJobType, int>(ClassJobType.Machinist, 10462), // Steel-Barreled Carbine
                new KeyValuePair<ClassJobType, int>(ClassJobType.Ninja, 7952), // Weathered Daggers
                new KeyValuePair<ClassJobType, int>(ClassJobType.Reaper, 35760), // Deepgold War Scythe
                new KeyValuePair<ClassJobType, int>(ClassJobType.RedMage, 18203), // Koppranickel Rapier
                new KeyValuePair<ClassJobType, int>(ClassJobType.Sage, 35778), // Stonegold Milpreves
                new KeyValuePair<ClassJobType, int>(ClassJobType.Samurai, 18046), // High Steel Tachi
                new KeyValuePair<ClassJobType, int>(ClassJobType.Scholar, 34091), // Gaja Codex
                new KeyValuePair<ClassJobType, int>(ClassJobType.Summoner, 2142), // Weathered Grimoire
                new KeyValuePair<ClassJobType, int>(ClassJobType.WhiteMage, 1995), // Weathered Cane

                // Crafters
                new KeyValuePair<ClassJobType, int>(ClassJobType.Carpenter, 2314), // Weathered Saw
                new KeyValuePair<ClassJobType, int>(ClassJobType.Blacksmith, 2340), // Weathered Cross-pein Hammer
                new KeyValuePair<ClassJobType, int>(ClassJobType.Armorer, 2366), // Weathered Doming Hammer
                new KeyValuePair<ClassJobType, int>(ClassJobType.Goldsmith, 2391), // Weathered Chaser Hammer
                new KeyValuePair<ClassJobType, int>(ClassJobType.Leatherworker, 2416), // Weathered Head Knife
                new KeyValuePair<ClassJobType, int>(ClassJobType.Weaver, 2442), // Rusty Needle
                new KeyValuePair<ClassJobType, int>(ClassJobType.Alchemist, 2467), // Weathered Alembic
                new KeyValuePair<ClassJobType, int>(ClassJobType.Culinarian, 2493), // Weathered Skillet

                // Gatherers
                new KeyValuePair<ClassJobType, int>(ClassJobType.Miner, 2519), // Weathered Pickaxe
                new KeyValuePair<ClassJobType, int>(ClassJobType.Botanist, 2545), // Weathered Hatchet
                new KeyValuePair<ClassJobType, int>(ClassJobType.Fisher, 2571), // Weathered Fishing Rod
            };

            var gearSets = GearsetManager.GearSets.Where(i => i.InUse);
            var foundJob = Enum.TryParse(Core.Me.CurrentJob.ToString().Trim(), true, out ClassJobType newjob);
            var weapon = StarterWeapons.FirstOrDefault(x => x.Key == newjob).Value;

            if (Core.Me.CurrentJob.IsDow())
            {
                Job = ClassMap[Core.Me.CurrentJob].ToString().Trim() + "s_Arm";
            }
            else
            {
                Job = Core.Me.CurrentJob.ToString().Trim() + "s_Primary_Tool";
            }

            var categoryFound = Enum.TryParse(Job, true, out ItemUiCategory category);
            var equippedWeapon = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.MainHand];
            var equippedWeaponId = equippedWeapon.RawItemId;
            var matchingWeapons = InventoryManager.FilledInventoryAndArmory
                .Where(i => i.Item.EquipmentCatagory == category && equippedWeaponId != i.RawItemId);

            var item = RequiredItemLevel >= 0
                ? matchingWeapons.OrderBy(i => Math.Abs((int)i.Item.ItemLevel - RequiredItemLevel)).ThenBy(i => i.Item.ItemLevel).FirstOrDefault()
                : matchingWeapons.OrderByDescending(i => i.Item.ItemLevel).FirstOrDefault();

            var mappedItem = weapon == 0 ? null : DataManager.GetItem((uint)weapon);
            var mappedItemIsCloser = RequiredItemLevel >= 0 && mappedItem != null &&
                                     (item == null || Math.Abs((int)mappedItem.ItemLevel - RequiredItemLevel) < Math.Abs((int)item.Item.ItemLevel - RequiredItemLevel));
            var EquipSlot = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.MainHand];

            if (RequiredItemLevel >= 0 && equippedWeapon.IsFilled)
            {
                var equippedDistance = Math.Abs((int)equippedWeapon.Item.ItemLevel - RequiredItemLevel);
                var ownedDistance = item == null
                    ? int.MaxValue
                    : Math.Abs((int)item.Item.ItemLevel - RequiredItemLevel);
                var mappedDistance = mappedItem == null
                    ? int.MaxValue
                    : Math.Abs((int)mappedItem.ItemLevel - RequiredItemLevel);

                if (equippedDistance <= ownedDistance && equippedDistance <= mappedDistance)
                {
                    Log.Information($"Already equipped with {equippedWeapon.EnglishName} (item level {equippedWeapon.Item.ItemLevel}), " +
                                    $"which is closest to requested item level {RequiredItemLevel}. No main-hand change is needed.");

                    if (Core.Me.CurrentJob == ClassJobType.Paladin)
                    {
                        await EnsurePaladinShield();
                    }

                    _isDone = true;
                    return;
                }
            }

            if (categoryFound && item != null && !mappedItemIsCloser)
            {
                Log.Information($"Found Item Category: {category}");
                if (RequiredItemLevel >= 0)
                {
                    Log.Information($"Using owned {item.Item.CurrentLocaleName} (item level {item.Item.ItemLevel}); it is closest to requested item level {RequiredItemLevel}.");
                }
                Log.Information($"Equipping {DataManager.GetItem(item.RawItemId).CurrentLocaleName}");
                if (item != null)
                {
                    item.Move(EquipSlot);
                    if (UpdateGearSet)
                    {
                        await LlamaLibrary.ScriptConditions.Helpers.UpdateGearSet();
                    }
                }
            }
            else
            {
                if (mappedItemIsCloser && item != null)
                {
                    Log.Information($"Mapped {mappedItem.CurrentLocaleName} (item level {mappedItem.ItemLevel}) is closer to requested item level {RequiredItemLevel} than owned {item.Item.CurrentLocaleName} (item level {item.Item.ItemLevel}).");
                }
                else
                {
                    Log.Information("No suitable other weapon was found; buying the mapped weapon.");
                }

                await BuyNewWeapon(weapon);
            }

            _isDone = Core.Me.CurrentJob == newjob;
        }

        internal async Task BuyNewWeapon(int weapon)
        {
            if (weapon == 0)
            {
                Log.Error($"No purchase weapon is configured for {Core.Me.CurrentJob}.");
                return;
            }

            var item = FindOwnedItem((uint)weapon, EquipmentSlot.MainHand);
            var EquipSlot = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.MainHand];

            if (item == null)
            {
                var message = $"Couldn't find another weapon for {Core.Me.CurrentJob}. Attempting to purchase {DataManager.GetItem((uint)weapon).CurrentLocaleName} with RebornBuddy";
                Core.OverlayManager.AddToast(() => $"" + message,
                                             TimeSpan.FromMilliseconds(10000),
                                             System.Windows.Media.Color.FromRgb((byte)13, (byte)106, (byte)175),
                                             System.Windows.Media.Color.FromRgb(13, 106, 175),
                                             new System.Windows.Media.FontFamily("Gautami"));
                Log.Information(message);

                if (InventoryManager.GetBagByInventoryBagId(InventoryBagId.Armory_MainHand).FreeSlots < 1)
                {
                    Log.Error("You have no free slots in your MainHand armory so we can't equip a new weapon.");
                    return;
                }

                if (!await PurchaseWeapon((uint)weapon))
                {
                    Log.Error($"Could not purchase {DataManager.GetItem((uint)weapon).CurrentLocaleName}");
                    return;
                }

                item = FindOwnedItem((uint)weapon, EquipmentSlot.MainHand);
            }
            else
            {
                Log.Information($"Already own {item.EnglishName}; skipping the purchase.");
            }

            Log.Information($"Found Item {item}");
            if (item != null)
            {
                if (item.BagId != InventoryBagId.EquippedItems || item.Slot != EquipSlot.Slot)
                {
                    item.Move(EquipSlot);
                    await Coroutine.Sleep(250);
                }

                if (UpdateGearSet)
                {
                    await LlamaLibrary.ScriptConditions.Helpers.UpdateGearSet();
                }
            }

            if (Core.Me.CurrentJob == ClassJobType.Paladin)
            {
                await EnsurePaladinShield();
            }
        }

        private async Task EnsurePaladinShield()
        {
            var shieldId = (uint)SquareMapleShield;
            var equipSlot = InventoryManager
                .GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.OffHand];
            var item = FindOwnedItem(shieldId, EquipmentSlot.OffHand);

            if (item == null)
            {
                Log.Information($"Attempting to purchase {DataManager.GetItem(shieldId).CurrentLocaleName} with RebornBuddy");

                if (InventoryManager.GetBagByInventoryBagId(InventoryBagId.Armory_OffHand).FreeSlots < 1)
                {
                    Log.Error("You have no free slots in your OffHand armory so we can't equip a new shield.");
                    return;
                }

                if (!await PurchaseWeapon(shieldId))
                {
                    Log.Error($"Could not purchase {DataManager.GetItem(shieldId).CurrentLocaleName}");
                    return;
                }

                item = FindOwnedItem(shieldId, EquipmentSlot.OffHand);
            }
            else
            {
                Log.Information($"Already own {item.EnglishName}; skipping the purchase.");
            }

            Log.Information($"Found Item {item}");
            if (item == null)
            {
                return;
            }

            if (item.BagId != InventoryBagId.EquippedItems || item.Slot != equipSlot.Slot)
            {
                item.Move(equipSlot);
                await Coroutine.Sleep(250);
            }

            if (UpdateGearSet)
            {
                await LlamaLibrary.ScriptConditions.Helpers.UpdateGearSet();
            }
        }

        private static BagSlot FindOwnedItem(uint itemId, EquipmentSlot preferredEquipmentSlot)
        {
            var equippedSlot = InventoryManager
                .GetBagByInventoryBagId(InventoryBagId.EquippedItems)[preferredEquipmentSlot];

            if (equippedSlot.IsFilled && equippedSlot.RawItemId == itemId)
            {
                return equippedSlot;
            }

            return InventoryManager.FilledInventoryAndArmory
                .FirstOrDefault(slot => slot.RawItemId == itemId);
        }

        private static PurchaseLocation GetPurchaseLocation(int itemId)
        {
            if (DiscipleOfWarWeapons.Contains(itemId))
            {
                var levelRange = itemId == 10400 || itemId == 10462 ? "30-39" : "1-9";
                return new PurchaseLocation(Faezghim, "Purchase Disciple of War Arms", $"Purchase Weapons (Lv. {levelRange})");
            }

            if (DiscipleOfMagicWeapons.Contains(itemId))
            {
                var levelRange = itemId == 10524 ? "30-39" : "1-9";
                return new PurchaseLocation(Faezghim, "Purchase Disciple of Magic Arms", $"Purchase Weapons (Lv. {levelRange})");
            }

            if (HandTools.Contains(itemId))
            {
                return new PurchaseLocation(Syneyhil, "Purchase Disciple of the Hand Tools", "Purchase Tools (Lv. 1-9)");
            }

            if (LandTools.Contains(itemId))
            {
                return new PurchaseLocation(Syneyhil, "Purchase Disciple of the Land Tools", "Purchase Tools (Lv. 1-9)");
            }

            if (itemId == 18046)
            {
                return new PurchaseLocation(Itto, "Purchase Weapons (Lv. 60)");
            }

            if (itemId == 18203)
            {
                return new PurchaseLocation(Itto, "Purchase Weapons (Lv. 62)");
            }

            if (itemId == 34091)
            {
                return new PurchaseLocation(ShaiTistt, "Purchase Weapons (Lv. 80)");
            }

            if (itemId == 25643 || itemId == 25644 || itemId == 35760 || itemId == 35778)
            {
                return new PurchaseLocation(ShaiTistt, "Purchase Weapons (Lv. 70)");
            }

            return null;
        }

        private async Task<bool> PurchaseWeapon(uint itemId)
        {
            var location = GetPurchaseLocation((int)itemId);
            if (location == null)
            {
                Log.Error($"No RebornBuddy vendor is configured for item {itemId}.");
                return false;
            }

            // A previous purchase (notably Paladin main hand followed by shield) can
            // return to a parent merchant menu. Always begin from a clean state.
            if (Shop.Open || Conversation.IsOpen)
            {
                await CloseVendorWindows();
            }

            var countBefore = InventoryManager.FilledInventoryAndArmory.Count(slot => slot.RawItemId == itemId);

            if (!await LlamaLibrary.Helpers.Navigation.GetToInteractNpcSelectString(location.Vendor))
            {
                Log.Error($"Could not reach or interact with {location.Vendor.Name}.");
                return false;
            }

            foreach (var menuEntry in location.MenuPath)
            {
                if (!await Coroutine.Wait(5000, () => Conversation.IsOpen))
                {
                    Log.Error($"The shop menu did not open at {location.Vendor.Name}.");
                    await CloseVendorWindows();
                    return false;
                }

                var previousMenu = string.Join("\n", Conversation.GetConversationList);
                if (!Conversation.SelectLineContains(menuEntry))
                {
                    Log.Error($"Could not find shop option '{menuEntry}' at {location.Vendor.Name}. Available options: {string.Join(" | ", Conversation.GetConversationList)}");
                    await CloseVendorWindows();
                    return false;
                }

                await Coroutine.Wait(5000, () => Shop.Open || !Conversation.IsOpen || string.Join("\n", Conversation.GetConversationList) != previousMenu);
                await Coroutine.Wait(5000, () => Shop.Open || Conversation.IsOpen);
            }

            if (!await Coroutine.Wait(5000, () => Shop.Open))
            {
                Log.Error($"The requested shop did not open at {location.Vendor.Name}.");
                await CloseVendorWindows();
                return false;
            }

            if (!Shop.Items.Any(shopItem => shopItem.ItemId == itemId))
            {
                Log.Error($"{location.Vendor.Name}'s selected shop does not sell {DataManager.GetItem(itemId).CurrentLocaleName}.");
                await CloseVendorWindows();
                return false;
            }

            Log.Information($"Purchasing {DataManager.GetItem(itemId).CurrentLocaleName} from {location.Vendor.Name}.");
            Shop.Purchase(itemId, 1);

            if (!await Coroutine.Wait(5000, () => SelectYesno.IsOpen))
            {
                Log.Error("The purchase confirmation window did not open.");
                await CloseVendorWindows();
                return false;
            }

            SelectYesno.Yes();
            await Coroutine.Wait(5000, () => !SelectYesno.IsOpen);
            await Coroutine.Wait(5000, () => InventoryManager.FilledInventoryAndArmory.Count(slot => slot.RawItemId == itemId) > countBefore);
            await Coroutine.Sleep(500);
            await CloseVendorWindows();

            return InventoryManager.FilledInventoryAndArmory.Count(slot => slot.RawItemId == itemId) > countBefore;
        }

        private async Task CloseVendorWindows()
        {
            // Closing a shop returns to its parent SelectString menu. Walk back through
            // every parent menu by selecting the final Cancel/Exit entry. Require the
            // windows to remain closed briefly so transitions cannot fool the cleanup.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (Shop.Open)
                {
                    Shop.Close();
                    await Coroutine.Wait(3000, () => !Shop.Open);
                    await Coroutine.Sleep(350);
                    continue;
                }

                if (Conversation.IsOpen)
                {
                    var previousMenu = string.Join("\n", Conversation.GetConversationList);
                    Conversation.SelectQuit();

                    await Coroutine.Wait(3000, () => !Conversation.IsOpen || string.Join("\n", Conversation.GetConversationList) != previousMenu);
                    await Coroutine.Sleep(350);
                    continue;
                }

                await Coroutine.Sleep(500);
                if (!Shop.Open && !Conversation.IsOpen)
                {
                    return;
                }
            }

            if (Shop.Open || Conversation.IsOpen)
            {
                Log.Warning($"Could not completely close the vendor windows. Shop open: {Shop.Open}; remaining options: {string.Join(" | ", Conversation.GetConversationList)}");
            }
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => ChangeJob());
        }
    }
}
