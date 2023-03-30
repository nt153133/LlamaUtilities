using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;
using Character = ff14bot.Objects.Character;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("BuyShopExchangeCurrency")]
    public class BuyShopExchangeCurrency : LLProfileBehavior
    {
        private bool _isDone;
        private bool _isOpening;

        public override bool IsDone => _isDone;

        [XmlAttribute("NpcId")]
        public int NpcId { get; set; }

        [XmlAttribute("ItemId")]
        public int ItemId { get; set; }

        [XmlAttribute("SelectString")]
        public int SelectString { get; set; }

        [XmlAttribute("Count")]
        [DefaultValue(1)]
        public int Count { get; set; }

        [XmlAttribute("Dialog")]
        [XmlAttribute("dialog")]
        [DefaultValue(false)]
        public bool Dialog { get; set; } = false;

        public override bool HighPriority => true;

        public BuyShopExchangeCurrency() : base()
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
            return new ActionRunCoroutine(r => BuyItem(ItemId, NpcId, Count, SelectString));
        }

        private async Task BuyItem(int itemId, int npcId, int count, int selectString)
        {
            var unit = GameObjectManager.GetObjectsByNPCId<Character>((uint)npcId).OrderBy(r => r.Distance()).FirstOrDefault();

            if (unit == null)
            {
                _isDone = true;
                return;
            }

            if (!ShopExchangeCurrency.Open && unit.Location.Distance(Core.Me.Location) > 4f)
            {
                await Navigation.OffMeshMove(unit.Location);
                await Coroutine.Sleep(500);
            }

            unit.Interact();

            if (Dialog)
            {
                await Coroutine.Wait(5000, () => Talk.DialogOpen);

                while (Talk.DialogOpen)
                {
                    Talk.Next();
                    await Coroutine.Sleep(1000);
                }
            }

            await Coroutine.Wait(5000, () => Conversation.IsOpen || ShopExchangeCurrency.Open);

            if (Conversation.IsOpen)
            {
                Conversation.SelectLine((uint)selectString);

                if (Dialog)
                {
                    await Coroutine.Wait(5000, () => Talk.DialogOpen);

                    while (Talk.DialogOpen)
                    {
                        Talk.Next();
                        await Coroutine.Sleep(1000);
                    }
                }
            }

            await Coroutine.Wait(5000, () => ShopExchangeCurrency.Open);

            if (ShopExchangeCurrency.Open)
            {
                //Log.Information("ShopExchangeCurrency opened");
                Purchase((uint)itemId, (uint)count);
                await Coroutine.Wait(2000, () => SelectYesno.IsOpen || Request.IsOpen);

                if (SelectYesno.IsOpen)
                {
                    SelectYesno.Yes();
                    await Coroutine.Sleep(1000);
                }

                if (SelectYesno.IsOpen)
                {
                    SelectYesno.Yes();
                    await Coroutine.Sleep(1000);
                }
            }

            await Coroutine.Wait(2000, () => ShopExchangeCurrency.Open);
            if (ShopExchangeCurrency.Open)
            {
                ShopExchangeCurrency.Close();
            }

            _isDone = true;
        }

        public static uint Purchase(uint itemId, uint itemCount)
        {
            if (!ShopExchangeCurrency.Open)
            {
                return 0;
            }

            var items = SpecialShopManager.Items;

            if (items == null || items.Count == 0)
            {
                return 0;
            }

            if (!items.Any(i => i.ItemIds.Contains(itemId)))
            {
                return 0;
            }

            var item = items.FirstOrDefault(i => i.ItemIds.Contains(itemId));

            var count = Math.Min(item.Item0.StackSize, itemCount);
            count = Math.Min(count, CanAfford(item));

            if (count == 0)
            {
                return 0;
            }

            RaptureAtkUnitManager.GetWindowByName("ShopExchangeCurrency").SendAction(4, 3, 0, 3, (ulong)items.IndexOf(item), 3, count, 0, 0);
            return count;
        }

        public static uint CanAfford(SpecialShopItem item)
        {
            if (item.CurrencyExchangeMode == 3)
            {
                return CurrencyHelper.GetAmountOfCurrency(CurrencyHelper.GetCurrencyItemId(item.CurrencyTypes[0])) / item.CurrencyCosts[0];
            }

            return ShopExchangeCurrency.CanAfford(item);
        }
    }
}