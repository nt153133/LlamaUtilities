//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using TreeSharp;
using Action = TreeSharp.Action;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("UseMedicine")]
    public class UseMedicine : LLProfileBehavior
    {


        [XmlAttribute("ItemId")]
        public uint ItemId { get; set; }

        [XmlAttribute("MinDuration")]
        [DefaultValue(5)]
        public int MinDuration { get; set; }


        [XmlAttribute("HqOnly")]
        public bool HqOnly { get; set; }

        [XmlAttribute("NqOnly")]
        public bool NqOnly { get; set; }

        protected bool _IsDone;


        #region Overrides of ProfileBehavior

        public override bool IsDone
        {
            get
            {
                return _IsDone;
            }
        }

        #endregion

        private BagSlot itemslot;
        private Item itemData;
        protected override void OnStart()
        {

            itemData = DataManager.GetItem(ItemId);
            if (itemData == null)
            {
                TreeRoot.Stop("Couldn't locate item with id of " + ItemId);
                return;
            }

            if (HqOnly && NqOnly)
            {
                TreeRoot.Stop("Both HqOnly and NqOnly cannot be true");
                return;
            }

            var validItems = InventoryManager.FilledSlots.Where(r => r.RawItemId == ItemId).ToArray();

            if (validItems.Length == 0)
            {
                TreeRoot.Stop(string.Format("We don't have any {0} {1} in our inventory.", itemData.CurrentLocaleName,ItemId));
                return;
            }



            if (HqOnly)
            {
                var items = validItems.Where(r => r.IsHighQuality).ToArray();
                if (items.Any())
                {
                    itemslot = items.FirstOrDefault();
                }
                else
                {
                    TreeRoot.Stop("HqOnly and we don't have any Hq medicine in the inventory with id " + ItemId);
                    return;
                }
            }
            else if (NqOnly)
            {
                var items = validItems.Where(r => !r.IsHighQuality).ToArray();
                if (items.Any())
                {
                    itemslot = items.FirstOrDefault();
                }
                else
                {
                    TreeRoot.Stop("NqOnly and we don't have any Nq medicine in the inventory with id " + ItemId);
                    return;
                }
            }
            else
            {
                itemslot = validItems.OrderBy(r => r.IsHighQuality).FirstOrDefault();
            }


        }




        protected override void OnResetCachedDone()
        {
            _IsDone = false;
        }





        public override string StatusText
        {
            get
            {
                if (itemData != null)
                {
                    return "Drinking " + itemData.CurrentLocaleName;
                }
                return "";
            }
        }


        private async Task<bool> Eatfood()
        {
            bool shouldEat = false;
            bool alreadyPresent = false;
            if (Core.Player.HasAura(49))
            {
                var auraInfo = Core.Player.GetAuraById(49);
                if (auraInfo.TimespanLeft.TotalMinutes < MinDuration)
                {
                    shouldEat = true;
                    alreadyPresent = true;
                }
            }
            else
            {
                shouldEat = true;
            }



            if (shouldEat)
            {
                if (CraftingLog.IsOpen || CraftingManager.IsCrafting)
                {
                    await Coroutine.Wait(Timeout.InfiniteTimeSpan, () => CraftingLog.IsOpen);
                    await Coroutine.Sleep(1000);
                    CraftingLog.Close();
                    await Coroutine.Yield();
                    await Coroutine.Wait(Timeout.InfiniteTimeSpan, () => !CraftingLog.IsOpen);
                    await Coroutine.Wait(Timeout.InfiniteTimeSpan, () => !CraftingManager.AnimationLocked);
                }

                Log("Waiting until the item is usable.");
                    await Coroutine.Wait(Timeout.InfiniteTimeSpan, () => itemslot.CanUse(null));

                Log("Drinking {0}",itemData.CurrentLocaleName);
                itemslot.UseItem();
                await Coroutine.Wait(10000, () => Core.Player.HasAura(49));

                if (!Core.Player.HasAura(49))
                {
                    Log("Waiting for the aura to appear");
                    await Coroutine.Wait(Timeout.InfiniteTimeSpan, () => Core.Player.HasAura(49));
                }
                else
                {
                    Log("Waiting until the duration is refreshed");
                    await Coroutine.Wait(Timeout.InfiniteTimeSpan, () => Core.Player.GetAuraById(49).TimespanLeft.TotalMinutes > MinDuration);
                }

            }

            _IsDone = true;


            return true;
        }

        private bool dialogwasopen;
        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(ctx => Eatfood());
        }
    }
}
