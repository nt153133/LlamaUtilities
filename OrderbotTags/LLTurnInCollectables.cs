using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot.Managers;
using LlamaLibrary.Extensions;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLTurnInCollectables")]
    public class LLTurnInCollectables : LLProfileBehavior
    {

        // [XmlAttribute("Vendor")]
        //[XmlAttribute("vendor")]
        //[DefaultValue(1)]
        // public int vendor { get; set; }

        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        protected override Color LogColor => Colors.Chartreuse;

        public LLTurnInCollectables() : base() { }

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
            return new ActionRunCoroutine(r => CollectableTurninTask());
        }

        private async Task CollectableTurninTask()
        {
            Log.Information("Turning in Collectables");
            await CollectableTurnin();

            _isDone = true;
        }

        private async Task<bool> CollectableTurnin()
        {
            var collectables = InventoryManager.FilledSlots.Where(i => i.IsCollectable).Select(x => x.RawItemId)
                .Distinct();
            var collectablesAll = InventoryManager.FilledSlots.Where(i => i.IsCollectable);

            if (collectables.Any(i => CollectableConstants.CollectableItems.Keys.Contains(i)))
            {
                Log.Information("Have collectables");
                foreach (var collectable in collectablesAll)
                {
                    if (CollectableConstants.CollectableItems.Keys.Contains(collectable.RawItemId))
                    {
                        var turnin = CollectableConstants.CollectableItems[collectable.RawItemId];
                        if (collectable.Collectability < turnin.MinCol)
                        {
                            Log.Information(
                                $"Discarding {collectable.Name} is at {collectable.Collectability} which is under {turnin.MinCol}");
                            collectable.Discard();
                        }
                    }
                }

                collectables = InventoryManager.FilledSlots.Where(i => i.IsCollectable && CollectableConstants.CollectableItems.Keys.Contains(i.RawItemId)).Select(x => x.RawItemId)
                    .Distinct();

                if (!CollectablesShop.Instance.IsOpen)
                {
                    // Radz-At-Han
                    await Navigation.GetToInteractNpc(1037306, 963, new Vector3(25.20608f, 0.9200001f, -70.4108f),
                                                      LlamaLibrary.RemoteWindows.CollectablesShop.Instance);
                    // Elmore
                    //					if (vendor == 2)
                    //					{
                    //await Navigation.GetToInteractNpc(1027542, 820, new Vector3(17.66549f, 82.05f, -18.34973f),
                    //					LlamaLibrary.RemoteWindows.CollectablesShop.Instance);
                    //					}
                }

                if (!CollectablesShop.Instance.IsOpen)
                {
                    Log.Information("Window not open");
                    return false;
                }


                foreach (var collectable in collectables)
                {
                    var item = CollectableConstants.CollectableItems[collectable];

                    CollectablesShop.Instance.SelectJob(item.Class);
                    await Coroutine.Sleep(1000);
                    CollectablesShop.Instance.SelectItem(CollectablesShop.Instance.GetItems()
                        .First(i => i.ItemId == item.ItemId).Line);
                    await Coroutine.Sleep(1000);
                    var count = CollectablesShop.Instance.TurninCount;
                    var currency = CurrencyHelper.GetCurrencyItemId((uint)item.Currency);

                    for (var i = 0; i < count; i++)
                    {
                        if (CurrencyHelper.GetAmountOfCurrency(currency) + item.Reward > 2000)
                        {
                            break;
                        }

                        var oldCount = CollectablesShop.Instance.TurninCount;
                        CollectablesShop.Instance.Trade();
                        await Coroutine.Wait(5000, () => CollectablesShop.Instance.TurninCount != oldCount);
                        await Coroutine.Sleep(200);
                    }
                }

                if (CollectablesShop.Instance.IsOpen)
                {
                    CollectablesShop.Instance.Close();
                    await Coroutine.Wait(5000, () => !CollectablesShop.Instance.IsOpen);
                }
            }

            return true;
        }

    }

    public static class CollectableConstants
    {
        public static Dictionary<uint, (uint ItemId, int Class, int Index, int MinCol, int Currency, int Reward)>
            CollectableItems =
                new Dictionary<uint, (uint ItemId, int Class, int Index, int MinCol, int Currency, int Reward)>()
                {
                    {30970, (30970, 0, 27, 110, 2, 54)}, //Rarefied Cedar Longbow
                    {30971, (30971, 0, 26, 117, 2, 54)}, //Rarefied Cedar Fishing Rod
                    {30972, (30972, 0, 25, 125, 2, 54)}, //Rarefied Holy Cedar Spinning Wheel
                    {30973, (30973, 0, 24, 133, 2, 54)}, //Rarefied Dark Chestnut Rod
                    {30974, (30974, 0, 23, 140, 2, 54)}, //Rarefied Hallowed Chestnut Ring
                    {30975, (30975, 0, 22, 148, 2, 54)}, //Rarefied Birch Signum
                    {30976, (30976, 0, 21, 158, 2, 54)}, //Rarefied Beech Composite Bow
                    {30977, (30977, 0, 20, 168, 2, 54)}, //Rarefied Larch Necklace
                    {30978, (30978, 0, 19, 178, 2, 54)}, //Rarefied Pine Cane
                    {30979, (30979, 0, 18, 188, 2, 54)}, //Rarefied Persimmon Bracelets
                    {30980, (30980, 0, 17, 198, 2, 54)}, //Rarefied Zelkova Spinning Wheel
                    {30981, (30981, 0, 16, 209, 2, 68)}, //Rarefied White Oak Partisan
                    {30982, (30982, 0, 15, 220, 2, 82)}, //Rarefied Applewood Staff
                    {30983, (30983, 0, 14, 231, 2, 85)}, //Rarefied White Ash Earrings
                    {30984, (30984, 0, 13, 242, 2, 99)}, //Rarefied Sandteak Fauchard
                    {31652, (31652, 0, 12, 253, 2, 114)}, //Rarefied Lignum Vitae Grinding Wheel
                    {35626, (35626, 0, 11, 286, 2, 128)}, //Rarefied Horse Chestnut Kasa
                    {35627, (35627, 0, 10, 341, 2, 142)}, //Rarefied Palm Bracelet
                    {35628, (35628, 0, 9, 368, 2, 157)}, //Rarefied Red Pine Spinning Wheel
                    {35629, (35629, 0, 8, 379, 2, 171)}, //Rarefied Ironwood Grinding Wheel
                    {35630, (35630, 0, 7, 390, 2, 198)}, //Rarefied Integral Armillae
                    {36619, (36619, 0, 6, 396, 6, 144)}, //Rarefied Integral Fishing Rod
                    {44185, (44185, 0, 5, 451, 2, 128)}, //Rarefied Ginseng Earrings
                    {44186, (44186, 0, 4, 495, 2, 142)}, //Rarefied Ceiba Spear
                    {44187, (44187, 0, 3, 539, 2, 157)}, //Rarefied Dark Mahogany Necklace
                    {44188, (44188, 0, 2, 583, 2, 171)}, //Rarefied Acacia Rod
                    {44189, (44189, 0, 1, 627, 2, 198)}, //Rarefied Claro Walnut Grinding Wheel
                    {44190, (44190, 0, 0, 660, 6, 144)}, //Rarefied Claro Walnut Fishing Rod
                    {30985, (30985, 1, 27, 110, 2, 54)}, //Rarefied Mythrite Katzbalger
                    {30986, (30986, 1, 26, 117, 2, 54)}, //Rarefied Mythrite Pugiones
                    {30987, (30987, 1, 25, 125, 2, 54)}, //Rarefied Mythrite Halfheart Saw
                    {30988, (30988, 1, 24, 133, 2, 54)}, //Rarefied Titanium Creasing Knife
                    {30989, (30989, 1, 23, 140, 2, 54)}, //Rarefied Titanium Mortar
                    {30990, (30990, 1, 22, 148, 2, 54)}, //Rarefied Adamantite Bill
                    {30991, (30991, 1, 21, 158, 2, 54)}, //Rarefied High Steel Guillotine
                    {30992, (30992, 1, 20, 168, 2, 54)}, //Rarefied High Steel Claw Hammer
                    {30993, (30993, 1, 19, 178, 2, 54)}, //Rarefied Doman Iron Uchigatana
                    {30994, (30994, 1, 18, 188, 2, 54)}, //Rarefied Doman Steel Patas
                    {30995, (30995, 1, 17, 198, 2, 54)}, //Rarefied Molybdenum Pliers
                    {30996, (30996, 1, 16, 209, 2, 68)}, //Rarefied Deepgold Anelace
                    {30997, (30997, 1, 15, 220, 2, 82)}, //Rarefied Deepgold Culinary Knife
                    {30998, (30998, 1, 14, 231, 2, 85)}, //Rarefied Bluespirit Gunblade
                    {30999, (30999, 1, 13, 242, 2, 99)}, //Rarefied Titanbronze Pickaxe
                    {31653, (31653, 1, 12, 253, 2, 114)}, //Rarefied Mythril Hatchet
                    {35631, (35631, 1, 11, 286, 2, 128)}, //Rarefied High Durium Pistol
                    {35632, (35632, 1, 10, 341, 2, 142)}, //Rarefied High Durium Greatsword
                    {35633, (35633, 1, 9, 368, 2, 157)}, //Rarefied Bismuth Sledgehammer
                    {35634, (35634, 1, 8, 379, 2, 171)}, //Rarefied Manganese Cross-pein Hammer
                    {35635, (35635, 1, 7, 390, 2, 198)}, //Rarefied Chondrite Culinary Knife
                    {36620, (36620, 1, 6, 396, 6, 144)}, //Rarefied Chondrite Lapidary Hammer
                    {44191, (44191, 1, 5, 451, 2, 128)}, //Rarefied Mountain Chromite Fists
                    {44192, (44192, 1, 4, 495, 2, 142)}, //Rarefied Ruthenium War Axe
                    {44193, (44193, 1, 3, 539, 2, 157)}, //Rarefied Cobalt Tungsten Scimitars
                    {44194, (44194, 1, 2, 583, 2, 171)}, //Rarefied Titanium Gold Mortar
                    {44195, (44195, 1, 1, 627, 2, 198)}, //Rarefied Ra'Kaznar War Scythe
                    {44196, (44196, 1, 0, 660, 6, 144)}, //Rarefied Ra'Kaznar Round Knife
                    {31000, (31000, 2, 27, 110, 2, 54)}, //Rarefied Mythrite Sallet
                    {31001, (31001, 2, 26, 117, 2, 54)}, //Rarefied Mythrite Hauberk
                    {31002, (31002, 2, 25, 125, 2, 54)}, //Rarefied Mythrite Bladed Lantern Shield
                    {31003, (31003, 2, 24, 133, 2, 54)}, //Rarefied Titanium Frypan
                    {31004, (31004, 2, 23, 140, 2, 54)}, //Rarefied Titanium Vambraces
                    {31005, (31005, 2, 22, 148, 2, 54)}, //Rarefied Adamantite Scutum
                    {31006, (31006, 2, 21, 158, 2, 54)}, //Rarefied High Steel Thermal Alembic
                    {31007, (31007, 2, 20, 168, 2, 54)}, //Rarefied High Steel Plate Belt
                    {31008, (31008, 2, 19, 178, 2, 54)}, //Rarefied Doman Iron Greaves
                    {31009, (31009, 2, 18, 188, 2, 54)}, //Rarefied Doman Steel Tabard
                    {31010, (31010, 2, 17, 198, 2, 54)}, //Rarefied Molybdenum Headgear
                    {31011, (31011, 2, 16, 209, 2, 68)}, //Rarefied Deepgold Cuirass
                    {31012, (31012, 2, 15, 220, 2, 82)}, //Rarefied Deepgold Wings
                    {31013, (31013, 2, 14, 231, 2, 85)}, //Rarefied Bluespirit Gauntlets
                    {31014, (31014, 2, 13, 242, 2, 99)}, //Rarefied Titanbronze Tower Shield
                    {31654, (31654, 2, 12, 253, 2, 114)}, //Rarefied Mythril Alembic
                    {35636, (35636, 2, 11, 286, 2, 128)}, //Rarefied High Durium Knuckles
                    {35637, (35637, 2, 10, 341, 2, 142)}, //Rarefied High Durium Kite Shield
                    {35638, (35638, 2, 9, 368, 2, 157)}, //Rarefied Bismuth Fat Cat Frypan
                    {35639, (35639, 2, 8, 379, 2, 171)}, //Rarefied Manganese Armor of the Behemoth King
                    {35640, (35640, 2, 7, 390, 2, 198)}, //Rarefied Chondrite Sollerets
                    {36621, (36621, 2, 6, 396, 6, 144)}, //Rarefied Chondrite Alembic
                    {44197, (44197, 2, 5, 451, 2, 128)}, //Rarefied Mountain Chromite Alembic
                    {44198, (44198, 2, 4, 495, 2, 142)}, //Rarefied Ruthenium Sabatons
                    {44199, (44199, 2, 3, 539, 2, 157)}, //Rarefied Cobalt Tungsten Chocobo Frypan
                    {44200, (44200, 2, 2, 583, 2, 171)}, //Rarefied Titanium Gold Thorned Corselet
                    {44201, (44201, 2, 1, 627, 2, 198)}, //Rarefied Ra'Kaznar Greaves
                    {44202, (44202, 2, 0, 660, 6, 144)}, //Rarefied Ra'Kaznar Ring
                    {31015, (31015, 3, 27, 110, 2, 54)}, //Rarefied Mythrite Goggles
                    {31016, (31016, 3, 26, 117, 2, 54)}, //Rarefied Mythrite Bangle
                    {31017, (31017, 3, 25, 125, 2, 54)}, //Rarefied Mythrite Needle
                    {31018, (31018, 3, 24, 133, 2, 54)}, //Rarefied Hardsilver Monocle
                    {31019, (31019, 3, 23, 140, 2, 54)}, //Rarefied Hardsilver Pole
                    {31020, (31020, 3, 22, 148, 2, 54)}, //Rarefied Aurum Regis Earrings
                    {31021, (31021, 3, 21, 158, 2, 54)}, //Rarefied Koppranickel Planisphere
                    {31022, (31022, 3, 20, 168, 2, 54)}, //Rarefied Koppranickel Necklace
                    {31023, (31023, 3, 19, 178, 2, 54)}, //Rarefied Durium Chaplets
                    {31024, (31024, 3, 18, 188, 2, 54)}, //Rarefied Durium Rod
                    {31025, (31025, 3, 17, 198, 2, 54)}, //Rarefied Palladium Needle
                    {31026, (31026, 3, 16, 209, 2, 68)}, //Rarefied Stonegold Degen
                    {31027, (31027, 3, 15, 220, 2, 82)}, //Rarefied Stonegold Orrery
                    {31028, (31028, 3, 14, 231, 2, 85)}, //Rarefied Manasilver Ear Cuffs
                    {31029, (31029, 3, 13, 242, 2, 99)}, //Rarefied Titanbronze Headgear
                    {31655, (31655, 3, 12, 253, 2, 114)}, //Rarefied Mythril Ring
                    {35641, (35641, 3, 11, 286, 2, 128)}, //Rarefied High Durium Milpreves
                    {35642, (35642, 3, 10, 341, 2, 142)}, //Rarefied Pewter Choker
                    {35643, (35643, 3, 9, 368, 2, 157)}, //Rarefied Phrygian Earring
                    {35644, (35644, 3, 8, 379, 2, 171)}, //Rarefied Manganese Horn of the Last Unicorn
                    {35645, (35645, 3, 7, 390, 2, 198)}, //Rarefied Star Quartz Choker
                    {36622, (36622, 3, 6, 396, 6, 144)}, //Rarefied Chondrite Needle
                    {44203, (44203, 3, 5, 451, 2, 128)}, //Rarefied Lar Longbow
                    {44204, (44204, 3, 4, 495, 2, 142)}, //Rarefied Ihuykanite Circlet
                    {44205, (44205, 3, 3, 539, 2, 157)}, //Rarefied Cobalt Tungsten Tuck
                    {44206, (44206, 3, 2, 583, 2, 171)}, //Rarefied White Gold Choker
                    {44207, (44207, 3, 1, 627, 2, 198)}, //Rarefied Ra'Kaznar Orrery
                    {44208, (44208, 3, 0, 660, 6, 144)}, //Rarefied Black Star Earrings
                    {31030, (31030, 4, 27, 110, 2, 54)}, //Rarefied Archaeoskin Belt
                    {31031, (31031, 4, 26, 117, 2, 54)}, //Rarefied Archaeoskin Cloche
                    {31032, (31032, 4, 25, 125, 2, 54)}, //Rarefied Wyvernskin Mask
                    {31033, (31033, 4, 24, 133, 2, 54)}, //Rarefied Dhalmelskin Coat
                    {31034, (31034, 4, 23, 140, 2, 54)}, //Rarefied Dragonskin Ring
                    {31035, (31035, 4, 22, 148, 2, 54)}, //Rarefied Serpentskin Hat
                    {31036, (31036, 4, 21, 158, 2, 54)}, //Rarefied Gaganaskin Shoes
                    {31037, (31037, 4, 20, 168, 2, 54)}, //Rarefied Gyuki Leather Jacket
                    {31038, (31038, 4, 19, 178, 2, 54)}, //Rarefied Tigerskin Tricorne
                    {31039, (31039, 4, 18, 188, 2, 54)}, //Rarefied Marid Leather Corset
                    {31040, (31040, 4, 17, 198, 2, 54)}, //Rarefied Gazelleskin Armguards
                    {31041, (31041, 4, 16, 209, 2, 68)}, //Rarefied Smilodonskin Trousers
                    {31042, (31042, 4, 15, 220, 2, 82)}, //Rarefied Gliderskin Thighboots
                    {31043, (31043, 4, 14, 231, 2, 85)}, //Rarefied Atrociraptorskin Cap
                    {31044, (31044, 4, 13, 242, 2, 99)}, //Rarefied Zonureskin Fingerless Gloves
                    {31656, (31656, 4, 12, 253, 2, 114)}, //Rarefied Swallowskin Coat
                    {35646, (35646, 4, 11, 286, 2, 128)}, //Rarefied Gajaskin Shoes
                    {35647, (35647, 4, 10, 341, 2, 142)}, //Rarefied Luncheon Toadskin Hose
                    {35648, (35648, 4, 9, 368, 2, 157)}, //Rarefied Saigaskin Gloves
                    {35649, (35649, 4, 8, 379, 2, 171)}, //Rarefied Kumbhiraskin Shoes
                    {35650, (35650, 4, 7, 390, 2, 198)}, //Rarefied Ophiotauroskin Top
                    {36623, (36623, 4, 6, 396, 6, 144)}, //Rarefied Ophiotauroskin Halfgloves
                    {44209, (44209, 4, 5, 451, 2, 128)}, //Rarefied Gargantuaskin Hat
                    {44210, (44210, 4, 4, 495, 2, 142)}, //Rarefied Gargantuaskin Trousers
                    {44211, (44211, 4, 3, 539, 2, 157)}, //Rarefied Gomphotherium Brais
                    {44212, (44212, 4, 2, 583, 2, 171)}, //Rarefied Br'aaxskin Armlets
                    {44213, (44213, 4, 1, 627, 2, 198)}, //Rarefied Crocodileskin Leggings
                    {44214, (44214, 4, 0, 660, 6, 144)}, //Rarefied Loboskin Fingerless Gloves
                    {31045, (31045, 5, 27, 110, 2, 54)}, //Rarefied Rainbow Bolero
                    {31046, (31046, 5, 26, 117, 2, 54)}, //Rarefied Rainbow Ribbon
                    {31047, (31047, 5, 25, 125, 2, 54)}, //Rarefied Holy Rainbow Hat
                    {31048, (31048, 5, 24, 133, 2, 54)}, //Rarefied Ramie Turban
                    {31049, (31049, 5, 23, 140, 2, 54)}, //Rarefied Hallowed Ramie Doublet
                    {31050, (31050, 5, 22, 148, 2, 54)}, //Rarefied Chimerical Felt Cyclas
                    {31051, (31051, 5, 21, 158, 2, 54)}, //Rarefied Bloodhempen Skirt
                    {31052, (31052, 5, 20, 168, 2, 54)}, //Rarefied Ruby Cotton Gilet
                    {31053, (31053, 5, 19, 178, 2, 54)}, //Rarefied Kudzu Hat
                    {31054, (31054, 5, 18, 188, 2, 54)}, //Rarefied Serge Hose
                    {31055, (31055, 5, 17, 198, 2, 54)}, //Rarefied Twinsilk Apron
                    {31056, (31056, 5, 16, 209, 2, 68)}, //Rarefied Brightlinen Himation
                    {31057, (31057, 5, 15, 220, 2, 82)}, //Rarefied Iridescent Top
                    {31058, (31058, 5, 14, 231, 2, 85)}, //Rarefied Pixie Cotton Hood
                    {31059, (31059, 5, 13, 242, 2, 99)}, //Rarefied Ovim Wool Tunic
                    {31657, (31657, 5, 12, 253, 2, 114)}, //Rarefied Dwarven Cotton Beret
                    {35651, (35651, 5, 11, 286, 2, 128)}, //Rarefied Darkhempen Hat
                    {35652, (35652, 5, 10, 341, 2, 142)}, //Rarefied Almasty Serge Gloves
                    {35653, (35653, 5, 9, 368, 2, 157)}, //Rarefied Snow Linen Doublet
                    {35654, (35654, 5, 8, 379, 2, 171)}, //Rarefied Scarlet Moko Wedge Cap
                    {35655, (35655, 5, 7, 390, 2, 198)}, //Rarefied AR-Caean Velvet Bottoms
                    {36624, (36624, 5, 6, 396, 6, 144)}, //Rarefied AR-Caean Velvet Work Cap
                    {44215, (44215, 5, 5, 451, 2, 128)}, //Rarefied Snow Cotton Beret
                    {44216, (44216, 5, 4, 495, 2, 142)}, //Rarefied Mountain Linen Top
                    {44217, (44217, 5, 3, 539, 2, 157)}, //Rarefied Sarcenet Kecks
                    {44218, (44218, 5, 2, 583, 2, 171)}, //Rarefied Rroneek Serge Hat
                    {44219, (44219, 5, 1, 627, 2, 198)}, //Rarefied Thunderyards Silk Gloves
                    {44220, (44220, 5, 0, 660, 6, 144)}, //Rarefied Thunderyards Silk Culottes
                    {31060, (31060, 6, 27, 110, 2, 54)}, //Rarefied Archaeoskin Grimoire
                    {31061, (31061, 6, 26, 117, 2, 54)}, //Rarefied Archaeoskin Codex
                    {31062, (31062, 6, 25, 125, 2, 54)}, //Rarefied Dissolvent
                    {31063, (31063, 6, 24, 133, 2, 54)}, //Rarefied Dhalmelskin Codex
                    {31064, (31064, 6, 23, 140, 2, 54)}, //Rarefied Max-Potion
                    {31065, (31065, 6, 22, 148, 2, 54)}, //Rarefied Book of Aurum Regis
                    {31066, (31066, 6, 21, 158, 2, 54)}, //Rarefied Koppranickel Index
                    {31067, (31067, 6, 20, 168, 2, 54)}, //Rarefied Reisui
                    {31068, (31068, 6, 19, 178, 2, 54)}, //Rarefied Tigerskin Grimoire
                    {31069, (31069, 6, 18, 188, 2, 54)}, //Rarefied Growth Formula
                    {31070, (31070, 6, 17, 198, 2, 54)}, //Rarefied Gazelleskin Codex
                    {31071, (31071, 6, 16, 209, 2, 68)}, //Rarefied Alkahest
                    {31072, (31072, 6, 15, 220, 2, 82)}, //Rarefied Gliderskin Grimoire
                    {31073, (31073, 6, 14, 231, 2, 85)}, //Rarefied Bluespirit Codex
                    {31074, (31074, 6, 13, 242, 2, 99)}, //Rarefied Syrup
                    {31658, (31658, 6, 12, 253, 2, 114)}, //Rarefied Dwarven Mythril Grimoire
                    {35656, (35656, 6, 11, 286, 2, 128)}, //Rarefied Gajaskin Codex
                    {35657, (35657, 6, 10, 341, 2, 142)}, //Rarefied Luncheon Toadskin Grimoire
                    {35658, (35658, 6, 9, 368, 2, 157)}, //Rarefied Moon Gel
                    {35659, (35659, 6, 8, 379, 2, 171)}, //Rarefied Enchanted Manganese Ink
                    {35660, (35660, 6, 7, 390, 2, 198)}, //Rarefied Draught
                    {36625, (36625, 6, 6, 396, 6, 144)}, //Rarefied Ophiotauroskin Magitek Codex
                    {44221, (44221, 6, 5, 451, 2, 128)}, //Rarefied Loboskin Grimoire
                    {44222, (44222, 6, 4, 495, 2, 142)}, //Rarefied Gemsap of Dexterity
                    {44223, (44223, 6, 3, 539, 2, 157)}, //Rarefied Br'aaxskin Codex
                    {44224, (44224, 6, 2, 583, 2, 171)}, //Rarefied Cunning Craftsman's Tisane
                    {44225, (44225, 6, 1, 627, 2, 198)}, //Rarefied Gemdraught of Vitality
                    {44226, (44226, 6, 0, 660, 6, 144)}, //Rarefied Claro Walnut Flat Brush
                    {31075, (31075, 7, 27, 110, 2, 54)}, //Rarefied Dhalmel Gratin
                    {31076, (31076, 7, 26, 117, 2, 54)}, //Rarefied Sohm Al Tart
                    {31077, (31077, 7, 25, 125, 2, 54)}, //Rarefied Sauteed Porcini
                    {31078, (31078, 7, 24, 133, 2, 54)}, //Rarefied Royal Eggs
                    {31079, (31079, 7, 23, 140, 2, 54)}, //Rarefied Peperoncino
                    {31080, (31080, 7, 22, 148, 2, 54)}, //Rarefied Marron Glace
                    {31081, (31081, 7, 21, 158, 2, 54)}, //Rarefied Baklava
                    {31082, (31082, 7, 20, 168, 2, 54)}, //Rarefied Shorlog
                    {31083, (31083, 7, 19, 178, 2, 54)}, //Rarefied Tempura Platter
                    {31084, (31084, 7, 18, 188, 2, 54)}, //Rarefied Persimmon Pudding
                    {31085, (31085, 7, 17, 198, 2, 54)}, //Rarefied Chirashi-zushi
                    {31086, (31086, 7, 16, 209, 2, 68)}, //Rarefied Grilled Rail
                    {31087, (31087, 7, 15, 220, 2, 82)}, //Rarefied Spaghetti al Nero
                    {31088, (31088, 7, 14, 231, 2, 85)}, //Rarefied Popotoes au Gratin
                    {31089, (31089, 7, 13, 242, 2, 99)}, //Rarefied Espresso con Panna
                    {31659, (31659, 7, 12, 253, 2, 114)}, //Rarefied Lemonade
                    {35661, (35661, 7, 11, 286, 2, 128)}, //Rarefied Archon Loaf
                    {35662, (35662, 7, 10, 341, 2, 142)}, //Rarefied King Crab Cake
                    {35663, (35663, 7, 9, 368, 2, 157)}, //Rarefied Happiness Juice
                    {35664, (35664, 7, 8, 379, 2, 171)}, //Rarefied Giant Haddock Dip
                    {35665, (35665, 7, 7, 390, 2, 198)}, //Rarefied Giant Popoto Pancakes
                    {36626, (36626, 7, 6, 396, 6, 144)}, //Rarefied Sykon Bavarois
                    {44227, (44227, 7, 5, 451, 2, 128)}, //Rarefied Boiled Alpaca Steak
                    {44228, (44228, 7, 4, 495, 2, 142)}, //Rarefied Banana Ponzecake
                    {44229, (44229, 7, 3, 539, 2, 157)}, //Rarefied Turali Pineapple Ponzecake
                    {44230, (44230, 7, 2, 583, 2, 171)}, //Rarefied Salmon Jerky
                    {44231, (44231, 7, 1, 627, 2, 198)}, //Rarefied Stuffed Peppers
                    {44232, (44232, 7, 0, 660, 6, 144)}, //Rarefied Tacos de Carne Asada
                    {32988, (32988, 8, 31, 400, 4, 15)}, //Rarefied Mythrite Sand
                    {32970, (32970, 8, 30, 600, 4, 20)}, //Rarefied Pyrite
                    {32971, (32971, 8, 29, 600, 4, 20)}, //Rarefied Chalcocite
                    {32972, (32972, 8, 28, 600, 4, 20)}, //Rarefied Limonite
                    {32973, (32973, 8, 27, 600, 4, 20)}, //Rarefied Abalathian Spring Water
                    {32974, (32974, 8, 26, 600, 4, 20)}, //Rarefied Aurum Regis Sand
                    {32989, (32989, 8, 25, 400, 4, 15)}, //Rarefied Gyr Abanian Mineral Water
                    {32975, (32975, 8, 24, 600, 4, 20)}, //Rarefied Raw Triphane
                    {32976, (32976, 8, 23, 600, 4, 20)}, //Rarefied Raw Star Spinel
                    {32977, (32977, 8, 22, 600, 4, 20)}, //Rarefied Raw Kyanite
                    {32978, (32978, 8, 21, 600, 4, 20)}, //Rarefied Raw Azurite
                    {32979, (32979, 8, 20, 600, 4, 20)}, //Rarefied Silvergrace Ore
                    {32990, (32990, 8, 19, 400, 4, 15)}, //Rarefied Bluespirit Ore
                    {32980, (32980, 8, 18, 600, 4, 23)}, //Rarefied Titancopper Ore
                    {32981, (32981, 8, 17, 600, 4, 23)}, //Rarefied Raw Lazurite
                    {32982, (32982, 8, 16, 600, 4, 23)}, //Rarefied Raw Petalite
                    {32983, (32983, 8, 15, 600, 4, 25)}, //Rarefied Sea Salt
                    {32984, (32984, 8, 14, 600, 4, 28)}, //Rarefied Reef Rock
                    {32991, (32991, 8, 13, 400, 4, 20)}, //Rarefied Manasilver Sand
                    {32985, (32985, 8, 12, 600, 4, 30)}, //Rarefied Raw Onyx
                    {32986, (32986, 8, 11, 600, 4, 31)}, //Rarefied Tungsten Ore
                    {32987, (32987, 8, 10, 600, 4, 31)}, //Rarefied Gyr Abanian Alumen
                    {36291, (36291, 8, 9, 400, 4, 20)}, //Rarefied High Durium Ore
                    {36293, (36293, 8, 8, 600, 4, 32)}, //Rarefied Raw Ametrine
                    {36294, (36294, 8, 7, 600, 4, 34)}, //Rarefied Bismuth Ore
                    {36295, (36295, 8, 6, 600, 4, 36)}, //Rarefied Sharlayan Rock Salt
                    {36296, (36296, 8, 5, 600, 4, 40)}, //Rarefied Phrygian Ore
                    {36297, (36297, 8, 4, 600, 4, 42)}, //Rarefied Blue Zircon
                    {36292, (36292, 8, 3, 400, 7, 20)}, //Rarefied Chloroschist
                    {36298, (36298, 8, 2, 600, 7, 38)}, //Rarefied Eblan Alumen
                    {36299, (36299, 8, 1, 600, 7, 40)}, //Rarefied Annite
                    {36300, (36300, 8, 0, 600, 7, 40)}, //Rarefied Pewter Ore
                    {33010, (33010, 9, 31, 400, 4, 15)}, //Rarefied Rainbow Cotton Boll
                    {32992, (32992, 9, 30, 600, 4, 20)}, //Rarefied Dark Chestnut Sap
                    {32993, (32993, 9, 29, 600, 4, 20)}, //Rarefied Dark Chestnut Log
                    {32994, (32994, 9, 28, 600, 4, 20)}, //Rarefied Dark Chestnut Branch
                    {32995, (32995, 9, 27, 600, 4, 20)}, //Rarefied Dark Chestnut
                    {32996, (32996, 9, 26, 600, 4, 20)}, //Rarefied Dark Chestnut Resin
                    {32997, (32997, 9, 25, 600, 4, 20)}, //Rarefied Larch Log
                    {33011, (33011, 9, 24, 400, 4, 15)}, //Rarefied Bloodhemp
                    {32998, (32998, 9, 23, 600, 4, 20)}, //Rarefied Shiitake Mushroom
                    {32999, (32999, 9, 22, 600, 4, 20)}, //Rarefied Larch Sap
                    {33000, (33000, 9, 21, 600, 4, 20)}, //Rarefied Pine Resin
                    {33001, (33001, 9, 20, 600, 4, 20)}, //Rarefied Pine Log
                    {33012, (33012, 9, 19, 400, 4, 15)}, //Rarefied Bright Flax
                    {33002, (33002, 9, 18, 600, 4, 23)}, //Rarefied Pixie Apple
                    {33003, (33003, 9, 17, 600, 4, 23)}, //Rarefied White Oak Log
                    {33004, (33004, 9, 16, 600, 4, 23)}, //Rarefied Miracle Apple Log
                    {33005, (33005, 9, 15, 600, 4, 25)}, //Rarefied Sandteak Log
                    {33006, (33006, 9, 14, 600, 4, 28)}, //Rarefied Kelp
                    {33013, (33013, 9, 13, 400, 4, 20)}, //Rarefied Night Pepper
                    {33007, (33007, 9, 12, 600, 4, 30)}, //Rarefied Amber Cloves
                    {33008, (33008, 9, 11, 600, 4, 31)}, //Rarefied Coral
                    {33009, (33009, 9, 10, 600, 4, 31)}, //Rarefied Urunday Log
                    {36301, (36301, 9, 9, 400, 4, 20)}, //Rarefied Thavnairian Perilla Leaf
                    {36303, (36303, 9, 8, 600, 4, 32)}, //Rarefied Palm Log
                    {36304, (36304, 9, 7, 600, 4, 34)}, //Rarefied Red Pine Log
                    {36305, (36305, 9, 6, 600, 4, 36)}, //Rarefied Coconut
                    {36306, (36306, 9, 5, 600, 4, 40)}, //Rarefied Sykon
                    {36307, (36307, 9, 4, 600, 4, 42)}, //Rarefied Dark Rye
                    {36302, (36302, 9, 3, 400, 7, 20)}, //Rarefied Ironwood Log
                    {36308, (36308, 9, 2, 600, 7, 38)}, //Rarefied Elder Nutmeg
                    {36309, (36309, 9, 1, 600, 7, 40)}, //Rarefied Iceberg Lettuce
                    {36310, (36310, 9, 0, 600, 7, 40)}, //Rarefied AR-Caean Cotton Boll
                };
    }
}