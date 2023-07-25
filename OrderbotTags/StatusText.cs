using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("StatusText")]
    public class StatusText : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Message")]
        [XmlAttribute("message")]
        public string Message { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public StatusText() : base() { }

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
            return new ActionRunCoroutine(r => SetStatusText());
        }

        private Task SetStatusText()
        {
            TreeRoot.StatusText = Message;

            _isDone = true;
            return Task.CompletedTask;
        }
    }
}