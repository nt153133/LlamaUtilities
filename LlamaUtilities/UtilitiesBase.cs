using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using ff14bot;
using ff14bot.AClasses;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Pathing.Service_Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary;
using LlamaLibrary.Enums;
using LlamaLibrary.Extensions;
using LlamaLibrary.Helpers;
using LlamaLibrary.Helpers.HousingTravel.Districts;
using LlamaLibrary.Logging;
using LlamaLibrary.Memory;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using LlamaLibrary.Utilities;
using LlamaUtilities.LlamaUtilities.Settings;
using LlamaUtilities.LlamaUtilities.Tasks;
using Newtonsoft.Json;
using TreeSharp;

namespace LlamaUtilities.LlamaUtilities
{
    public class UtilitiesBase : BotBase
    {
        public static readonly string _name = "Llama Utilities";
        private static readonly LLogger Log = new LLogger(_name, Colors.Pink);
        public static BotTask BotTask = new BotTask();

        public override bool WantButton => true;
        public override string Name => _name;
        public override PulseFlags PulseFlags => PulseFlags.All;
        public override bool IsAutonomous => true;
        public override bool RequiresProfile => false;
        public override Composite Root => _root;

        private Composite _root;
        private Utilities settings;

        public static bool IsBusy => DutyManager.InInstance || DutyManager.InQueue || DutyManager.DutyReady || Core.Me.IsCasting || Core.Me.IsMounted || Core.Me.InCombat || Talk.DialogOpen || MovementManager.IsMoving ||
                                     MovementManager.IsOccupied;

