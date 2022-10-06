using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Behavior;
using ff14bot.Managers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLChangeZone")]
    public class LLChangeZone : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Heading")]
        public float Heading { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LLChangeZone() : base() { }

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
            return new ActionRunCoroutine(r => ChangeZoneTask(Heading));
        }

        private async Task ChangeZoneTask(float Heading)
        {
            MovementManager.SetFacing(Heading);
            MovementManager.MoveForwardStart();

            await Coroutine.Wait(-1, () => CommonBehaviors.IsLoading);
            Log($"Waiting for loading to finish...");
            await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);

            MovementManager.MoveStop();

            _isDone = true;
        }
    }
}