using System;
using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot;
using LlamaLibrary.ScriptConditions;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LisbethCheck")]
    [XmlElement("CheckLisbeth")]
    public class LisbethCheck : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LisbethCheck() : base()
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
            return new ActionRunCoroutine(r => LisbethCheckTask());
        }

        private async Task LisbethCheckTask()
        {
            if (await Helpers.CheckLisbeth())
            {
                _isDone = true;
                return;
            }

            Core.OverlayManager.AddToast(() => "Please Install And Configure Lisbeth", TimeSpan.FromMilliseconds(25000), System.Windows.Media.Color.FromRgb(29, 213, 226), System.Windows.Media.Color.FromRgb(13, 106, 175), new System.Windows.Media.FontFamily("Gautami"));
            TreeRoot.Stop("Lisbeth is not installed or configured");
            _isDone = true;
        }
    }
}