        private static readonly List<(string Name, uint ItemLevel, ItemRarity Rarity)> TrustDungeonDesynthList = new()
        {
            // 2.x A Realm Reborn
            // Grouped by dungeon-shared loot, plus some 2.x weapons have unique names
            // Sastasha, Tam-Tara Deepcroft, Copperbell Mines
            ("Acolyte's", 17, ItemRarity.Green),
            ("Foestriker's", 17, ItemRarity.Green),
            ("Plundered", 17, ItemRarity.Green),
            ("Warded Round Shield", 17, ItemRarity.Green),
            ("Nightprowler's Targe", 17, ItemRarity.Green),
            ("Pirate's", 17, ItemRarity.Green),
            ("Aetherial Brass", 16, ItemRarity.Pink),

            // Thousand Maws of Toto-Rak
            ("Warden's", 26, ItemRarity.Green),
            ("Torturer's", 26, ItemRarity.Green),
            ("Brigand's", 26, ItemRarity.Green),
            ("Ascetic's", 26, ItemRarity.Green),
            ("Nighthawk Visor", 26, ItemRarity.Green),
            ("Eternal Shade", 26, ItemRarity.Green),
            ("Antares Needles", 26, ItemRarity.Green),
            ("Bow of Owls", 26, ItemRarity.Green),
            ("Crimson Tide", 26, ItemRarity.Green),
            ("Elmlord's Tusk", 26, ItemRarity.Green),
            ("Frostbite", 26, ItemRarity.Green),
            ("Thalassian Shield", 26, ItemRarity.Green),
            ("Howling Talons", 26, ItemRarity.Green),
            ("Kple Kple", 26, ItemRarity.Green),
            ("Pupil's Book of Brass", 26, ItemRarity.Green),
            ("Taurus Staff", 26, ItemRarity.Green),
            ("Thalassian Targe", 26, ItemRarity.Green),

            // Haukke Manor
            ("Aetherial", 28, ItemRarity.Pink),
            ("Aetherial", 29, ItemRarity.Pink),
            ("Aetherial", 30, ItemRarity.Pink),
            ("Manor", 30, ItemRarity.Green),
            ("Shield of the Savage", 30, ItemRarity.Green),
            ("Charred Axe", 30, ItemRarity.Green),
            ("Inquisitor's Tuck", 30, ItemRarity.Green),
            ("Waning Sun Pelta", 30, ItemRarity.Green),
            ("Heart Snatchers", 30, ItemRarity.Green),
            ("Heart of House d'Arlendre", 30, ItemRarity.Green),
            ("Joukil's Guile", 30, ItemRarity.Green),
            ("Pupil's Leather Grimoire", 30, ItemRarity.Green),
            ("Tenfinger Tallstaff", 30, ItemRarity.Green),
            ("Tidesplitter", 30, ItemRarity.Green),
            ("Unbreakable Knuckles", 30, ItemRarity.Green),

            // Brayflox Longstop
            ("Longstop", 34, ItemRarity.Green),
            ("Battlemage", 34, ItemRarity.Green),
            ("Cavalry", 34, ItemRarity.Green),
            ("Infantry", 34, ItemRarity.Green),
            ("Gladiator's Ring", 34, ItemRarity.Green),
            ("Marauder's Ring", 34, ItemRarity.Green),
            ("Lancer's Ring", 34, ItemRarity.Green),
            ("Pugilist's Ring", 34, ItemRarity.Green),
            ("Rogue's Ring", 34, ItemRarity.Green),
            ("Archer's Ring", 34, ItemRarity.Green),
            ("Thaumaturge's Ring", 34, ItemRarity.Green),
            ("Arcanist's Ring", 34, ItemRarity.Green),
            ("Conjurer's Ring", 34, ItemRarity.Green),

            // The Stone Vigil
            ("Vigil", 43, ItemRarity.Green),
            ("Ancient Sword", 43, ItemRarity.Green),
            ("Ars Almadel", 43, ItemRarity.Green),
            ("Ars Notoria", 43, ItemRarity.Green),
            ("Blue Steel", 43, ItemRarity.Green),
            ("Capella", 43, ItemRarity.Green),
            ("Dryad Cane", 43, ItemRarity.Green),
            ("Eisentaenzer", 43, ItemRarity.Green),
            ("Jamadhars", 43, ItemRarity.Green),
            ("Jambiyas", 43, ItemRarity.Green),
            ("Keep of Saints", 43, ItemRarity.Green),
            ("Lockheart", 43, ItemRarity.Green),
            ("Shadow Bow", 43, ItemRarity.Green),
            ("Wyvern Spear", 43, ItemRarity.Green),

            // Snowcloak
            // Not sure about desynthing repurposed gear still available from tomes + MSQ
            //("Hero's", 90, ItemRarity.Blue),

            // The Keeper of the Lake
            ("Bogatyr's", 100, ItemRarity.Green),
            ("Picaroon's", 100, ItemRarity.Green),
            ("Varlet's", 100, ItemRarity.Green),

            // 3.x Heavensward
            // Grouped by dungeon-shared loot, plus some 3.x weapons have unique names
            // Sohm Al
            ("Woad", 130, ItemRarity.Green),
            ("Coffinmaker", 130, ItemRarity.Green),
            ("Destroyers", 130, ItemRarity.Green),
            ("Expunger", 130, ItemRarity.Green),
            ("Foolkiller", 130, ItemRarity.Green),
            ("Grandeur", 130, ItemRarity.Green),
            ("Guespiere", 130, ItemRarity.Green),
            ("Gunromaru", 130, ItemRarity.Green),
            ("Metamorphosis", 130, ItemRarity.Green),
            ("Ox Tongue", 130, ItemRarity.Green),
            ("Dissector", 130, ItemRarity.Green),
            ("Prester", 130, ItemRarity.Green),
            ("Renegades", 130, ItemRarity.Green),
            ("Skofnung", 130, ItemRarity.Green),
            ("The Red Pullet", 130, ItemRarity.Green),
            ("The Black Pullet", 130, ItemRarity.Green),

            // The Aery
            ("Orthodox", 136, ItemRarity.Green),
            ("Astrild", 136, ItemRarity.Green),
            ("Dainslaif", 136, ItemRarity.Green),
            ("Fagrskinna", 136, ItemRarity.Green),
            ("Gambanteinn", 136, ItemRarity.Green),
            ("Gjallarhorn", 136, ItemRarity.Green),
            ("Grasitha", 136, ItemRarity.Green),
            ("Hofuds", 136, ItemRarity.Green),
            ("Katayama", 136, ItemRarity.Green),
            ("Mistilteinn", 136, ItemRarity.Green),
            ("Morkinskinna", 136, ItemRarity.Green),
            ("Muspell", 136, ItemRarity.Green),
            ("Skeggiold", 136, ItemRarity.Green),
            ("Verdun", 136, ItemRarity.Green),
            ("Wargfangs", 136, ItemRarity.Green),
            ("Tyrfing", 136, ItemRarity.Green),
            ("Svalin", 136, ItemRarity.Green),

            // The Vault
            ("Halonic", 142, ItemRarity.Green),

            // The Great Gubal Library
            ("Sharlayan", 148, ItemRarity.Green),
            ("Belah'dian", 148, ItemRarity.Green),
            ("Kagehide", 148, ItemRarity.Green),
            ("Lewphon's Eye", 148, ItemRarity.Green),
            ("Nymian Royal Marine", 148, ItemRarity.Green),
            ("Old World", 148, ItemRarity.Green),
            ("The Southern Sun", 148, ItemRarity.Green),
            ("Pre-imperial Garlean Revolver", 148, ItemRarity.Green),
            ("Thavnairian", 148, ItemRarity.Green),

            // The Antitower
            ("Dravanian", 195, ItemRarity.Green),

            // Sohr Khai
            ("Berserker's", 215, ItemRarity.Green),
            ("Conqueror's", 215, ItemRarity.Green),
            ("Panegyrist's", 215, ItemRarity.Green),
            ("Prophet's", 215, ItemRarity.Green),
            ("Subjugator's", 215, ItemRarity.Green),
            ("Wrangler's", 215, ItemRarity.Green),
            ("Viking", 215, ItemRarity.Green),

            // Xelphatol
            ("Valkyrie's", 225, ItemRarity.Green),

            // Baelsar's Wall
            ("Filibuster's", 245, ItemRarity.Green),

            // 4.x Stormblood
            // Grouped by dungeon-shared loot, plus some 4.x weapons have unique names
            // The Sirensong Sea
            ("Ghost Barque", 260, ItemRarity.Green),

            // Bardam's Mettle
            ("Nomad's", 276, ItemRarity.Green),
            ("The Awaited Stars", 276, ItemRarity.Green),
            ("The Blazing Sun", 276, ItemRarity.Green),
            ("The Coming Storm", 276, ItemRarity.Green),
            ("The Crying Wind", 276, ItemRarity.Green),
            ("The Dancing Reeds", 276, ItemRarity.Green),
            ("The Eloquent Moon", 276, ItemRarity.Green),
            ("The Everflowing Waters", 276, ItemRarity.Green),
            ("The Heavens Devoured", 276, ItemRarity.Green),
            ("The Impassible Peak", 276, ItemRarity.Green),
            ("The Impassionate Tide", 276, ItemRarity.Green),
            ("The Lone Beacon", 276, ItemRarity.Green),
            ("The Nimble Beast", 276, ItemRarity.Green),
            ("The Stained Earth", 276, ItemRarity.Green),
            ("The Unsullied Skies", 276, ItemRarity.Green),
            ("The Voiceless Moon", 276, ItemRarity.Green),
            ("The Eminent Dominion", 276, ItemRarity.Green),
            ("Mol Shield", 276, ItemRarity.Green),

            // Doma Castle
            ("Yanxian", 282, ItemRarity.Green),
            ("Shin", 282, ItemRarity.Green),

            // Castrum Abania
            ("Valerian", 288, ItemRarity.Green),
            ("Xenobian", 288, ItemRarity.Green),
            ("Enhancing Sword", 288, ItemRarity.Green),
            ("Ritter Shield", 288, ItemRarity.Green),
            ("Acantha Shavers", 288, ItemRarity.Green),
            ("Aurora", 288, ItemRarity.Green),
            ("Cruadin", 288, ItemRarity.Green),
            ("Deae Gratia", 288, ItemRarity.Green),
            ("Ethica", 288, ItemRarity.Green),
            ("Griffinbanes", 288, ItemRarity.Green),
            ("Hrotti", 288, ItemRarity.Green),
            ("Kaman", 288, ItemRarity.Green),
            ("Kards", 288, ItemRarity.Green),
            ("Kotetsu", 288, ItemRarity.Green),
            ("Magnatus", 288, ItemRarity.Green),
            ("Narval", 288, ItemRarity.Green),
            ("Rigel", 288, ItemRarity.Green),
            ("Schlaeger", 288, ItemRarity.Green),
            ("Sparth", 288, ItemRarity.Green),
            ("Teiwaz", 288, ItemRarity.Green),

            // Ala Mhigo
            ("Arhat", 300, ItemRarity.Green),
            ("of the Crimson Lotus", 300, ItemRarity.Green),

            // The Drowned City of Skalla
            ("Skallic", 315, ItemRarity.Green),

            // The Burn
            ("Royal Volunteer's", 355, ItemRarity.Green),

            // The Ghimlyt Dark
            ("Alliance", 375, ItemRarity.Green),

            // 5.x Shadowbringers
            ("Lakeland", 390, ItemRarity.Green),
            ("Voeburtite", 400, ItemRarity.Green),
            ("Fae", 400, ItemRarity.Green),
            ("Ravel Keeper's", 406, ItemRarity.Green),
            ("Nabaath", 412, ItemRarity.Green),
            ("The Forgiven", 418, ItemRarity.Green),
            ("Amaurotine", 430, ItemRarity.Green),
            ("Warg", 445, ItemRarity.Green),
            ("Anamnesis", 455, ItemRarity.Green),
            ("Shadowless", 475, ItemRarity.Green),
            ("Heirloom", 485, ItemRarity.Green),
            ("Paglth'an", 505, ItemRarity.Green),

            // 6.x Endwalker
            ("Manusya", 520, ItemRarity.Green),
            ("Monstrorum", 520, ItemRarity.Green),
            ("Imperial", 530, ItemRarity.Green),
            ("Palaka", 536, ItemRarity.Green),
            ("Ktiseos", 542, ItemRarity.Green),
            ("Etheirys", 548, ItemRarity.Green),
            ("The Last", 560, ItemRarity.Green),
            ("Darbar", 575, ItemRarity.Green),
            ("Troian", 595, ItemRarity.Green),
            ("Manalis", 605, ItemRarity.Green),
            ("Distance", 625, ItemRarity.Green),

			// 7.x Dawntrail
            ("Riversbreath", 650, ItemRarity.Green),
            ("Zormor", 660, ItemRarity.Green),
            ("Skydeep", 666, ItemRarity.Green),
            ("Vanguard", 672, ItemRarity.Green),
            ("Origenics", 678, ItemRarity.Green),
            ("Epochal", 690, ItemRarity.Green),
        };

