using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Helpers;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.Extensions;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("EquipAnima")]
    public class EquipAnima : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public EquipAnima() : base()
        {
        }

        protected override void OnStart()
        {
        }

        public static Dictionary<ClassJobType, uint[]> AnimaRelicWeapons = new Dictionary<ClassJobType, uint[]>()
        {
            { ClassJobType.Astrologian, new uint[] { 13623, 13609, 13235, 14882, 15235, 15249, 15263, 16062 } },
            { ClassJobType.Bard, new uint[] { 13615, 13601, 13227, 14874, 15227, 15241, 15255, 16054 } },
            { ClassJobType.BlackMage, new uint[] { 13620, 13606, 13232, 14879, 15232, 15246, 15260, 16059 } },
            { ClassJobType.DarkKnight, new uint[] { 13617, 13603, 13229, 14876, 15229, 15243, 15257, 16056 } },
            { ClassJobType.Dragoon, new uint[] { 13614, 13600, 13226, 14873, 15226, 15240, 15254, 16053 } },
            { ClassJobType.Machinist, new uint[] { 13618, 13604, 13230, 14877, 15230, 15244, 15258, 16057 } },
            { ClassJobType.Monk, new uint[] { 13612, 13598, 13224, 14871, 15224, 15238, 15252, 16051 } },
            { ClassJobType.Ninja, new uint[] { 13616, 13602, 13228, 14875, 15228, 15242, 15256, 16055 } },
            { ClassJobType.Paladin, new uint[] { 13611, 13597, 13223, 14870, 15223, 15237, 15251, 16050 } },
            { ClassJobType.Scholar, new uint[] { 13622, 13608, 13234, 14881, 15234, 15248, 15262, 16061 } },
            { ClassJobType.Summoner, new uint[] { 13621, 13607, 13233, 14880, 15233, 15247, 15261, 16060 } },
            { ClassJobType.Warrior, new uint[] { 13613, 13599, 13225, 14872, 15225, 15239, 15253, 16052 } },
            { ClassJobType.WhiteMage, new uint[] { 13619, 13605, 13231, 14878, 15231, 15245, 15259, 16058 } },
        };

        public static uint[] AnimaRelicOffhands = new uint[] { 13624, 13610, 13236, 14883, 15236, 15250, 15264, 16063};

        protected override void OnDone()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => EquipAnimaTask());
        }

        private async Task EquipAnimaTask()
        {
            var mainhand = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.MainHand];
            if (AnimaRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId))
            {
                Log.Information($"Main Hand: {mainhand.Name} already equipped");
            }
            else
            {
                while (!AnimaRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId))
                {
                    if (Core.Me.InCombat)
                    {
                        Log.Information("We are in combat and can't switch weapons.");
                        Log.Information("Waiting 10 seconds or until combat drops");
                        await Coroutine.Wait(10000, () => !Core.Me.InCombat);
                        while (Core.Me.InCombat)
                        {
                            Log.Error("Combat didn't end after 10 seconds. Trying again.");
                            await Coroutine.Wait(10000, () => !Core.Me.InCombat);
                        }
                    }

                    Logging.WriteDiagnostic($"Main Hand: {mainhand.Name} not already equipped");
                    var item1 = InventoryManager.FilledInventoryAndArmory.FirstOrDefault(i => AnimaRelicWeapons[Core.Me.CurrentJob].Contains(i.RawItemId));
                    if (item1 != default(BagSlot))
                    {
                        Log.Information($"Equipping {mainhand.Name}");
                        item1.Move(mainhand);
                        await BagSlotExtensions.BagSlotNotFilledWait(item1);
                        await Coroutine.Wait(10000, () => AnimaRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId));
                        if (!AnimaRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId))
                        {
                            Log.Error($"Equipping {mainhand.Name} failed");
                        }
                        else
                        {
                            Log.Information($"Now wearing {mainhand.Name}");
                        }
                    }
                    else
                    {
                        Log.Error("No Anima Relic Weapon Found. Exiting");
                        _isDone = true;
                        return;
                    }
                }
            }

            if (Core.Me.CurrentJob == ClassJobType.Paladin)
            {
                var offhand = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.OffHand];
                if (AnimaRelicOffhands.Contains(offhand.RawItemId))
                {
                    Log.Information($"OffHand: {offhand.Name} already equipped");
                }
                else
                {
                    while (!AnimaRelicOffhands.Contains(offhand.RawItemId))
                    {
                        if (Core.Me.InCombat)
                        {
                            Log.Information("We are in combat and can't switch weapons.");
                            Log.Information("Waiting 10 seconds or until combat drops");
                            await Coroutine.Wait(10000, () => !Core.Me.InCombat);
                            while (Core.Me.InCombat)
                            {
                                Log.Error("Combat didn't end after 10 seconds. Trying again.");
                                await Coroutine.Wait(10000, () => !Core.Me.InCombat);
                            }
                        }

                        Log.Information($"Offhand: {offhand.Name} Not Equipped");
                        var item2 = InventoryManager.FilledInventoryAndArmory.FirstOrDefault(i => AnimaRelicOffhands.Contains(i.RawItemId));
                        if (item2 != default(BagSlot))
                        {
                            Log.Information($"Equipping {offhand.Name}");
                            item2.Move(offhand);
                            await BagSlotExtensions.BagSlotNotFilledWait(item2);
                            await Coroutine.Wait(10000, () => AnimaRelicOffhands.Contains(offhand.RawItemId));
                            if (!AnimaRelicOffhands.Contains(offhand.RawItemId))
                            {
                                Log.Error($"Offhand: {offhand.Name} equipping failed. Trying again");
                            }
                            else
                            {
                                Log.Information($"Now wearing {offhand.Name}");
                            }
                        }
                        else
                        {
                            Log.Error("No Anima Relic Offhand Found");
                            _isDone = true;
                            return;
                        }
                    }
                }
            }

            _isDone = true;
        }

        public override bool IsDone => _isDone;
    }
}