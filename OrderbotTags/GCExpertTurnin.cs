using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Clio.XmlEngine;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("GCExpertTurnin")]
    public class GCExpertTurnin : LLProfileBehavior
    {
        [XmlAttribute("ItemIDs")]
        [XmlAttribute("ItemIds")]
        [XmlAttribute("ItemID")]
        [XmlAttribute("ItemId")]
        [DefaultValue(new int[0])]
        private int[] ItemIds { get; set; }

        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public GCExpertTurnin() : base()
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
            return new ActionRunCoroutine(r => DoGCExpertTurnin());
        }

        private async Task DoGCExpertTurnin()
        {
            if (ItemIds.Length == 0)
            {
                await LlamaLibrary.Helpers.GrandCompanyHelper.GCHandInExpert();
                _isDone = true;
                return;
            }

            await LlamaLibrary.Helpers.ExpertDelivery.DeliverItems(ItemIds.Select(i => (uint)i));

            _isDone = true;
        }
    }
}