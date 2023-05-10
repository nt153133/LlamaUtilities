using System;
using System.ComponentModel;
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
    [XmlElement("LeaveGuildhest")]
    public class LeaveGuildhest : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("SayGoodbye")]
        [DefaultValue(false)]
        public bool SayGoodbye { get; set; }

        [XmlAttribute("RandomWait")]
        [DefaultValue(false)]
        public bool RandomWait { get; set; }

        [XmlAttribute("VoteMVP")]
        [DefaultValue(false)]
        public bool VoteMVP { get; set; }

        [XmlAttribute("NpcId")]
        [XmlAttribute("NPCID")]
        [XmlAttribute("NpcID")]
        [DefaultValue(0)]
        public int NpcId { get; set; }

        [XmlAttribute("MinWait")]
        [DefaultValue(45000)]
        public int MinWait { get; set; }

        [XmlAttribute("MaxWait")]
        [DefaultValue(90000)]
        public int MaxWait { get; set; }

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

        public LeaveGuildhest() : base()
        {
        }

        private Composite _passOnLoot;
        private Composite _voteMVP;

        protected override void OnStart()
        {
        }

        private async Task<bool> VoteMVPTask()
        {
            Log.Information("Voting on MVP");

            var name = await AgentVoteMVP.Instance.OpenAndVoteName();

            Log.Information($"Voted for {name}");

            return false;
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

            if (SayGoodbye)
            {
                var sentfarewell = _farewellQueue.Dequeue();

                Log.Information($"Saying '{sentfarewell}' the group");
                await PartyBroadcaster.Send(sentfarewell);
            }

            if (VoteMVP)
            {
                Log.Information($"Waiting to vote on MVP.");
                await Coroutine.Wait(10000, () => AgentVoteMVP.Instance.CanToggle || VoteMvp.Instance.IsOpen);
                await VoteMVPTask();
            }

            if (RandomWait)
            {
                Log.Information($"Leaving Guildhest in {waitTime / 1000} seconds or when party has disbanded.");
                await Coroutine.Wait(waitTime, () => !PartyManager.IsInParty);
            }

            while (DutyManager.InInstance)
            {
                Log.Information($"Leaving Instance...");
                var exitNPC = GameObjectManager.GetObjectByNPCId((uint)NpcId);

                if (exitNPC == null)
                {
                    Log.Information($"Couldn't find {exitNPC.Name}, exiting'.");
                    return;
                }

                if (!exitNPC.IsWithinInteractRange)

                {
                    var _target = exitNPC.Location;
                    Navigator.PlayerMover.MoveTowards(_target);
                    while (_target.Distance2D(Core.Me.Location) >= 4)
                    {
                        Navigator.PlayerMover.MoveTowards(_target);
                        await Coroutine.Sleep(100);
                    }

                    Navigator.PlayerMover.MoveStop();
                }

                exitNPC.Interact();

                await Coroutine.Wait(10000, () => Talk.DialogOpen);

                if (!Talk.DialogOpen)
                {
                    Log.Information($"Interacting with {exitNPC.Name} didn't work, trying again.");
                    exitNPC.Interact();
                    await Coroutine.Wait(10000, () => Talk.DialogOpen);
                }

                while (!JournalResult.IsOpen)
                {
                    Talk.Next();
                    await Coroutine.Yield();
                    await Coroutine.Sleep(500);
                }

                if (JournalResult.IsOpen)
                {
                    JournalResult.Complete();
                    Log.Information($"Waiting for exit.");
                    await Coroutine.Wait(-1, () => CommonBehaviors.IsLoading);
                    await Coroutine.Wait(20000, () => !CommonBehaviors.IsLoading);
                    await Coroutine.Sleep(500);
                }
            }

            _isDone = true;
        }
    }
}