using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LoadAchievements")]
    public class LoadAchievements : LLProfileBehavior
    {
        public override bool HighPriority => true;
        private bool _isDone;
        public override bool IsDone => _isDone;

        public LoadAchievements() : base()
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
            return new ActionRunCoroutine(r => OpenWindow());
        }

        private async Task OpenWindow()
        {
            if (!Achievement.Instance.IsOpen)
            {
                AgentAchievement.Instance.Toggle();
                await Coroutine.Wait(2000, () => AgentAchievement.Instance.Status != 0);
                await Coroutine.Wait(10000, () => AgentAchievement.Instance.Status == 0);
                if (Achievement.Instance.IsOpen)
                {
                    Achievement.Instance.Close();
                    await Coroutine.Wait(10000, () => !Achievement.Instance.IsOpen);
                }
            }

            _isDone = true;
        }
    }
}