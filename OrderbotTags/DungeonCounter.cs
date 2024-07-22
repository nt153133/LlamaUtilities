using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Helpers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("DungeonCounter")]
    public class DungeonCounter : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        [XmlAttribute("MaxCount")]
        public int MaxCount { get; set; }

        public override bool IsDone => _isDone;

        public static int _counter;

        public DungeonCounter() : base() { }

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
            return new ActionRunCoroutine(r => CounterTask());
        }

        private async Task CounterTask()
        {
            // Increment the counter
            _counter++;

            // Compare the counter with the saved variable
            if (_counter <= MaxCount)
            {
                // Do one thing if counter is less than savedVariable
                Log.Information($"Counter ({_counter}) is less than ({MaxCount}). Continuing profile.");
            }
            else
            {
                // Do another thing if counter is greater than or equal to savedVariable
                Log.Information($"Counter is more than ({MaxCount}). Stopping bot and resetting counter.");
                _counter = 0;
                TreeRoot.Stop($"Counter is more than ({MaxCount})");
            }

            _isDone = true;
        }
    }
}