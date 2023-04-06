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
using LlamaLibrary.Extensions;
using LlamaLibrary.Helpers;
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

        private static readonly List<(string Name, uint ItemLevel)> DesynthList = new List<(string, uint)>
        {
            // 5.x Shadowbringers
            ("Lakeland", 390),
            ("Voeburtite", 400),
            ("Fae", 400),
            ("Ravel Keeper's", 406),
            ("Nabaath", 412),
            ("The Forgiven", 418),
            ("Amaurotine", 430),
            ("Warg", 445),
            ("Anamnesis", 455),
            ("Shadowless", 475),
            ("Heirloom", 485),
            ("Paglth'an", 505),

            // 6.x Endwalker
            ("Manusya", 520),
            ("Imperial", 530),
            ("Palaka", 536),
            ("Ktiseos", 542),
            ("Etheirys", 548),
            ("The Last", 560),
            ("Darbar", 575),
            ("Troian", 595),
            ("Manalis", 605),
        };

        public UtilitiesBase()
        {
            OffsetManager.Init();
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
                    await LlamaLibrary.Utilities.CustomDeliveries.RunCustomDeliveriesBySelection(DeliveriesSettings.Instance.DoZhloeDeliveries,DeliveriesSettings.Instance.DoMnaagoDeliveries,DeliveriesSettings.Instance.DoKurenaiDeliveries,DeliveriesSettings.Instance.DoAdkiraghDeliveries,DeliveriesSettings.Instance.DoKaishirrDeliveries,DeliveriesSettings.Instance.DoEhlltouDeliveries,DeliveriesSettings.Instance.DoCharlemendDeliveries,DeliveriesSettings.Instance.DoAmelianceeliveries,DeliveriesSettings.Instance.CraftingClass);
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
                    bagSlot.OpenMeldInterface();
                    await Coroutine.Wait(5000, () => MateriaAttach.Instance.IsOpen);

                    if (!MateriaAttach.Instance.IsOpen)
                    {
                        Log.Information($"Can't open meld window");
                        return false;
                    }

                    MateriaAttach.Instance.ClickItem(0);
                    await Coroutine.Sleep(1000);
                    MateriaAttach.Instance.ClickMateria(0);
                    await Coroutine.Wait(7000, () => AgentMeld.Instance.Ready);
                    await Coroutine.Wait(5000, () => MateriaAttachDialog.Instance.IsOpen);
                }

                if (!MateriaAttachDialog.Instance.IsOpen)
                {
                    Log.Information($"Opening dialog attach");
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
            return DesynthList.Any(x =>
                bagSlot.Item.ItemLevel == x.ItemLevel
                && bagSlot.Item.EnglishName.Contains(x.Name, StringComparison.InvariantCultureIgnoreCase));
        }

        private static InventoryBagId[] BagsToCheck()
        {
            return ReduceSettings.Instance.IncludeArmory ? Inventory.InventoryBagIds.Concat(Inventory.ArmoryBagIds).ToArray() : Inventory.InventoryBagIds;

            //return inventoryBagIds;
        }
    }
}