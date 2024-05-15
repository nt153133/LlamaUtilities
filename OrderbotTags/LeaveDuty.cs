using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using LlamaLibrary.Structs;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLLeaveDuty")]
    public class LeaveDuty : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("SayGoodbye")]
        [DefaultValue(false)]
        public bool SayGoodbye { get; set; }

        [XmlAttribute("RandomWait")]
        [DefaultValue(false)]
        public bool RandomWait { get; set; }

        [XmlAttribute("PassOnLoot")]
        [DefaultValue(false)]
        public bool PassOnLoot { get; set; }

        [XmlAttribute("VoteMVP")]
        [DefaultValue(false)]
        public bool VoteMVP { get; set; }

        [XmlAttribute("MinWait")]
        [DefaultValue(45000)]
        public int MinWait { get; set; }

        [XmlAttribute("MaxWait")]
        [DefaultValue(90000)]
        public int MaxWait { get; set; }

        [XmlAttribute("SayGoodbyeCustom")]
        [DefaultValue(false)]
        public bool SayGoodbyeCustom { get; set; }

        [XmlAttribute("SayGoodbyeMessages")]
        [DefaultValue("gg/bye")]
        public string SayGoodbyeMessages { get; set; }

        public static ChatBroadcaster PartyBroadcaster = new ChatBroadcaster(MessageType.Party);
        public static ChatBroadcaster EmoteBroadcaster = new ChatBroadcaster(MessageType.StandardEmotes);

        public override bool HighPriority => true;

        private static readonly string[] Farewells = new string[]
        {
            "See you later, alligator!",
            "After a while, crocodile.",
            "Stay out of trouble guys.",
            "I’m out of here baby.",
            "Okay...bye, fry guys!",
            "If I don’t see you around, I'll see you square.",
            "Stay classy. Get it? Classy. *snicker*",
            "Fare thee well.",
            "Catch you on the rebound.",
            "Gotta go, buffalo.",
            "Peace out!",
            "Gotta hit the road. Thanks guys",
            "Long live and prosper!",
            "Live long and prosper!",
            "Well, I'm off!",
            "Smoke me a kipper, I'll be back for breakfast.",
            "Bye-bye, butterfly.",
            "Gotta get going.",
            "It has been emotional, bye.",
            "Out to the door, dinosaur.",
            "Catch you on the flip side.",
            "Gotta bolt!",
            "See you soon, raccoon.",
            "You're still here? It's over. Go home. Go!",
            "It's been fun! Don't get run over on your way out!",
            "It's been fun! Don't let the door hit you on your way out!",
            "Have a good one.",
            "Hasta la vista, baby.",
            "Later, haters.",
            "I gotta bounce.",
            "Toodle-pip!",
            "Catch you later!",
            "See you on the other side.",
            "Have fun storming the castle!",
            "Adieu, cockatoo!",
            "It's been a pleasure and a privilege to meet you guys.",
            "Don't get attacked by a bear, it's nighttime!",
            "Be good and don't get caught.",
            "Thank you for your cooperation. Farewell.",
            "I am the weakest link. Goodbye!",
            "I look forward to our next meeting.",
            "I'm gonna make like a bakery truck and haul buns.",
            "Gotta roll!",
            "Autobots! Roll out!",
            "Oh, and in case I don't see you—good afternoon, good evening, and good night!",
            "Influence everyone in a good way!",
            "Don't forget to come back!",
            "Once more unto the breach, dear friends!",
            "See ya, wouldn't wanna be ya.",
            "Peace out, girl scout!",
            "You guys did awesome, thank you for having me!.",
            "Awesome, couldn't have done it without you guys.",
            "GG",
            "pew",
            "gg thanks",
            "ggs lads",
            "ggty",
            "GG thank you all",
            "gg",
            "ggs",
            "cya",
            "thanks",
            "ezpz",
            "tygg",
            "cheers",
            "Nice and clean, thanks!",
            "/goodbye",
            "gg everyone",
            "gg everyone, thank you",
            "ez",
            "Gg!",
            "Great job all!",
            "ggggggggggggggggggg",
            "RIP",
            "tyfp!",
            "GG everyone, thanks a lot.",
            "Welp, that was a lot of work. Think I need a shower now."
        };

        public override bool IsDone => _isDone;

        private static readonly Random _random = new Random();

        private static readonly ShuffleCircularQueue<string> _farewellQueue = new ShuffleCircularQueue<string>(Farewells);

        private static ShuffleCircularQueue<string> _farewellQueueCustom;

        public LeaveDuty() : base()
        {
        }

        private Composite _passOnLoot;
        private Composite _voteMVP;

        protected override void OnStart()
        {
        }

        protected override void OnDone()
        {
            RemoveHooks();
        }

        private void AddHooks()
        {
        }

        private void RemoveHooks()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => LeaveDutyTask());
        }

        private async Task LeaveDutyTask()
        {
            var rnd = new Random();
            var waitTime = rnd.Next(MinWait, MaxWait);
            ShuffleCircularQueue<string> _farewellQueueCustom = new ShuffleCircularQueue<string>(SayGoodbyeMessages.Split('/'));

            if (SayGoodbye && !SayGoodbyeCustom)
            {
                var sentfarewell = _farewellQueue.Dequeue();

                Log.Information($"Saying '{sentfarewell}' the group");
                await PartyBroadcaster.Send(sentfarewell);
            }

            if (SayGoodbye && SayGoodbyeCustom)
            {
                var sentcustomgreeting = _farewellQueueCustom.Dequeue();
                Log.Information($"Saying '{sentcustomgreeting}' the group");
                await PartyBroadcaster.Send(sentcustomgreeting);
            }

            JournalAccept.Decline();

            if (VoteMVP)
            {
                Log.Information($"Waiting to vote on MVP.");
                await Coroutine.Wait(5000, () => AgentVoteMVP.Instance.CanToggle || VoteMvp.Instance.IsOpen);
                if (AgentVoteMVP.Instance.CanToggle || VoteMvp.Instance.IsOpen)
                {
                    Log.Information($"MVP window open, voting on MVP");
                    await LlamaLibrary.Helpers.GeneralFunctions.VoteMVPTask();
                }
                else
                {
                    Log.Information($"MVP window did not open.");
                }
            }

            if (PassOnLoot)
            {
                if (GameObjectManager.GetObjectsOfType<Treasure>().Where(i => i.State == 0).Any())
                {
                    Log.Information($"There are chests that need to be opened.");
                    await LlamaLibrary.Helpers.GeneralFunctions.OpenChests();
                }
                Log.Information($"Waiting for loot window.");
                await Coroutine.Wait(5000, () => LlamaLibrary.RemoteWindows.NotificationLoot.Instance.IsOpen || NeedGreed.Instance.IsOpen);
                if (LlamaLibrary.RemoteWindows.NotificationLoot.Instance.IsOpen || NeedGreed.Instance.IsOpen)
                {
                    Log.Information($"Loot window open, passing on loot");
                    await LlamaLibrary.Helpers.GeneralFunctions.PassOnAllLoot();
                }
                else
                {
                    Log.Information($"Loot window did not open.");
                }
            }

            if (RandomWait)
            {
                Log.Information($"Leaving dungeon in {waitTime / 1000} seconds or when party has disbanded.");
                await Coroutine.Wait(waitTime, () => !PartyManager.IsInParty);
            }

            while (DutyManager.InInstance)
            {
                Log.Information($"Leaving Instance...");
                ff14bot.Managers.DutyManager.LeaveActiveDuty();
                await Coroutine.Wait(20000, () => CommonBehaviors.IsLoading);
                if (CommonBehaviors.IsLoading)
                {
                    await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);
                }
            }

            _isDone = true;
        }
    }
}