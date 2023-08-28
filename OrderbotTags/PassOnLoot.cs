using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("PassOnLoot")]
    public class PassOnLoot : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public PassOnLoot() : base()
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
            return new ActionRunCoroutine(r => PassLoot());
        }

        public async Task PassLoot()
        {
            Log.Information($"Waiting for loot window.");
            await Coroutine.Wait(5000, () => LlamaLibrary.RemoteWindows.NotificationLoot.Instance.IsOpen || NeedGreed.Instance.IsOpen);
            if (LlamaLibrary.RemoteWindows.NotificationLoot.Instance.IsOpen || NeedGreed.Instance.IsOpen)
            {
                await LlamaLibrary.Helpers.GeneralFunctions.PassOnAllLoot();
            }
            else
            {
                Log.Information($"Loot window did not open.");
            }

            _isDone = true;
        }

        public override bool IsDone => _isDone;
    }
}