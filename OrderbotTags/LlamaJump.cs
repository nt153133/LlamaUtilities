using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot.Managers;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using TreeSharp;
using Action = TreeSharp.Action;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LlamaJump")]
    public class LlamaJump : LLProfileBehavior
    {
        private bool _isDone;
        private bool _isOpening;

        public override bool IsDone => _isDone;

        [XmlAttribute("Start")]
        public Vector3 Start { get; set; }

        [XmlAttribute("End")]
        public Vector3 End { get; set; }

        public override bool HighPriority => true;

        public LlamaJump() : base() { }

        protected override void OnStart()
        {
        }

        protected override void OnDone()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
            _isOpening = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => JumpLikeALlama());
        }

        private async Task JumpLikeALlama()
        {
            var jump = new JumpNav(Start, End);
            var result = await jump.Jump();
            //Log.Information($"Jump result: {result}");
            _isDone = true;
        }
    }
}