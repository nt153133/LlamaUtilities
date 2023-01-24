using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLToast")]
    public class LLToast : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Message")]
        [XmlAttribute("message")]
        public string Message { get; set; }

        [XmlAttribute("Duration")]
        [XmlAttribute("Duration")]
        [DefaultValue(25000)]
        public int DisplayTime { get; set; }

        [XmlAttribute("Red")]
        [XmlAttribute("red")]
        [DefaultValue(29)]
        public int Red { get; set; }

        [XmlAttribute("Blue")]
        [XmlAttribute("blue")]
        [DefaultValue(213)]
        public int Blue { get; set; }

        [XmlAttribute("Green")]
        [XmlAttribute("green")]
        [DefaultValue(226)]
        public int Green { get; set; }

        [XmlAttribute("Font")]
        [XmlAttribute("font")]
        [DefaultValue("Gautami")]
        public string Font { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LLToast() : base() { }

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
            return new ActionRunCoroutine(r => SendToast(Message));
        }

        private Task SendToast(string message)
        {
            Core.OverlayManager.AddToast(() => $"" + message, TimeSpan.FromMilliseconds(DisplayTime), System.Windows.Media.Color.FromRgb((byte)Red, (byte)Green, (byte)Blue), System.Windows.Media.Color.FromRgb(13, 106, 175), new System.Windows.Media.FontFamily(Font));

            _isDone = true;
            return Task.CompletedTask;
        }
    }
}