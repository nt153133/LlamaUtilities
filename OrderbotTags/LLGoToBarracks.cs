using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.NeoProfiles;
using ff14bot.Pathing.Service_Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.Enums;
using LlamaLibrary.Helpers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLGoToBarracks")]
    public class LLGoToBarracks : ProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

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
            return new ActionRunCoroutine(r => GoToBarracksTask());
        }

        private async Task GoToBarracksTask()
        {
            if (WorldManager.ZoneId != 534 && WorldManager.ZoneId != 535 && WorldManager.ZoneId != 536)
            {
                if (Navigator.NavigationProvider == null)
                {
                    Navigator.PlayerMover = new SlideMover();
                    Navigator.NavigationProvider = new ServiceNavigationProvider();
                }
                // Not in Barracks
                Log($"Moving to Barracks");
                uint[] entranceIds = { 2007527,2007529,2006962 };
                var entranceNpc = GameObjectManager.GameObjects.Where(r => r.IsTargetable && r.IsValid && entranceIds.Contains(r.NpcId)).OrderBy(r => r.Distance()).FirstOrDefault();
                if (entranceNpc != null)
                {
                    while (Core.Me.Location.Distance2D(entranceNpc.Location) > 1.5f)
                    {
                        await Coroutine.Yield();
                        await Navigation.FlightorMove(entranceNpc.Location);
                    }
                }
                await GrandCompanyHelper.InteractWithNpc(GCNpc.Entrance_to_the_Barracks);
                await Coroutine.Wait(5000, () => SelectYesno.IsOpen);
                await Buddy.Coroutines.Coroutine.Sleep(500);
                if (ff14bot.RemoteWindows.SelectYesno.IsOpen)
                {
                    Log($"Selecting Yes.");
                    ff14bot.RemoteWindows.SelectYesno.ClickYes();
                }

                await Coroutine.Wait(5000, () => CommonBehaviors.IsLoading);
                while (CommonBehaviors.IsLoading)
                {
                    Log($"Waiting for zoning to finish...");
                    await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);
                }
            }
            else
            {
                Log($"Already in barracks");
            }


            _isDone = true;
        }
    }
}