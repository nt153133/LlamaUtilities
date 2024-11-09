using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("GCExpertTurnin")]
    public class GCExpertTurnin : LLProfileBehavior
    {
        [XmlAttribute("ItemIDs")]
        [XmlAttribute("ItemIds")]
        [XmlAttribute("ItemID")]
        [XmlAttribute("ItemId")]
        [DefaultValue(new int[0])]
        private int[] ItemIds { get; set; }

        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public GCExpertTurnin() : base()
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
            return new ActionRunCoroutine(r => DoGCExpertTurnin());
        }

        private async Task DoGCExpertTurnin()
        {

            if (DutyManager.InInstance)
            {
                Log.Information($"Currently in an instance, exiting");

                _isDone = true;
                return;
            }

            if (WorldManager.ZoneId == 534 || WorldManager.ZoneId == 535 || WorldManager.ZoneId == 536)
            {
                uint[] npcIds = { 2007528,2006963,2007530 };
                var exitNpc = GameObjectManager.GameObjects.Where(r => r.IsTargetable && npcIds.Contains(r.NpcId)).OrderBy(r => r.Distance()).FirstOrDefault();
                if (exitNpc != null)
                {
                    while (Core.Me.Location.Distance2D(exitNpc.Location) > 1.5f)
                    {
                        await Coroutine.Yield();
                        Navigator.PlayerMover.MoveTowards(exitNpc.Location);
                    }
                    Navigator.PlayerMover.MoveStop();
                    exitNpc.Interact();
                    await Coroutine.Wait(10000, () => SelectYesno.IsOpen);
                    if (!SelectYesno.IsOpen)
                    {
                        exitNpc.Interact();
                        await Coroutine.Wait(10000, () => SelectYesno.IsOpen);
                    }
                    while (SelectYesno.IsOpen)
                    {
                        SelectYesno.Yes();
                        await Coroutine.Wait(10000, () => CommonBehaviors.IsLoading);
                        Log.Information($"Waiting for loading to finish...");
                        await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);
                    }
                }
                else
                {
                    Log.Error($"Couldn't find the exit");
                    return;
                }
            }

            if (ItemIds.Length == 0)
            {
                await LlamaLibrary.Helpers.GrandCompanyHelper.GCHandInExpert();
                _isDone = true;
                return;
            }

            await LlamaLibrary.Helpers.ExpertDelivery.DeliverItems(ItemIds.Select(i => (uint)i));

            _isDone = true;
        }
    }
}