using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using Buddy.Coroutines;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using ff14bot.RemoteWindows.ChocoboRace;
using LlamaLibrary;
using LlamaUtilities.LlamaUtilities.Settings;
using LlamaLibrary.Helpers;
using LlamaLibrary.Logging;

namespace LlamaUtilities.LlamaUtilities
{
    public static class PandaRacer
    {
        private static readonly LLogger Log = new LLogger("PandaRacer", Colors.Chartreuse);

        // My Variables
        private static uint _blueSpeedPlatform = 2005038; // Blue go fast platform

        // Items
        public enum ChocoboItems : uint
        {
            ChocoPotion = 1, // Instantly restores stamina by 10%.
            StaminaTablet = 2, // Prevents stamina reduction for 10 seconds. If stamina is at zero, restores a small amount of stamina.
            SprintShoes = 3, // Summons a quick burst of speed for 3s. Functionally identical to the ability Choco Dash III.
            ChocoAether = 4, // Allows a previously used race ability to be executed again.
            BacchusWater = 5, // Sends the forerunning chocobo into a Frenzied state for 15 seconds and increases its stamina loss.
            Graviball = 6, // Inflicts the forerunning chocobo with Heavy for 10 seconds.
            BriarCaltrop = 7, // Surrounds your chocobo with a ring of thorns which will sap the stamina of any chocobos that enter at a rate of 5% per second (up to 4 seconds).
            HeroTonic = 8, // Invigorates your chocobo for 15 seconds, increasing its overall performance while preventing and nullifying any current enfeeblements

            ChocoMeteor =
                9, // Summons a meteor from the heavens to wreak havoc on all forerunning chocobos. Inflicts 20% stamina damage to all chocobos in front of you, as well as the "Lamed" status for 8 seconds which prevents all acceleration, including from items and abilities.
            StaminaSwapper = 10, // Interchanges your chocobo's stamina with that of a forerunning chocobo. Swap is made only if the forerunning chocobo has more stamina than yours.
            Spiderweb = 11, // Renders the chocobo running immediately behind you unable to use items for 10 seconds.
        }

        public enum ChocoboAbilities : uint
        {
            ChocoDashII = 2, // Instantly restores stamina by 10%.
        }

        // Ability
        private static int _paradigmShift = 51;

        private static List<int> RaceMaps = new()
        {
            391, // Tranquil Paths
            390, // Sagolii Road
            389, // Costa del Sol
        };

        public static async Task<bool> Race()
        {
            while (RaceSettings.Instance.Loop)
            {
                if (!RaceMaps.Contains(WorldManager.ZoneId))
                {
                    await QueueChocoRace();
                }

                while (RaceMaps.Contains(WorldManager.ZoneId))
                {
                    var speedPlatform = GameObjectManager.GetObjectsOfType<EventObject>().Where(i => i.NpcId == _blueSpeedPlatform && i.InLineOfSight() && i.IsVisible && i.IsTargetable && i.Location.Distance2D(Core.Me.Location) <= 10);

                    while (!RaceChocoboResult.IsOpen)
                    {
                        switch (ChocoboRaceManager.Stamina)
                        {
                            case > 20:
                                await GoFaster();
                                break;
                            case <= 20:
                                //Log.Information($"Waiting to restore Stamina.");
                                await Coroutine.Wait(-1, () => ChocoboRaceManager.Stamina > 30 || ChocoboRaceManager.CanUseItem || ChocoboRaceManager.CanUseAbility || RaceChocoboResult.IsOpen && RaceMaps.Contains(WorldManager.ZoneId));
                                if (ChocoboRaceManager.CanUseItem && !RaceChocoboResult.IsOpen)
                                {
                                    await UseItem();
                                }

                                if (ChocoboRaceManager.CanUseAbility && !RaceChocoboResult.IsOpen)
                                {
                                    await UseAbility();
                                }

                                break;
                        }
                    }

                    if (RaceChocoboResult.IsOpen)
                    {
                        Log.Information($"Leaving race.");
                        RaceChocoboResult.Close();
                        await Coroutine.Wait(-1, () => !DutyManager.InInstance);
                    }
                }
            }

            TreeRoot.Stop("Stop Requested");
            return true;
        }

