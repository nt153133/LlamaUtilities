using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Managers;
using LlamaLibrary.Data;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("UnlockTTCard")]
    public class UnlockTTCard : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("CardName")]
        [DefaultValue(null)]
        public string CardName { get; set; }


        public override bool HighPriority => true;
        public override bool IsDone => _isDone;

        public UnlockTTCard() : base() { }

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
            return new ActionRunCoroutine(r => UnlockTTCardTask());
        }

        private async Task UnlockTTCardTask()
        {

            foreach (var bagslot in InventoryManager.FilledSlots.Where(bagslot => bagslot.RawItemId == TripleTriadCards.GetCardByName(CardName).ItemId))
            {
                var count = bagslot.Count;
                for (var i = 0; i < count; i++)
                {
                    Log.Information($"Useing item to unlock {TripleTriadCards.GetCardByName(CardName).Name}");
                    bagslot.UseItem();
                    await Coroutine.Wait(5000, () => Core.Me.IsCasting);
                    await Coroutine.Wait(5000, () => !Core.Me.IsCasting);
                    await Coroutine.Sleep(5000);
                }
            }

            _isDone = true;
        }
    }
}