using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("EquipCurrentGearset")]
    public class EquipCurrentGearset : LLProfileBehavior
    {
        private bool _isDone;

        public override bool IsDone => _isDone;

        public override bool HighPriority => true;

        public EquipCurrentGearset() : base() { }

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

        private async Task EquipCurrentGearsetTask()
        {
            var Job = Core.Me.CurrentJob.ToString();
            var gearSets = GearsetManager.GearSets.Where(i => i.InUse);
            var foundJob = Enum.TryParse(Job.Trim(), true, out ClassJobType newjob);

            if (Core.Me.InCombat)
            {
                Log.Error("Currently in combat, can't switch gearsets. Exiting.");
                _isDone = true;
                return;
            }

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
                Job = Job.Trim() + "s_Primary_Tool";

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

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => EquipCurrentGearsetTask());
        }
    }
}