        public UtilitiesBase()
        {
            OffsetManager.Init();
            AssemblyProxy.AddAssembly("LlamaUtilities", typeof(UtilitiesBase).Assembly);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Start()
        {
            Navigator.NavigationProvider = new ServiceNavigationProvider();
            Navigator.PlayerMover = new SlideMover();

            if (BotTask.Type == TaskType.None)
            {
                Log.Error("Use the BotBase Settings window to run a task.");
                //OnButtonPress();

                _root = new TreeSharp.Action(a => RunStatus.Failure);
            }

            if (BotTask.Type == TaskType.AutoFollow)
            {
                //Just....no that botbase is shit
            }
            else
            {
                _root = new ActionRunCoroutine(r => Run());
            }
        }

        public override void Stop()
        {
            (Navigator.NavigationProvider as IDisposable)?.Dispose();
            Navigator.NavigationProvider = null;
        }

        public override void OnButtonPress()
        {
            if (settings == null || settings.IsDisposed)
            {
                settings = new Utilities();
            }

            try
            {
                settings.Show();
                settings.Activate();
            }
            catch
            {
                // ignored
            }
        }

        private async Task<bool> Run()
        {
            switch (BotTask.Type)
            {
                case TaskType.MateriaRemove:
                    var bagInfo = JsonConvert.DeserializeObject<(uint, ushort)>(BotTask.TaskInfo);
                    var slot = InventoryManager.GetBagByInventoryBagId((InventoryBagId)bagInfo.Item1).First(i => i.Slot == bagInfo.Item2);
                    await RemoveMateria(slot);
                    break;
                //List<(uint, ushort)>
                case TaskType.AutoFollow:
                    break;
                case TaskType.Reduce:
                    await Inventory.ReduceAll();
                    break;
                case TaskType.Desynth:
                    await Desynth();
                    break;
                case TaskType.None:
                    break;
                case TaskType.Hunts:
                    var huntTypes = new List<int>();
                    if (HuntsSettings.Instance.ARRHunts)
                    {
                        huntTypes.AddRange(HuntHelper.ARRHunts);
                    }

                    if (HuntsSettings.Instance.ClanHunts)
                    {
                        huntTypes.AddRange(HuntHelper.ClanHunts);
                    }

                    if (HuntsSettings.Instance.VerteranClanHunts)
                    {
                        huntTypes.AddRange(HuntHelper.VerteranClanHunts);
                    }

                    if (HuntsSettings.Instance.NutClanHunts)
                    {
                        huntTypes.AddRange(HuntHelper.NutClanHunts);
                    }

                    if (HuntsSettings.Instance.EwHunts)
                    {
                        huntTypes.AddRange(HuntHelper.EwHunts);
                    }

                    if (HuntsSettings.Instance.DtHunts)
                    {
                        huntTypes.AddRange(HuntHelper.DtHunts);
                    }


                    if (huntTypes.Count > 0)
                    {
                        await Hunts.DoHunts(huntTypes.ToArray());
                    }
                    else
                    {
                        Log.Error("Select some hunt types in settings");
                    }

                    break;
                case TaskType.Extract:
                    await Inventory.ExtractFromAllGear();
                    break;
                case TaskType.Coffers:
                    await Inventory.CofferTask();
                    break;
                case TaskType.Housing:
                    await Housing.CheckHousing();
                    break;
                case TaskType.CustomDeliveries:
                    await LlamaLibrary.Utilities.CustomDeliveries.RunCustomDeliveriesBySelection(DeliveriesSettings.Instance.DoZhloeDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoMnaagoDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoKurenaiDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoAdkiraghDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoKaishirrDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoEhlltouDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoCharlemendDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoAmelianceeliveries,
                                                                                                 DeliveriesSettings.Instance.DoAndenDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoMargratDeliveries,
                                                                                                 DeliveriesSettings.Instance.DoNitowikweDeliveries,
                                                                                                 DeliveriesSettings.Instance.CraftingClass);
                    break;
                case TaskType.GcTurnin:
                    await GCDailyTurnins.DoGCDailyTurnins();
                    break;
                case TaskType.Retainers:
                    await Retainers.RetainerRun();
                    break;
                case TaskType.FCWorkshop:
                    await FCWorkshop.HandInItems();
                    break;
                case TaskType.MateriaAffix:
                    //Log.Information(BotTask.TaskInfo);
                    var materiaInfo = JsonConvert.DeserializeObject<List<(uint Bag, ushort Slot)>>(BotTask.TaskInfo);
                    if (materiaInfo.Count <= 1)
                    {
                        Log.Information("List is under 1");
                        break;
                    }

                    var materiaBagSlots = new List<BagSlot>();
                    var equipmentSlot = InventoryManager.GetBagByInventoryBagId((InventoryBagId)materiaInfo.First().Bag).FirstOrDefault(i => i.Slot == materiaInfo.First().Slot);
                    if (equipmentSlot == null)
                    {
                        Log.Information("Slot is null");
                        break;
                    }

                    foreach (var mTuple in materiaInfo.Skip(1))
                    {
                        materiaBagSlots.Add(InventoryManager.GetBagByInventoryBagId((InventoryBagId)mTuple.Bag).First(i => i.Slot == mTuple.Slot));
                    }

                    Log.Information(equipmentSlot.ToString());
                    foreach (var slot2 in materiaBagSlots)
                    {
                        Log.Information(slot2.ToString());
                    }

                    await AffixMateria(equipmentSlot, materiaBagSlots);
                    break;
                case TaskType.ChocoRace:
                    await PandaRacer.Race();
                    break;
                case TaskType.GetToBreeder:
                    await PandaRacer.GetToBreeder();
                    break;
                case TaskType.GetToCounter:
                    await PandaRacer.GetToCounter();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (BotTask.Type == TaskType.Retainers && RetainerSettings.Instance.Loop)
            {
                return true;
            }

            BotTask.Type = TaskType.None;

            TreeRoot.Stop("Stop Requested");
            return true;
        }

        public static async Task<bool> AffixMateria(BagSlot bagSlot, List<BagSlot> materiaList)
        {
            Log.Information($"MateriaList count {materiaList.Count}");
            if (bagSlot == null || !bagSlot.IsValid)
            {
                return true;
            }

            Log.Information($"Want to affix Materia to {bagSlot}");

            for (var i = 0; i < materiaList.Count; i++)
            {
                if (materiaList[i] == null || !materiaList[i].IsValid)
                {
                    break;
                }

                Log.Information($"Want to affix materia {i} {materiaList[i]}");

                if (!materiaList[i].IsFilled)
                {
                    break;
                }

                if (!MateriaAttach.Instance.IsOpen)
                {
                    Log.Information($"Opening meld window");
                    await MateriaAttach.Instance.OpenMateriaAttachDialog(bagSlot);
                    await Coroutine.Wait(5000, () => MateriaAttach.Instance.IsOpen);

                    /*
                    if (!MateriaAttach.Instance.IsOpen)
                    {
                        Log.Information($"Can't open meld window");
                        return false;
                    }

                    MateriaAttach.Instance.ClickItem(0);
                    await Coroutine.Sleep(1000);
                    MateriaAttach.Instance.ClickMateria(0);
                    await Coroutine.Wait(7000, () => AgentMeld.Instance.Ready);
                    await Coroutine.Wait(5000, () => MateriaAttachDialog.Instance.IsOpen);*/
                }

                if (!MateriaAttachDialog.Instance.IsOpen)
                {
                    Log.Information($"Opening dialog attach");
                    /*
                    MateriaAttach.Instance.ClickItem(0);
                    await Coroutine.Sleep(1000);
                    MateriaAttach.Instance.ClickMateria(0);
                    await Coroutine.Wait(7000, () => AgentMeld.Instance.Ready);
                    await Coroutine.Wait(5000, () => MateriaAttachDialog.Instance.IsOpen);
                    await Coroutine.Wait(7000, () => AgentMeld.Instance.Ready);
                    if (!MateriaAttachDialog.Instance.IsOpen)
                    {
                        Log.Information($"Can't open meld dialog");
                        return false;
                    }
                    */

                    await MateriaAttach.Instance.OpenMateriaAttachDialog(bagSlot);
                }

                Log.Information("Wait Ready");
                await Coroutine.Wait(7000, () => MateriaAttachDialog.Instance.IsOpen);
                // await Coroutine.Wait(7000, () => AgentMeld.Instance.Ready);
                Log.Information("Send Baglsot Affix");
                // await Coroutine.Wait(7000, () => AgentMeld.Instance.CanMeld);
                bagSlot.AffixMateria(materiaList[i], true);
                Log.Information("Wait not ready");
                await Coroutine.Wait(20000, () => !AgentMeld.Instance.Ready);
                Log.Information("Wait ready");
                await Coroutine.Wait(20000, () => AgentMeld.Instance.Ready);
                await Coroutine.Wait(7000, () => !MateriaAttachDialog.Instance.IsOpen);
                Log.Information("Should be done melding");

                if (!materiaList[i].IsFilled)
                {
                    Log.Information("Materia not full?");
                    return false;
                }
            }

            if (MateriaAttach.Instance.IsOpen)
            {
                Log.Information("Closing window");
                MateriaAttach.Instance.Close();
                await Coroutine.Wait(7000, () => !MateriaAttach.Instance.IsOpen);
                //await Coroutine.Wait(7000, () => !AgentMeld.Instance.Ready);
                //await Coroutine.Sleep(1000);
            }

            return true;
        }

        public static async Task<bool> RemoveMateria(BagSlot bagSlot)
        {
            if (bagSlot != null && bagSlot.IsValid)
            {
                Log.Information($"Want to remove Materia from {bagSlot}");
                var count = bagSlot.MateriaCount();
                for (var i = 0; i < count; i++)
                {
                    Log.Information($"Removing materia {count - i}");
                    bagSlot.RemoveMateria();
                    await Coroutine.Wait(20000, () => Core.Memory.Read<uint>(LlamaLibrary.Memory.Offsets.Conditions + 0x27) != 0);
                    await Coroutine.Wait(20000, () => Core.Memory.Read<uint>(LlamaLibrary.Memory.Offsets.Conditions + 0x27) == 0);
                    await Coroutine.Sleep(1000);
                }
            }

            Log.Information($"Item now has {bagSlot.MateriaCount()} materia affixed");

            return true;
        }

        public static async Task<bool> Desynth()
        {
            if (IsBusy)
            {
                await GeneralFunctions.StopBusy(leaveDuty: false);
                if (IsBusy)
                {
                    Log.Warning("Can't desynth right now, we're busy.");
                    return false;
                }
            }

            var toDesynthList = InventoryManager.GetBagsByInventoryBagId(BagsToCheck())
                .SelectMany(bag => bag.FilledSlots
                                .FindAll(bs => bs.IsDesynthesizable && (IsOnDesynthList(bs) || ShouldDesynthFish(bs))))
                .ToList();

            if (!toDesynthList.Any())
            {
                Log.Warning("No items to desynth.");
                return false;
            }

            Log.Information($"# of slots to Desynth: {toDesynthList.Count()}");

            await Inventory.Desynth(toDesynthList);
            return true;
        }

        private static bool ShouldDesynthFish(BagSlot bs)
        {
            return ReduceSettings.Instance.IncludeFish && bs.Item.EquipmentCatagory == ItemUiCategory.Seafood && bs.CanDesynthesize;
        }

        private static bool IsOnDesynthList(BagSlot bagSlot)
        {
            return TrustDungeonDesynthList.Any(x =>
                bagSlot.Item.ItemLevel == x.ItemLevel
                && (ItemRarity)bagSlot.Item.Rarity == x.Rarity
                //&& is not gearset item, if we can figure that out one day
                && bagSlot.Item.EnglishName.Contains(x.Name, StringComparison.InvariantCultureIgnoreCase));
        }

        private static InventoryBagId[] BagsToCheck()
        {
            return ReduceSettings.Instance.IncludeArmory ? Inventory.InventoryBagIds.Concat(Inventory.ArmoryBagIds).ToArray() : Inventory.InventoryBagIds;

            //return inventoryBagIds;
        }
    }
}