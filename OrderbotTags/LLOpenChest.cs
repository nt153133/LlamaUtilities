using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using LlamaLibrary.Logging;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLOpenChest")]
    public class LLOpenChest : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LLOpenChest() : base() { }

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
            return new ActionRunCoroutine(r => OpenChestTask());
        }

        private async Task OpenChestTask()
        {
            await LlamaLibrary.Helpers.GeneralFunctions.OpenChests();

            _isDone = true;
        }
    }
}