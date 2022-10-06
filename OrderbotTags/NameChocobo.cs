using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("NameChocobo")]
    public class NameChocobo : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;


        public NameChocobo() : base()
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
            return new ActionRunCoroutine(r => NameThatChocobo());
        }

        private async Task NameThatChocobo()
        {
            var name = await LlamaLibrary.Utilities.RetainerHire.GetName();

            var npcId = GameObjectManager.GetObjectByNPCId(1006002);

            if (!npcId.IsWithinInteractRange)

            {
                var _target = npcId.Location;
                Navigator.PlayerMover.MoveTowards(_target);
                while (_target.Distance2D(Core.Me.Location) >= 4)
                {
                    Navigator.PlayerMover.MoveTowards(_target);
                    await Coroutine.Sleep(100);
                }
                Navigator.PlayerMover.MoveStop();
            }

            npcId.Interact();

            await Coroutine.Wait(5000, () => Talk.DialogOpen || InputString.Instance.IsOpen);

            if (!Talk.DialogOpen)
            {
                npcId.Interact();

                await Coroutine.Wait(5000, () => Talk.DialogOpen);
            }


            while (Talk.DialogOpen)
            {
                Talk.Next();
                await Coroutine.Wait(500, () => !Talk.DialogOpen);
                await Coroutine.Wait(500, () => Talk.DialogOpen);
                await Coroutine.Yield();
            }

            if (InputString.Instance.IsOpen)
            {
                Log.Debug("Waiting a sec to enter name");
                await Coroutine.Sleep(1000);
                InputString.Instance.Confirm(name);
            }

            await Coroutine.Wait(5000, () => SelectYesno.IsOpen);

            if (SelectYesno.IsOpen)
            {
                Log.Debug("Selecting Yes");
                SelectYesno.Yes();
            }

            _isDone = true;
        }
    }
}