using System.Linq;
using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot.Enums;
using ff14bot.Managers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("RetainerItemDump")]
    public class RetainerItemDump : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public RetainerItemDump() : base() { }

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
            return new ActionRunCoroutine(r => RetainerItemDumpTask());
        }

        private async Task RetainerItemDumpTask()
        {

            await LlamaLibrary.Retainers.RetainerRoutine.ReadRetainers(()=> LlamaLibrary.Retainers.RetainerRoutine.DumpItems());

            _isDone = true;
        }

        public override bool IsDone => _isDone;
    }
}