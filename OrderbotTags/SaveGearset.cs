using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Behavior;
using ff14bot.Managers;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;
using static LlamaLibrary.Helpers.GeneralFunctions;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("SaveGearset")]
    public class SaveGearset : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public SaveGearset() : base() { }

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
            return new ActionRunCoroutine(r => SaveGearsetTask());
        }

        private async Task SaveGearsetTask()
        {
            if (_isDone)
            {
                await Coroutine.Yield();
                return;
            }

            if (Class2JobHotbar.Instance.IsOpen)
            {
                Class2JobHotbar.Instance.Transfer();
            }

            await CommonTasks.StopAndDismount();
            ChatManager.SendChat("/gs save");

            _isDone = true;
        }
    }
}