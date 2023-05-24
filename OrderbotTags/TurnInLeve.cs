using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("TurnInLeve")]
    public class TurnInLeve : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("XYZ")]
        public Vector3 Location { get; set; }

        [XmlAttribute("NpcId")]
        public uint NpcId { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public TurnInLeve() : base()
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

            return new ActionRunCoroutine(r => TurnInLeveTask());
        }

        private async Task TurnInLeveTask()
        {
            var npc = GameObjectManager.GetObjectByNPCId(NpcId);

            while (!Conversation.IsOpen && !Talk.DialogOpen)
            {
                // Movement
                if (Core.Me.Distance2D(Location) > 2.5)
                {
                    Logging.WriteDiagnostic($"Moving to {Location}");
                    await Navigation.FlightorMove(Location);
                    await CommonTasks.Land();
                }

                npc.Interact();
                await Coroutine.Wait(5000, () => Conversation.IsOpen || Talk.DialogOpen);
            }

            if (Talk.DialogOpen)
            {
                while (!Conversation.IsOpen)
                {
                    Talk.Next();
                    await Coroutine.Wait(500, () => !Talk.DialogOpen);
                    await Coroutine.Wait(500, () => Talk.DialogOpen);
                    await Coroutine.Yield();
                }
            }

            if (Conversation.IsOpen)
            {
                Conversation.SelectLine(0);
                await Coroutine.Wait(2000, () => Talk.DialogOpen || JournalResult.IsOpen);
            }

            if (Talk.DialogOpen)
            {
                while (!JournalResult.IsOpen)
                {
                    Talk.Next();
                    await Coroutine.Wait(500, () => !Talk.DialogOpen);
                    await Coroutine.Wait(500, () => Talk.DialogOpen);
                    await Coroutine.Yield();
                }
            }

            if (JournalResult.IsOpen)
            {
                JournalResult.Complete();
                await Coroutine.Wait(500, () => SelectYesno.IsOpen);
                if (SelectYesno.IsOpen) // Checked for YesNo here to account for capped seals
                {
                    SelectYesno.Yes();
                }
            }

            await GeneralFunctions.StopBusy();

            _isDone = true;
        }

    }
}