using System.ComponentModel;
using System.Threading.Tasks;
using Clio.XmlEngine;
using LlamaLibrary.Utilities;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLReduce")]
    public class RetainerHire : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        [XmlAttribute("OnlyLimsa")]
        [DefaultValue(false)]
        public bool OnlyLimsa { get; set; } = false;

        public RetainerHire() : base()
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
            return new ActionRunCoroutine(r => RetainerHireTask());
        }

        private async Task RetainerHireTask()
        {
            await LlamaLibrary.Utilities.RetainerHire.HireAllRetainers(OnlyLimsa);

            _isDone = true;
        }
    }
}