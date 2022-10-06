using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("EnterSoloDuty")]
    public class EnterSoloDuty : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("NpcId")]
        [XmlAttribute("NpcID")]
        public int InteracttId { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public EnterSoloDuty() : base()
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
            return new ActionRunCoroutine(r => EnterSoloDutyTask());
        }

        private async Task EnterSoloDutyTask()
        {
            var npcId = GameObjectManager.GetObjectByNPCId((uint)InteracttId);

            if (!npcId.IsWithinInteractRange)

            {
                var _target = npcId.Location;
                Navigator.PlayerMover.MoveTowards(_target);
                while (_target.Distance2D(Core.Me.Location) >= 3)
                {
                    Navigator.PlayerMover.MoveTowards(_target);
                    await Coroutine.Sleep(100);
                }
                Navigator.PlayerMover.MoveStop();
            }

            npcId.Interact();

            await Coroutine.Wait(10000, () => DifficultySelectYesNo.IsOpen || SelectYesno.IsOpen);

            if (!DifficultySelectYesNo.IsOpen || !SelectYesno.IsOpen)
            {
                npcId.Interact();
            }

            if (DifficultySelectYesNo.IsOpen)
            {
                DifficultySelectYesNo.Proceed(2);
                await Coroutine.Wait(10000, () => CommonBehaviors.IsLoading);
            }

            if (SelectYesno.IsOpen)
            {
                SelectYesno.Yes();
                await Coroutine.Wait(10000, () => CommonBehaviors.IsLoading);
            }

            while (CommonBehaviors.IsLoading)
            {
                await Coroutine.Wait(20000, () => !CommonBehaviors.IsLoading);
                await Coroutine.Sleep(500);
            }

            _isDone = true;
        }
    }
}