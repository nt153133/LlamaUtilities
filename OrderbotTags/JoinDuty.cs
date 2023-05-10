using System;
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
using ff14bot.Pathing.Service_Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.Enums;
using LlamaLibrary.Helpers;
using LlamaLibrary.Structs;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLJoinDuty")]
    [XmlElement("JoinDuty")]
    public class LLJoinDuty : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("GoToBarracks")]
        [DefaultValue(false)]
        public bool GoToBarracks { get; set; }

        [XmlAttribute("DutyId")]
        public int DutyId { get; set; }

        [XmlAttribute("Trial")]
        [DefaultValue(false)]
        public bool Trial { get; set; }

        [XmlAttribute("Raid")]
        [DefaultValue(false)]
        public bool Raid { get; set; }

        [XmlAttribute("Guildhest")]
        [DefaultValue(false)]
        public bool Guildhest { get; set; }

        [XmlAttribute("Undersized")]
        [DefaultValue(true)]
        public bool Undersized { get; set; }

        [XmlAttribute("SayHello")]
        [DefaultValue(false)]
        public bool SayHello { get; set; }

        [XmlAttribute("SayHelloCustom")]
        [DefaultValue(false)]
        public bool SayHelloCustom { get; set; }

        [XmlAttribute("SayHelloMessages")]
        [DefaultValue("gg/bye")]
        public string SayHelloMessages { get; set; }

        public static ChatBroadcaster PartyBroadcaster = new ChatBroadcaster(MessageType.Party);
        public static ChatBroadcaster EmoteBroadcaster = new ChatBroadcaster(MessageType.StandardEmotes);
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

        private static readonly ShuffleCircularQueue<string> _greetingQueue = new ShuffleCircularQueue<string>(Greetings);

        private static ShuffleCircularQueue<string> _greetingQueueCustom;

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
            return new ActionRunCoroutine(r => JoinDutyTask());
        }

        private async Task JoinDutyTask()
        {
            await GeneralFunctions.StopBusy(false);

            if (GoToBarracks && (WorldManager.ZoneId != 534 && WorldManager.ZoneId != 535 && WorldManager.ZoneId != 536))
            {
                await GoToBarracksTask();
            }

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

            ShuffleCircularQueue<string> _greetingQueueCustom = new ShuffleCircularQueue<string>(SayHelloMessages.Split('/'));

            if (DirectorManager.ActiveDirector is ff14bot.Directors.InstanceContentDirector director)
            {
                var time = new TimeSpan(1, 29, 59);
                if (Raid)
                {
                    time = new TimeSpan(1, 59, 59);
                }

                if (Trial)
                {
                    time = new TimeSpan(0, 59, 59);
                }

                if (Guildhest)
                {
                    time = new TimeSpan(0, 29, 59);
                }

                if (director.TimeLeftInDungeon >= time.Add(new TimeSpan(0,0,1)))
                {
                    Log.Information("Barrier up");
                    if (SayHello && !SayHelloCustom)
                    {
                        var sentgreeting = _greetingQueue.Dequeue();
                        Log.Information($"Saying '{sentgreeting}' the group");
                        await PartyBroadcaster.Send(sentgreeting);
                    }

                    if (SayHelloCustom && !SayHello)
                    {
                        var sentcustomgreeting = _greetingQueueCustom.Dequeue();
                        Log.Information($"Saying '{sentcustomgreeting}' the group");
                        await PartyBroadcaster.Send(sentcustomgreeting);
                    }

                    await Coroutine.Wait(-1, () => director.TimeLeftInDungeon < time);
                }
            }
            else
            {
                Log.Error("Director is null");
            }

            Log.Information("Should be ready");

            _isDone = true;
        }

        public async Task GoToBarracksTask()
        {
            Log.Information($"Traveling to barracks");
            if (Navigator.NavigationProvider == null)
            {
                Navigator.PlayerMover = new SlideMover();
                Navigator.NavigationProvider = new ServiceNavigationProvider();
            }

            uint[] entranceIds = { 2007527, 2007529, 2006962 };
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
                Log.Information($"Selecting Yes.");
                ff14bot.RemoteWindows.SelectYesno.ClickYes();
            }

            await Coroutine.Wait(5000, () => CommonBehaviors.IsLoading);
            while (CommonBehaviors.IsLoading)
            {
                Log.Information($"Waiting for zoning to finish...");
                await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);
            }
        }
    }
}