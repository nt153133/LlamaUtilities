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
    [XmlElement("EquipZodiac")]
    public class EquipZodiac : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public EquipZodiac() : base()
        {
        }

        protected override void OnStart()
        {
        }

        public static Dictionary<ClassJobType, uint[]> ZodiacRelicWeapons = new Dictionary<ClassJobType, uint[]>()
        {
            { ClassJobType.Bard, new uint[] { 1955, 6261, 7828, 7838, 7867, 8653, 9495, 10058 } },
            { ClassJobType.BlackMage, new uint[] { 2140, 6263, 7830, 7840, 7869, 8655, 9497, 10060 } },
            { ClassJobType.Dragoon, new uint[] { 1885, 6260, 7827, 7837, 7866, 8652, 9494, 10057 } },
            { ClassJobType.Monk, new uint[] { 1746, 6258, 7825, 7835, 7864, 8650, 9492, 10055 } },
            { ClassJobType.Ninja, new uint[] { 7888, 9250, 9251, 9252, 9253, 9254, 9501, 10064 } },
            { ClassJobType.Paladin, new uint[] { 1675,6257,7824,7834,7863,8649,9491,10054 } },
            { ClassJobType.Scholar, new uint[] { 2214, 6265, 7832, 7842, 7871, 8657, 9499, 10062 } },
            { ClassJobType.Summoner, new uint[] { 2213, 6264, 7831, 7841, 7870, 8656, 9498, 10061 } },
            { ClassJobType.Warrior, new uint[] { 1816, 6259, 7826, 7836, 7865, 8651, 9493, 10056 } },
            { ClassJobType.WhiteMage, new uint[] { 2052, 6262, 7829, 7839, 7868, 8654, 9496, 10059 } },
        };

        public static uint[] ZodiacRelicOffhands = new uint[] { 2306,6266,7833,7843,7872,8658,9500,10063 };

        protected override void OnDone()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => EquipZodiacTask());
        }

        private async Task EquipZodiacTask()
        {
            var mainhand = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems)[EquipmentSlot.MainHand];
            if (ZodiacRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId))
            {
                Log.Information($"Main Hand: {mainhand.Name} already equipped");
            }
            else
            {
                while (!ZodiacRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId))
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
                    var item1 = InventoryManager.FilledInventoryAndArmory.FirstOrDefault(i => ZodiacRelicWeapons[Core.Me.CurrentJob].Contains(i.RawItemId));
                    if (item1 != default(BagSlot))
                    {
                        Log.Information($"Equipping {mainhand.Name}");
                        item1.Move(mainhand);
                        await BagSlotExtensions.BagSlotNotFilledWait(item1);
                        await Coroutine.Wait(10000, () => ZodiacRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId));
                        if (!ZodiacRelicWeapons[Core.Me.CurrentJob].Contains(mainhand.RawItemId))
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
                if (ZodiacRelicOffhands.Contains(offhand.RawItemId))
                {
                    Log.Information($"OffHand: {offhand.Name} already equipped");
                }
                else
                {
                    while (!ZodiacRelicOffhands.Contains(offhand.RawItemId))
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
                        var item2 = InventoryManager.FilledInventoryAndArmory.FirstOrDefault(i => ZodiacRelicOffhands.Contains(i.RawItemId));
                        if (item2 != default(BagSlot))
                        {
                            Log.Information($"Equipping {offhand.Name}");
                            item2.Move(offhand);
                            await BagSlotExtensions.BagSlotNotFilledWait(item2);
                            await Coroutine.Wait(10000, () => ZodiacRelicOffhands.Contains(offhand.RawItemId));
                            if (!ZodiacRelicOffhands.Contains(offhand.RawItemId))
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