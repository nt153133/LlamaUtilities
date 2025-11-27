using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.NeoProfiles;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.Helpers.NPC;
using LlamaLibrary.LlamaManagers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("AnimusBooks")]
    public class AnimusBooks : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("ItemId")]
        [XmlAttribute("ItemID")]
        public int ItemId { get; set; }

        private readonly Npc gjusana = new(1006976, 156, new Vector3(71.94617f, 33.06656f, -706.2944f)); //G'jusana (Book lady)
        private readonly Npc jalzahn = new(1008948, 154, new Vector3(405.1117f, -4.498068f, -89.15851f)); //Jalzahn  (Relic dude)

        private uint curtanaAtma = 7824;
        private uint holyShieldAtma = 7833;
        private uint tomestoneOfPoetics = 28;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public AnimusBooks() : base()
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
            return new ActionRunCoroutine(r => Main());
        }

        private async Task Main()
        {
            if (Core.Me.CurrentJob == ClassJobType.Paladin)
            {
                var bookList = new[] { RelicBookType.Fire, RelicBookType.Fall, RelicBookType.Wind, RelicBookType.Earth };
                var bookListShield = new[] { RelicBookType.Fire, RelicBookType.Fall };

                if (bookList.Any(i => !RelicBookManager.IsBookCompleted(curtanaAtma, i)) || bookListShield.Any(i => !RelicBookManager.IsBookCompleted(holyShieldAtma, i)))
                {
                    if (ConditionParser.ItemCount(tomestoneOfPoetics) < 100)
                    {
                        Log.Error($"We need more {DataManager.GetItem(28).CurrentLocaleName}");
                        _isDone = true;
                        return;
                    }

                    await GetNewBook();
                }
                else
                {
                    await UpgradeRelic();
                }
            }
            else
            {
                var bookList = new[] { RelicBookType.Fire, RelicBookType.Fall, RelicBookType.Wind, RelicBookType.Earth };
                if (bookList.Any(i => !RelicBookManager.IsBookCompleted((uint)ItemId, i)))
                {
                    if (ConditionParser.ItemCount(tomestoneOfPoetics) < 100)
                    {
                        Log.Error($"We need more {DataManager.GetItem(28).CurrentLocaleName}");
                        _isDone = true;
                        return;
                    }

                    await GetNewBook();
                }
                else
                {
                    await UpgradeRelic();
                }
            }

            _isDone = true;
        }

        private async Task UpgradeRelic()
        {
            Log.Information("Going to Upgrade our relic.");

            Dictionary<ClassJobType, uint[]> Slots = new Dictionary<ClassJobType, uint[]>
            {
                { ClassJobType.Paladin, new uint[] { 0, 1 } },
                { ClassJobType.Monk, new uint[] { 2 } },
                { ClassJobType.Warrior, new uint[] { 3 } },
                { ClassJobType.Dragoon, new uint[] { 4 } },
                { ClassJobType.Bard, new uint[] { 5 } },
                { ClassJobType.Ninja, new uint[] { 6 } },
                { ClassJobType.WhiteMage, new uint[] { 7 } },
                { ClassJobType.BlackMage, new uint[] { 8 } },
                { ClassJobType.Summoner, new uint[] { 9 } },
                { ClassJobType.Scholar, new uint[] { 10 } },
            };

            foreach (var slot in Slots[Core.Me.CurrentJob])
            {
                if (ff14bot.Managers.InventoryManager.GetBagByInventoryBagId(ff14bot.Enums.InventoryBagId.EquippedItems)[ff14bot.Enums.EquipmentSlot.MainHand].RawItemId == ItemId ||
                    ff14bot.Managers.InventoryManager.GetBagByInventoryBagId(ff14bot.Enums.InventoryBagId.EquippedItems)[ff14bot.Enums.EquipmentSlot.OffHand].RawItemId == holyShieldAtma)
                {
                    await ChangeJob();
                    if (ff14bot.Managers.InventoryManager.GetBagByInventoryBagId(ff14bot.Enums.InventoryBagId.EquippedItems)[ff14bot.Enums.EquipmentSlot.MainHand].RawItemId == ItemId ||
                        ff14bot.Managers.InventoryManager.GetBagByInventoryBagId(ff14bot.Enums.InventoryBagId.EquippedItems)[ff14bot.Enums.EquipmentSlot.OffHand].RawItemId == holyShieldAtma)
                    {
                        await ChangeJob();
                        if (ff14bot.Managers.InventoryManager.GetBagByInventoryBagId(ff14bot.Enums.InventoryBagId.EquippedItems)[ff14bot.Enums.EquipmentSlot.MainHand].RawItemId == ItemId ||
                            ff14bot.Managers.InventoryManager.GetBagByInventoryBagId(ff14bot.Enums.InventoryBagId.EquippedItems)[ff14bot.Enums.EquipmentSlot.OffHand].RawItemId == holyShieldAtma)
                        {
                            Log.Error("Please unequip your relic repo and restart the profile.");
                            _isDone = true;
                            return;
                        }
                    }
                }

                if (!await LlamaLibrary.Helpers.Navigation.GetToInteractNpcSelectString(jalzahn))
                {
                    Log.Error($"Failed to get to {DataManager.GetLocalizedNPCName(1008948)}");
                    return;
                }

                if (SelectIconString.IsOpen)
                {
                    Logging.WriteDiagnostic($"Selecting '{LlamaLibrary.Helpers.Translator.RelicWeaponAtmaEnhancement}'.");
                    if (!SelectIconString.ClickLineContains(LlamaLibrary.Helpers.Translator.RelicWeaponAtmaEnhancement))
                    {
                        Logging.WriteDiagnostic($"We can't find '{LlamaLibrary.Helpers.Translator.RelicWeaponAtmaEnhancement}', exiting'.");
                        _isDone = true;
                        return;
                    }
                }

                while (!SelectString.IsOpen)
                {
                    Talk.Next();
                    await Coroutine.Wait(500, () => !Talk.DialogOpen);
                    await Coroutine.Wait(500, () => Talk.DialogOpen);
                    await Coroutine.Yield();
                }

                if (SelectString.IsOpen)
                {
                    Logging.WriteDiagnostic("Choosing weapon animus.");
                    ff14bot.RemoteWindows.SelectString.ClickSlot(slot);
                }
                else
                {
                    Logging.WriteDiagnostic("Weapon select window failed to open.");
                    ff14bot.TreeRoot.Stop("Window failed to open");
                }

                await Coroutine.Wait(5000, () => SelectYesno.IsOpen);
                if (ff14bot.RemoteWindows.SelectYesno.IsOpen)
                {
                    Logging.WriteDiagnostic("Selecting Yes.");
                    ff14bot.RemoteWindows.SelectYesno.ClickYes();
                    await Coroutine.Wait(10000, () => QuestLogManager.InCutscene);
                    if (QuestLogManager.InCutscene)
                    {
                        while (QuestLogManager.InCutscene)
                        {
                            Talk.Next();
                            await Coroutine.Wait(500, () => !Talk.DialogOpen);
                            await Coroutine.Wait(500, () => Talk.DialogOpen);
                            await Coroutine.Yield();

                            await Buddy.Coroutines.Coroutine.Sleep(1000);
                        }
                    }
                }
            }
        }

        private async Task GetNewBook()
        {
            Log.Information("Going to purchase a new book.");
            if (!await LlamaLibrary.Helpers.Navigation.GetToInteractNpcSelectString(gjusana))
            {
                Log.Error($"Failed to get to {DataManager.GetLocalizedNPCName(1006976)}");
                return;
            }

            if (SelectIconString.IsOpen)
            {
                SelectIconString.ClickSlot(0);
                while (!SelectString.IsOpen)
                {
                    Talk.Next();
                    await Coroutine.Wait(500, () => !Talk.DialogOpen);
                    await Coroutine.Wait(500, () => Talk.DialogOpen);
                    await Coroutine.Yield();
                }

                await Coroutine.Wait(500, () => !SelectIconString.IsOpen);
                await Coroutine.Wait(500, () => SelectString.IsOpen);
            }

            if (SelectString.IsOpen)
            {
                // PLD logic
                if (Core.Me.CurrentJob == ClassJobType.Paladin)
                {
                    await HandlePaladinBooks();
                }
                else
                {
                    await HandleBooks();
                }
            }

            if (SelectYesno.IsOpen)
            {
                Log.Information($"Selecting Yes");
                SelectYesno.ClickYes();
            }

            await GeneralFunctions.StopBusy();

            _isDone = true;
        }

        private async Task HandleBooks()
        {
            var fireBooksCompleted = RelicBookManager.NumOfFireCompleted((uint)ItemId);
            var fallBooksCompleted = RelicBookManager.NumOfFallCompleted((uint)ItemId);
            var windBooksCompleted = RelicBookManager.NumOfWindCompleted((uint)ItemId);
            var earthBooksCompleted = RelicBookManager.NumOfEarthCompleted((uint)ItemId);

            // Get to book selection
            var selection = "";

            if (SelectString.IsOpen)
            {
                if (fireBooksCompleted < RelicBookManager.MaxCountByType((uint)ItemId, RelicBookType.Fire))
                {
                    selection = LlamaLibrary.LlamaManagers.RelicBookManager.BooksofFire;
                }
                else if (fallBooksCompleted < RelicBookManager.MaxCountByType((uint)ItemId, RelicBookType.Fall))
                {
                    selection = LlamaLibrary.LlamaManagers.RelicBookManager.BooksofFall;
                }
                else if (windBooksCompleted < RelicBookManager.MaxCountByType((uint)ItemId, RelicBookType.Wind))
                {
                    selection = LlamaLibrary.LlamaManagers.RelicBookManager.BooksofWind;
                }
                else if (earthBooksCompleted < RelicBookManager.MaxCountByType((uint)ItemId, RelicBookType.Earth))
                {
                    selection = LlamaLibrary.LlamaManagers.RelicBookManager.BooksofEarth;
                }
            }

            Log.Information($"Selecting {selection}");
            if (!SelectString.ClickLineContains(selection))
            {
                Log.Error($"We can't find '{selection}', exiting'.");
                _isDone = true;
                return;
            }

            await Coroutine.Wait(2000, () => SelectYesno.IsOpen);
        }

        private async Task HandlePaladinBooks()
        {
            var pldSwordFireBooksCompleted = RelicBookManager.NumOfFireCompleted(curtanaAtma);
            var pldSwordFallBooksCompleted = RelicBookManager.NumOfFallCompleted(curtanaAtma);
            var pldSwordWindBooksCompleted = RelicBookManager.NumOfWindCompleted(curtanaAtma);
            var pldSwordEarthBooksCompleted = RelicBookManager.NumOfEarthCompleted(curtanaAtma);

            Log.Information($"Fire {pldSwordFireBooksCompleted}/{RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Fire)}, Fall: {pldSwordFallBooksCompleted}/{RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Fall)}, Wind: {pldSwordWindBooksCompleted}/{RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Wind)}, Earth: {pldSwordEarthBooksCompleted}/{RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Earth)}");
            // Get to book selection
            if (pldSwordFireBooksCompleted == RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Fire) && pldSwordFallBooksCompleted == RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Fall) &&
                pldSwordWindBooksCompleted == RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Wind) && pldSwordEarthBooksCompleted == RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Earth))
            {
                Log.Information($"Selecting {RelicBookManager.PLDBooksOfShields}");
                if (!SelectString.ClickLineContains(RelicBookManager.PLDBooksOfShields))
                {
                    Log.Error($"We can't find '{RelicBookManager.PLDBooksOfShields}', exiting'.");
                    _isDone = true;
                    return;
                }

                await Coroutine.Wait(500, () => !SelectString.IsOpen);
                await Coroutine.Wait(500, () => SelectString.IsOpen);
                var selection = PldShieldSelection();
                Log.Information($"Paladin Shield: Selecting {selection}");
                if (!SelectString.ClickLineContains(selection))
                {
                    Log.Error($"Paladin Shield: We can't find '{selection}', exiting'.");
                    _isDone = true;
                    return;
                }

                await Coroutine.Wait(2000, () => SelectYesno.IsOpen);
            }
            else
            {
                Log.Information($"Paladin Sword: Selecting {RelicBookManager.PLDBooksOfSwords}");
                if (!SelectString.ClickLineContains(RelicBookManager.PLDBooksOfSwords))
                {
                    Log.Error($"Paladin Sword: We can't find '{RelicBookManager.PLDBooksOfSwords}', exiting'.");
                    _isDone = true;
                    return;
                }

                await Coroutine.Wait(500, () => !SelectString.IsOpen);
                await Coroutine.Wait(500, () => SelectString.IsOpen);
                var selection = PldSwordSelection();
                Log.Information($"Paladin Sword: Selecting {selection}");
                if (!SelectString.ClickLineContains(selection))
                {
                    Log.Error($"Paladin Sword: We can't find '{selection}', exiting'.");
                    _isDone = true;
                    return;
                }

                await Coroutine.Wait(2000, () => SelectYesno.IsOpen);
            }
        }

        private string PldSwordSelection()
        {
            var pldSwordFireBooksCompleted = RelicBookManager.NumOfFireCompleted(curtanaAtma);
            var pldSwordFallBooksCompleted = RelicBookManager.NumOfFallCompleted(curtanaAtma);
            var pldSwordWindBooksCompleted = RelicBookManager.NumOfWindCompleted(curtanaAtma);
            var pldSwordEarthBooksCompleted = RelicBookManager.NumOfEarthCompleted(curtanaAtma);

            var selection = "";

            if (SelectString.IsOpen)
            {
                if (pldSwordFireBooksCompleted < RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Fire))
                {
                    selection = RelicBookManager.BooksofFire;
                }
                else if (pldSwordFallBooksCompleted < RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Fall))
                {
                    selection = RelicBookManager.BooksofFall;
                }
                else if (pldSwordWindBooksCompleted < RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Wind))
                {
                    selection = RelicBookManager.BooksofWind;
                }
                else if (pldSwordEarthBooksCompleted < RelicBookManager.MaxCountByType(curtanaAtma, RelicBookType.Earth))
                {
                    selection = RelicBookManager.BooksofEarth;
                }
            }

            return selection;
        }

        private string PldShieldSelection()
        {
            var pldShieldFireBooksCompleted = RelicBookManager.NumOfFireCompleted(holyShieldAtma);
            var pldShieldFallBooksCompleted = RelicBookManager.NumOfFallCompleted(holyShieldAtma);

            var selection = "";

            if (SelectString.IsOpen)
            {
                if (pldShieldFireBooksCompleted < RelicBookManager.MaxCountByType(holyShieldAtma, RelicBookType.Fire))
                {
                    selection = RelicBookManager.PLDBookOfNetherfire;
                }
                else if (pldShieldFallBooksCompleted < RelicBookManager.MaxCountByType(holyShieldAtma, RelicBookType.Fall))
                {
                    selection = RelicBookManager.PLDBookOfNetherfall;
                }
            }

            return selection;
        }

        private async Task ChangeJob()
        {
            var newjob = Core.Me.CurrentJob;

            await Coroutine.Wait(10000, () => !LlamaLibrary.Utilities.Inventory.IsBusy);

            var gearSets = GearsetManager.GearSets.Where(i => i.InUse);
            var foundJob = gearSets.Any(i => i.Class == newjob);

            Log.Information("Started");
            Log.Information($"Found job: {foundJob} Job:{newjob}");
            if (foundJob && gearSets.Any(gs => gs.Class == newjob))
            {
                Log.Information($"Found GearSet");
                gearSets.First(gs => gs.Class == newjob).Activate();

                await Coroutine.Wait(3000, () => SelectYesno.IsOpen);
                if (SelectYesno.IsOpen)
                {
                    SelectYesno.Yes();
                    await Coroutine.Sleep(3000);
                }

                // await Coroutine.Sleep(1000);
            }
            else if (foundJob)
            {
                var Job = newjob.ToString() + "s_Primary_Tool";

                var categoryFound = Enum.TryParse(Job, true, out ItemUiCategory category);

                if (categoryFound)
                {
                    Log.Information($"Found Item Category: {categoryFound} Category:{category}");
                    var item = InventoryManager.FilledInventoryAndArmory.Where(i => i.Item.EquipmentCatagory == category).OrderByDescending(i => i.Item.ItemLevel).FirstOrDefault();
                    var EquipSlot = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.MainHand];

                    Log.Information($"Found Item {item}");
                    if (item != null)
                    {
                        item.Move(EquipSlot);
                    }

                    await Coroutine.Sleep(1000);

                    ChatManager.SendChat("/gs save");

                    await Coroutine.Sleep(1000);
                }
                else
                {
                    Log.Error($"Couldn't find item category for {Job}");
                }
            }

            _isDone = Core.Me.CurrentJob == newjob;
        }
    }
}