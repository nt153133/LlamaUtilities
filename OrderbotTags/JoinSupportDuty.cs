using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("JoinSupportDuty")]
    public class JoinSupportDuty : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("DutyId")]
        public int DutyId { get; set; }

        [XmlAttribute("Trial")]
        [DefaultValue(false)]
        public bool Trial { get; set; }

        [XmlAttribute("Raid")]
        [DefaultValue(false)]
        public bool Raid { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public JoinSupportDuty() : base()
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
            return new ActionRunCoroutine(r => JoinDutyTask(DutyId, Trial, Raid));
        }

        private async Task JoinDutyTask(int DutyId, bool Trial, bool Raid)
        {
            await GeneralFunctions.StopBusy();

            while (DutyManager.QueueState == QueueState.None)
            {
                Log.Information("Queuing for " + DataManager.InstanceContentResults[(uint)DutyId].CurrentLocaleName);
                if (!DawnStory.Instance.IsOpen)
                {
                    AgentDawnStory.Instance.Toggle();
                }

                if (await Coroutine.Wait(8000, () => DawnStory.Instance.IsOpen))
                {
                    if (await DawnStory.Instance.SelectDuty(DutyId))
                    {
                        DawnStory.Instance.Commence();
                    }
                }

                await Coroutine.Wait(10000, () => DutyManager.QueueState == QueueState.CommenceAvailable || DutyManager.QueueState == QueueState.JoiningInstance);
                if (DutyManager.QueueState != QueueState.None)
                {
                    Log.Information("Queued for Dungeon");
                }
                else if (DutyManager.QueueState == QueueState.None)
                {
                    Log.Error("Something went wrong, queueing again...");
                }
            }

            while (DutyManager.QueueState != QueueState.None || DutyManager.QueueState != QueueState.InDungeon || CommonBehaviors.IsLoading)
            {
                if (DutyManager.QueueState == QueueState.CommenceAvailable)
                {
                    Log.Information("Waiting for queue pop.");
                    await Coroutine.Wait(-1, () => DutyManager.QueueState == QueueState.JoiningInstance || DutyManager.QueueState == QueueState.None);
                }

                if (DutyManager.QueueState == QueueState.JoiningInstance)
                {
                    var rnd = new Random();
                    var waitTime = rnd.Next(1000, 10000);

                    Log.Information($"Dungeon popped, commencing in {waitTime / 1000} seconds.");
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
                    await Coroutine.Wait(2000, () => Conversation.IsOpen || SelectYesno.IsOpen);
                    if (Conversation.IsOpen)
                    {
                        Conversation.SelectLine(0);
                    }

                    if (SelectYesno.IsOpen)
                    {
                        SelectYesno.Yes();
                    }
                }
            }

            Log.Information("Should be in duty");

            var director = (ff14bot.Directors.InstanceContentDirector)DirectorManager.ActiveDirector;
            if (director != null)
            {
                if (Trial)
                {
                    if (director.TimeLeftInDungeon >= new TimeSpan(0, 60, 0))
                    {
                        Log.Information("Barrier up");
                        await Coroutine.Wait(-1, () => director.TimeLeftInDungeon < new TimeSpan(0, 59, 58));
                    }
                }

                if (Raid)
                {
                    if (director.TimeLeftInDungeon >= new TimeSpan(2, 0, 0))
                    {
                        Log.Information("Barrier up");
                        await Coroutine.Wait(-1, () => director.TimeLeftInDungeon < new TimeSpan(1, 59, 58));
                    }
                }
                else
                {
                    if (director.TimeLeftInDungeon >= new TimeSpan(1, 30, 0))
                    {
                        Log.Information("Barrier up");
                        await Coroutine.Wait(-1, () => director.TimeLeftInDungeon < new TimeSpan(1, 29, 58));
                    }
                }
            }
            else
            {
                Log.Error("Director is null");
            }

            Log.Information("Should be ready");

            _isDone = true;
        }
    }
}