        private static async Task QueueChocoRace()
        {
            await GeneralFunctions.StopBusy(false);

            while (DutyManager.QueueState == QueueState.None)
            {
                // You can't queue Racing as undersize and this causes and error, so turn it off
                if (GameSettingsManager.JoinWithUndersizedParty)
                {
                    Log.Information($"Turning off Undersized");
                    GameSettingsManager.JoinWithUndersizedParty = false;
                }

                Log.Information($"Queuing for {DataManager.InstanceContentResults[(uint)RaceSettings.Instance.RaceChoice].CurrentLocaleName}");
                DutyManager.Queue(DataManager.InstanceContentResults[(uint)RaceSettings.Instance.RaceChoice]);
                await Coroutine.Wait(10000, () => DutyManager.QueueState == QueueState.CommenceAvailable || DutyManager.QueueState == QueueState.JoiningInstance);

                if (DutyManager.QueueState != QueueState.None)
                {
                    Log.Information($"Queued for {DataManager.InstanceContentResults[(uint)RaceSettings.Instance.RaceChoice].CurrentLocaleName}");
                }
                else if (DutyManager.QueueState == QueueState.None)
                {
                    Log.Error("Something went wrong, queuing again...");
                }
            }

            while (DutyManager.QueueState != QueueState.None || DutyManager.QueueState != QueueState.InDungeon ||
                   CommonBehaviors.IsLoading)
            {
                if (DutyManager.QueueState == QueueState.CommenceAvailable)
                {
                    Log.Information("Waiting for queue pop.");
                    await Coroutine.Wait(-1,
                                         () => DutyManager.QueueState == QueueState.JoiningInstance ||
                                               DutyManager.QueueState == QueueState.None);
                }

                if (DutyManager.QueueState == QueueState.JoiningInstance)
                {
                    var rnd = new Random();
                    var waitTime = rnd.Next(1000, 10000);

                    Log.Information($"Race queue popped, commencing in {waitTime / 1000} seconds.");
                    await Coroutine.Sleep(waitTime);
                    DutyManager.Commence();
                    await Coroutine.Wait(-1, () => DutyManager.QueueState == QueueState.LoadingContent || DutyManager.QueueState == QueueState.CommenceAvailable);
                }

                if (DutyManager.QueueState == QueueState.LoadingContent)
                {
                    Log.Information("Waiting for everyone to accept queue.");
                    await Coroutine.Wait(-1, () => CommonBehaviors.IsLoading || DutyManager.QueueState == QueueState.CommenceAvailable);
                    await Coroutine.Sleep(1000);
                }

                if (CommonBehaviors.IsLoading)
                {
                    break;
                }

                await Coroutine.Sleep(500);
            }

            if (DutyManager.QueueState == QueueState.None)
            {
                return;
            }

            await Coroutine.Sleep(500);
            if (CommonBehaviors.IsLoading)
            {
                await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);
            }

            if (QuestLogManager.InCutscene)
            {
                TreeRoot.StatusText = "InCutscene";
                if (ff14bot.RemoteAgents.AgentCutScene.Instance != null)
                {
                    ff14bot.RemoteAgents.AgentCutScene.Instance.PromptSkip();
                    await Coroutine.Wait(2000, () => SelectString.IsOpen || SelectYesno.IsOpen);

                    if (SelectString.IsOpen)
                    {
                        SelectString.ClickSlot(0);
                    }

                    if (SelectYesno.IsOpen)
                    {
                        SelectYesno.Yes();
                    }
                }
            }

            await Coroutine.Wait(-1, () => RaceMaps.Contains(WorldManager.ZoneId));
            Log.Information("Should be in the race");
        }

        private static  async Task UseItem()
        {
            switch (ChocoboRaceManager.Item.BaseActionId)
            {
                case (uint)ChocoboItems.ChocoPotion:
                    Log.Information($"Using Choco Potion.");
                    break;
                case (uint)ChocoboItems.StaminaTablet:
                    Log.Information($"Using Stamina Tablet.");
                    break;
                case (uint)ChocoboItems.SprintShoes:
                    Log.Information($"Using Sprint Shoes.");
                    break;
                case (uint)ChocoboItems.ChocoAether:
                    Log.Information($"Using Choco Aether.");
                    break;
                case (uint)ChocoboItems.BacchusWater:
                    Log.Information($"Using Bacchus's Water.");
                    break;
                case (uint)ChocoboItems.Graviball:
                    Log.Information($"Using Graviball.");
                    break;
                case (uint)ChocoboItems.BriarCaltrop:
                    Log.Information($"Using Briar Caltrop.");
                    break;
                case (uint)ChocoboItems.HeroTonic:
                    Log.Information($"Using Hero Toonic.");
                    break;
                case (uint)ChocoboItems.ChocoMeteor:
                    Log.Information($"Using Choco Meteor.");
                    break;
                case (uint)ChocoboItems.StaminaSwapper:
                    Log.Information($"Using Stamina Swapper.");
                    break;
                case (uint)ChocoboItems.Spiderweb:
                    Log.Information($"Using Spiderweb.");
                    break;
            }

            ChocoboRaceManager.UseItem();
            await Coroutine.Sleep(1000);
        }

        private static  async Task UseAbility()
        {
            switch (ChocoboRaceManager.Ability.BaseActionId)
            {
                case (uint)ChocoboAbilities.ChocoDashII:
                    Log.Information($"Using Choco Dash II.");
                    break;
            }

            ChocoboRaceManager.UseAbility();
            await Coroutine.Sleep(1000);
        }

        private static  async Task GoFaster()
        {
            switch (ChocoboRaceManager.Status)
            {
                case ChocoboStatus.Normal:
                    if (ChocoboRaceManager.CanUseItem && !RaceChocoboResult.IsOpen)
                    {
                        await UseItem();
                    }

                    if (ChocoboRaceManager.CanUseAbility && !RaceChocoboResult.IsOpen)
                    {
                        await UseAbility();
                    }

                    //Log.Information($"Going faster");
                    MovementManager.Move(MovementDirection.Forward, TimeSpan.FromSeconds(1));
                    await Coroutine.Wait(1000, () => ChocoboRaceManager.Status != ChocoboStatus.Normal || RaceChocoboResult.IsOpen || ChocoboRaceManager.Stamina < 20);
                    break;
                case ChocoboStatus.Lathered:
                    //Log.Information($"Slow it down now.");
                    MovementManager.Move(MovementDirection.Backward, TimeSpan.FromSeconds(1));
                    await Coroutine.Wait(1000, () => ChocoboRaceManager.Status != ChocoboStatus.Lathered || RaceChocoboResult.IsOpen);
                    break;
                case ChocoboStatus.Sprint:
                    //Log.Information($"Holding while sprinting.");
                    await Coroutine.Wait(10000, () => ChocoboRaceManager.Status != ChocoboStatus.Sprint || RaceChocoboResult.IsOpen);
                    break;
            }
        }
    }
}
