using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using ff14bot.Objects;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLTurnInGuildLeve")]
    public class LLTurnInGuildLeve : LLProfileBehavior
    {
        private readonly Stopwatch interactTimeout = new Stopwatch();

        private bool checkedTransport;

        private uint iconStringIndex = 9001;

        private bool turnedItemsIn;

        public override bool IsDone => _isDone;
        private bool _isDone;

        [DefaultValue(true)]
        [XmlAttribute("AcceptTransport")]
        public bool AcceptTransport { get; set; }

        [DefaultValue("Collect Reward.")]
        [XmlAttribute("CollectRewardText")]
        public string CollectRewardText { get; set; }

        [XmlAttribute("HqOnly")]
        public bool HqOnly { get; set; }

        [XmlAttribute("MultipleQuests")]
        public bool MultipleQuests { get; set; }

        [XmlAttribute("NqOnly")]
        public bool NqOnly { get; set; }

        [DefaultValue(60)]
        [XmlAttribute("Timeout")]
        public int Timeout { get; set; }

        [DefaultValue("Yes.")]
        [XmlAttribute("YesText")]
        public string YesText { get; set; }

        protected Color Info
        {
            get { return Colors.Plum; }
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected void DoReset()
        {
            interactTimeout.Reset();
            turnedItemsIn = false;
            checkedTransport = false;
            iconStringIndex = 9001;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => Main());
        }

        protected async Task<bool> Main()
        {
            var npc = GameObjectManager.GetObjectByNPCId(NpcId);

            while (!SelectIconString.IsOpen && !SelectString.IsOpen && !Request.IsOpen && !JournalResult.IsOpen && !Talk.DialogOpen)
            {
                // Movement
                if (Core.Me.Distance2D(Location) > 3.5)
                {
                    Logging.WriteDiagnostic($"Moving to {Location}");
                    await Navigation.FlightorMove(Location);
                    return true;
                }

                npc.Interact();
                await Coroutine.Wait(5000, () => Conversation.IsOpen || Talk.DialogOpen);
            }

            if (Talk.DialogOpen)
            {
                await HandleTalk();
            }

            if (SelectIconString.IsOpen)
            {
                if (iconStringIndex == 9001)
                {
                    iconStringIndex = (uint)SelectIconString.Lines().Count - 1;
                }

                // We will just click the last quest and decrement until we have either no quests left or none to turn in.
                SelectIconString.ClickSlot(--iconStringIndex);
                await Coroutine.Sleep(500);

                if (iconStringIndex == uint.MaxValue)
                {
                    Logging.WriteDiagnostic("Nothing to turn in");

                    _isDone = true;
                    return true;
                }

                return true;
            }

            if (Talk.DialogOpen)
            {
                await HandleTalk();
            }

            if (SelectString.IsOpen)
            {
                var lines = SelectString.Lines();

                // If Collect Reward exists, we click that; otherwise we will click Close. (-1 as uint = uint.MaxValue)
                var index = Array.FindIndex(lines.ToArray(), x => x.Equals(CollectRewardText, StringComparison.OrdinalIgnoreCase));

                if (index > -1)
                {
                    SelectString.ClickSlot((uint)index);
                    if (await Coroutine.Wait(1000, () => Talk.DialogOpen))
                    {
                        await HandleTalk();
                    }

                    await Coroutine.Yield();
                    return true;
                }

                // If yes is an option, click it to turn in more items.(crafting)
                index = Array.FindIndex(lines.ToArray(), x => x.Equals(YesText, StringComparison.OrdinalIgnoreCase));

                if (index > -1)
                {
                    SelectString.ClickSlot((uint)index);
                    await Coroutine.Yield();
                    return true;
                }

                _isDone = true;
                SelectString.ClickSlot((uint)index);
                await Coroutine.Yield();
                return true;
            }

            if (Talk.DialogOpen)
            {
                await HandleTalk();
            }

            if (Request.IsOpen)
            {
                var itemCount = Request.ItemCount;

                var itemId = Request.RequestedItems[0].RawItemId;

                IEnumerable<BagSlot> itemSlots =
                    InventoryManager.FilledInventoryAndArmory
                        .Where(bs => bs.RawItemId == itemId
                                     && bs.BagId != InventoryBagId.EquippedItems
                                     && !Blacklist.Contains((uint)bs.Pointer.ToInt64(), BlacklistFlags.Loot)).ToArray();

                if (HqOnly)
                {
                    itemSlots = itemSlots.Where(bs => bs.IsHighQuality);
                }

                if (NqOnly)
                {
                    itemSlots = itemSlots.Where(bs => !bs.IsHighQuality);
                }

                var items = itemSlots.Take(itemCount).ToArray();

                if (items.Length == 0)
                {
                    await Coroutine.Wait(2000, () => !Request.IsOpen && SelectYesno.IsOpen);

                    Logging.WriteDiagnostic($"No items to turn in. Settings -&gt; HqOnly: {HqOnly}, NqOnly: {NqOnly}, ItemId: {itemId}");
                    _isDone = true;
                    return true;
                }

                var isHq = items.Any(i => i.IsHighQuality);
                var itemName = items[0].EnglishName;
                var requestAttempts = 0;
                while (Request.IsOpen && requestAttempts++ < 5)
                {
                    foreach (var item in items)
                    {
                        item.Handover();
                        await Coroutine.Yield();
                    }

                    await Coroutine.Wait(1000, () => Request.HandOverButtonClickable);

                    if (Request.HandOverButtonClickable)
                    {
                        Request.HandOver();

                        if (isHq)
                        {
                            await Coroutine.Wait(2000, () => !Request.IsOpen && SelectYesno.IsOpen);
                        }
                        else
                        {
                            await Coroutine.Wait(2000, () => !Request.IsOpen);
                        }
                    }
                }

                turnedItemsIn = true;

                if (SelectYesno.IsOpen)
                {
                    SelectYesno.ClickYes();
                    await Coroutine.Yield();
                    Logging.WriteDiagnostic($"Turned in HQ {itemName} on {WorldManager.EorzaTime} ET");
                }
                else
                {
                    Logging.WriteDiagnostic($"Turned in {itemName} on {WorldManager.EorzaTime} ET");
                }

                await HandleTalk();

                await Coroutine.Wait(2000, () => JournalResult.IsOpen);
                return true;
            }

            if (JournalResult.IsOpen)
            {
                await Coroutine.Wait(2000, () => JournalResult.ButtonClickable);
                JournalResult.Complete();
                Logging.WriteDiagnostic($"Completed on {WorldManager.EorzaTime} ET");
                await Coroutine.Wait(2000, () => SelectYesno.IsOpen);
                if (SelectYesno.IsOpen)
                {
                    Logging.WriteDiagnostic($"Clicking yes");
                    SelectYesno.ClickYes();
                }
                await Coroutine.Wait(2000, () => !JournalResult.IsOpen);

                await HandleTalk();

                return true;
            }

            if (!await WaitForOpenWindow())
            {
                if (MultipleQuests)
                {
                    Logging.WriteDiagnostic($"Looks like no windows are open, lets clear our target and try again.");
                    CloseWindows();
                    Core.Player.ClearTarget();
                }
                else
                {
                    _isDone = true;
                }
            }

            Logging.WriteDiagnostic("TurnInGuildLeve Done");
            _isDone = true;
            return true;
        }

        protected override void OnDone()
        {
            interactTimeout.Stop();

            CloseWindows();
        }

        private void CloseWindows()
        {
            if (SelectYesno.IsOpen)
            {
                SelectYesno.ClickNo();
            }

            if (Request.IsOpen)
            {
                Request.Cancel();
            }

            if (JournalResult.IsOpen)
            {
                JournalResult.Decline();
            }

            if (SelectString.IsOpen)
            {
                SelectString.ClickSlot(uint.MaxValue);
            }

            if (SelectIconString.IsOpen)
            {
                SelectIconString.ClickSlot(uint.MaxValue);
            }

            if (Shop.Open)
            {
                Shop.Close();
            }
        }

        private async Task<bool> HandleTalk(int interval = 100)
        {
            Logging.WriteDiagnostic("Handling talk");
            await Coroutine.Wait(1000, () => Talk.DialogOpen);

            var ticks = 0;
            while (ticks++ < 50 && Talk.DialogOpen)
            {
                Talk.Next();
                await Coroutine.Sleep(interval);
            }

            return await WaitForOpenWindow();
        }

        private async Task<bool> WaitForOpenWindow()
        {
            return
                await
                    Coroutine.Wait(3000, () => SelectIconString.IsOpen || SelectString.IsOpen || Request.IsOpen || JournalResult.IsOpen);
        }

        #region IInteractWithNpc Members

        [XmlAttribute("XYZ")]
        public Vector3 Location { get; set; }

        [XmlAttribute("NpcId")]
        public uint NpcId { get; set; }

        #endregion IInteractWithNpc Members
    }
}