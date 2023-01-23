using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.RemoteAgents;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("AWGrowthTrade")]
    public class AWGrowthTrade : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("ItemToSpend")]
        public int ItemToSpend { get; set; }

        [XmlAttribute("Qty")]
        public int Qty { get; set; }

        [XmlAttribute("Overload")]
        [DefaultValue(true)]
        public bool Overload { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public AWGrowthTrade() : base()
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
            return new ActionRunCoroutine(r => AWGrowthTradeTask());
        }

        private async Task AWGrowthTradeTask()
        {
            var result = await AgentAWGrowthFragTrade.BuyCrystalSand((uint)ItemToSpend,Qty,Overload);

            _isDone = true;
        }
    }
}