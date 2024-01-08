using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot.Enums;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("RetainerJobs")]
    public class RetainerJobs : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        [XmlAttribute("Job")]
        public string Job { get; set; }

        public RetainerJobs() : base()
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
            return new ActionRunCoroutine(r => TheTookOurJobs());
        }

        private async Task TheTookOurJobs()
        {
            // This tag is intended to take in the given job, go buy a level 1 weapon for said job if you don't have it, and then assign your retainers to that job.
            await LlamaLibrary.Utilities.RetainerJobs.GiveThemJobs(Job);

            _isDone = true;
        }
    }
}