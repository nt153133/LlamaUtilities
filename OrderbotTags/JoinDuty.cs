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
using LlamaLibrary.Enums;
using LlamaLibrary.Helpers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLJoinDuty")]
    [XmlElement("JoinDuty")]
    public class LLJoinDuty : LLProfileBehavior
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

        [XmlAttribute("Undersized")]
        [DefaultValue(true)]
        public bool Undersized { get; set; }

        [XmlAttribute("SayHello")]
        [DefaultValue(false)]
        public bool SayHello { get; set; }

        public static ChatBroadcaster PartyBroadcaster = new ChatBroadcaster(MessageType.Party);
        public static ChatBroadcaster EmoteBroadcaster = new ChatBroadcaster(MessageType.StandardEmotes);

        private static readonly Random _random = new Random();

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        private static readonly string[] Greetings = new string[]
        {
            "Hola",
            "Bonjour",
            "Hallo",
            "Ciao",
            "Konnichiwa",
            "What’s kicking, little chicken?",
            "Hello, governor!",
            "Whaddup bro?",
            "Bonjour monsieur!",
            "Ciao babydoll!",
            "Bing bing! How’s it going?",
            "Good day guys",
            "Oooo la la. This guy again",
            "Welcome to the club guys",
            "What’s sizzling?",
            "Whazzup?",
            "Ni hao ma?",
            "What’s up, buttercup?",
            "Hello!",
            "Hey",
            "Heyo",
            "Hihi",
            "Hello new friends!",
            "Hi new friends",
            "Heya",
            "Ello! o/",
            "hello!",
            "Hi, I just met you, and yes, this is crazy. Here’s my number – can we kill this guy, maybe?",
            "Hi guys",
            "What’s smokin’?",
            "How is life sailing?",
            "Hiya",
            "Hi",
            "Hey friends!",
            "Yo",
            "I come in peace. Okay, yeah maybe not.",
            "Hello, my name is Inigo Montoya.",
            "I'm Batman",
            "‘Ello, mates",
            "How you doin'?",
            "What's cookin', good lookin'?",
            "Aloha",
            "Hey you, yeah you. I like your face.",
            "Why, hello there!",
            "This fight may be recorded for training purposes.",
            "GOOOOOD MORNING, VIETNAM!",
            "‘Sup, homeslice?",
            "What’s crackin’?",
            "Here's Johnny!",
            "Whaddup",
            "o/",
            "o7",
            "Greetings and salutations!",
            "Top of the mornin’ to ya!",
            "Howdy partners.",
            "Ahoy there, matey.",
            "Anyone else have chicken too?",
            "Hey guys, glad to be here. Let's go have some fun.",
            "Oh yeah, love fighting this guy"
        };

        public LLJoinDuty() : base()
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
            return new ActionRunCoroutine(r => JoinDutyTask(DutyId, Undersized, Trial, Raid));
        }

        private async Task JoinDutyTask(int DutyId, bool Undersized, bool Trial, bool Raid)
        {

            await GeneralFunctions.StopBusy();

            if (Undersized)
            {
                Log.Information("Joining Duty as Undersized party.");
                GameSettingsManager.JoinWithUndersizedParty = true;
            }
            else
            {
                Log.Information("Joining Duty as normal group.");
                GameSettingsManager.JoinWithUndersizedParty = false;
            }

            if (!PartyManager.IsInParty || (PartyManager.IsInParty && PartyManager.IsPartyLeader))
            {
                while (DutyManager.QueueState == QueueState.None)
                {


                    Log.Information("Queuing for " + DataManager.InstanceContentResults[(uint)DutyId].CurrentLocaleName);
                    DutyManager.Queue(DataManager.InstanceContentResults[(uint)DutyId]);
                    await Coroutine.Wait(10000, () => DutyManager.QueueState == QueueState.CommenceAvailable || DutyManager.QueueState == QueueState.JoiningInstance);

                    if (DutyManager.QueueState != QueueState.None)
                    {
                        Log.Information("Queued for Dungeon");
                    }
                    else if (DutyManager.QueueState == QueueState.None)
                    {
                        Log.Error("Something went wrong, queuing again...");
                    }
                }
            }
            else
            {
                Log.Information("Waiting for dungeon queue.");
                await Coroutine.Wait(-1, () => DutyManager.QueueState == QueueState.CommenceAvailable || DutyManager.QueueState == QueueState.JoiningInstance);
                Log.Information("Queued for Dungeon");
            }

            while (DutyManager.QueueState != QueueState.None || DutyManager.QueueState != QueueState.InDungeon || CommonBehaviors.IsLoading)
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

                    Log.Information($"Dungeon popped, commencing in {waitTime / 1000} seconds.");
                    await Coroutine.Sleep(waitTime);
                    DutyManager.Commence();
                    await Coroutine.Wait(-1,
                                         () => DutyManager.QueueState == QueueState.LoadingContent ||
                                                DutyManager.QueueState == QueueState.CommenceAvailable);
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

            Log.Information("Should be in duty");

            if (DirectorManager.ActiveDirector is ff14bot.Directors.InstanceContentDirector director)
            {
                if (Trial)
                {
                    if (director.TimeLeftInDungeon >= new TimeSpan(0, 60, 0))
                    {
                        Log.Information("Barrier up");
                        if (SayHello)
                        {
                            var sentgreeting = Greetings[_random.Next(0, Greetings.Length)];

                            Log.Information($"Saying '{sentgreeting}' the group");
                            await PartyBroadcaster.Send(sentgreeting);

                        }

                        await Coroutine.Wait(-1, () => director.TimeLeftInDungeon < new TimeSpan(0, 59, 59));
                    }
                }

                if (Raid)
                {
                    if (director.TimeLeftInDungeon >= new TimeSpan(2, 0, 0))
                    {
                        Log.Information("Barrier up");
                        await Coroutine.Wait(-1, () => director.TimeLeftInDungeon < new TimeSpan(1, 59, 59));
                    }
                }
                else
                {
                    if (director.TimeLeftInDungeon >= new TimeSpan(1, 30, 0))
                    {
                        Log.Information("Barrier up");
                        await Coroutine.Wait(-1, () => director.TimeLeftInDungeon < new TimeSpan(1, 29, 59));
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