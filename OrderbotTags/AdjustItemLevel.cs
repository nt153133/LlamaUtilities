using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>
    /// Unequips the combination of non-weapon gear that gets the character as
    /// close as possible to the requested average item level.
    ///
    /// This is intentionally a one-shot tag. It leaves the selected gear
    /// unequipped so subsequent profile behaviors run at the adjusted level.
    /// Use EquipCurrentGearset afterward to restore the saved gearset.
    /// </summary>
    [XmlElement("AdjustItemLevel")]
    public class AdjustItemLevel : LLProfileBehavior
    {
        private const int ItemLevelDivisor = 12;

        private static readonly EquipmentSlot[] RemovableSlots =
        {
            EquipmentSlot.Head,
            EquipmentSlot.Body,
            EquipmentSlot.Hands,
            EquipmentSlot.Legs,
            EquipmentSlot.Feet,
            EquipmentSlot.Earring,
            EquipmentSlot.Necklace,
            EquipmentSlot.Bracelet,
            EquipmentSlot.Ring1,
            EquipmentSlot.Ring2
        };

        private bool _isDone;
        private bool _combatWaitLogged;

        [XmlAttribute("RequiredItemLevel")]
        [XmlAttribute("TargetItemLevel")]
        [DefaultValue(-1)]
        public int RequiredItemLevel { get; set; } = -1;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        protected override void OnStart()
        {
            if (RequiredItemLevel < 0)
            {
                Log.Error("RequiredItemLevel must be zero or greater.");
                _isDone = true;
            }
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(_ => AdjustEquipment());
        }

        protected override void OnDone()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
            _combatWaitLogged = false;
        }

        private async Task<bool> AdjustEquipment()
        {
            if (_isDone)
                return true;

            if (Core.Player.InCombat)
            {
                if (!_combatWaitLogged)
                {
                    Log.Information("Waiting to leave combat before adjusting item level.");
                    _combatWaitLogged = true;
                }

                await Coroutine.Yield();
                return true;
            }

            var equippedBag = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems);
            var candidates = RemovableSlots
                .Select(slot => new GearCandidate(slot, equippedBag[slot]))
                .Where(candidate => candidate.Item != null && candidate.Item.IsFilled)
                .ToList();

            var slotsToRemove = FindBestRemovalSet(candidates);
            var startingItemLevel = LlamaLibrary.ScriptConditions.Helpers.CurrentItemLevel();
            var removedCount = 0;

            foreach (var candidate in slotsToRemove)
            {
                var destinationBag = GetArmoryBag(candidate.Slot);
                var destination = InventoryManager
                    .GetBagByInventoryBagId(destinationBag)
                    .FirstOrDefault(slot => !slot.IsFilled);

                if (destination == null)
                {
                    Log.Warning($"No free slot in {destinationBag}; leaving {candidate.Item.EnglishName} equipped.");
                    continue;
                }

                if (!candidate.Item.Move(destination))
                {
                    Log.Warning($"Could not unequip {candidate.Item.EnglishName} from {candidate.Slot}.");
                    continue;
                }

                removedCount++;
                await Coroutine.Sleep(300);
            }

            await Coroutine.Sleep(500);
            var resultingItemLevel = LlamaLibrary.ScriptConditions.Helpers.CurrentItemLevel();
            Log.Information($"Adjusted item level from {startingItemLevel} to {resultingItemLevel} " +
                            $"(requested {RequiredItemLevel}); removed {removedCount} item(s).");
            Log.Information("Use <EquipCurrentGearset /> after the controlled-level behavior to restore equipment.");

            _isDone = true;
            return true;
        }

        private List<GearCandidate> FindBestRemovalSet(List<GearCandidate> candidates)
        {
            if (candidates.Count == 0)
                return new List<GearCandidate>();

            var equipped = InventoryManager.GetBagByInventoryBagId(InventoryBagId.EquippedItems);
            var mainHand = equipped[EquipmentSlot.MainHand];
            var offHand = equipped[EquipmentSlot.OffHand];

            // FFXIV average item level uses twelve weighted slots. A two-handed
            // main hand contributes twice; a main/off-hand pair contributes once each.
            long fixedTotal = 0;
            if (mainHand != null && mainHand.IsFilled)
                fixedTotal += mainHand.Item.ItemLevel * (offHand != null && offHand.IsFilled ? 1 : 2);
            if (offHand != null && offHand.IsFilled)
                fixedTotal += offHand.Item.ItemLevel;

            var candidateTotal = candidates.Sum(candidate => (long)candidate.Item.Item.ItemLevel);
            var combinations = 1 << candidates.Count;
            var bestMask = 0;
            var bestDistance = double.MaxValue;
            var bestRemovedCount = int.MaxValue;

            for (var mask = 0; mask < combinations; mask++)
            {
                long removed = 0;
                var removedCount = 0;

                for (var index = 0; index < candidates.Count; index++)
                {
                    if ((mask & (1 << index)) == 0)
                        continue;

                    removed += candidates[index].Item.Item.ItemLevel;
                    removedCount++;
                }

                var simulatedItemLevel =
                    (fixedTotal + candidateTotal - removed) / (double)ItemLevelDivisor;
                var distance = Math.Abs(simulatedItemLevel - RequiredItemLevel);

                if (distance < bestDistance - 0.0001 ||
                    (Math.Abs(distance - bestDistance) < 0.0001 &&
                     removedCount < bestRemovedCount))
                {
                    bestDistance = distance;
                    bestRemovedCount = removedCount;
                    bestMask = mask;
                }
            }

            return candidates
                .Where((_, index) => (bestMask & (1 << index)) != 0)
                .ToList();
        }

        private static InventoryBagId GetArmoryBag(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head:
                    return InventoryBagId.Armory_Helmet;
                case EquipmentSlot.Body:
                    return InventoryBagId.Armory_Chest;
                case EquipmentSlot.Hands:
                    return InventoryBagId.Armory_Glove;
                case EquipmentSlot.Legs:
                    return InventoryBagId.Armory_Pants;
                case EquipmentSlot.Feet:
                    return InventoryBagId.Armory_Boots;
                case EquipmentSlot.Earring:
                    return InventoryBagId.Armory_Earrings;
                case EquipmentSlot.Necklace:
                    return InventoryBagId.Armory_Necklace;
                case EquipmentSlot.Bracelet:
                    return InventoryBagId.Armory_Writs;
                case EquipmentSlot.Ring1:
                case EquipmentSlot.Ring2:
                    return InventoryBagId.Armory_Rings;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(slot),
                        slot,
                        "Unsupported removable equipment slot");
            }
        }

        private sealed class GearCandidate
        {
            public GearCandidate(EquipmentSlot slot, BagSlot item)
            {
                Slot = slot;
                Item = item;
            }

            public EquipmentSlot Slot { get; }
            public BagSlot Item { get; }
        }
    }
}
