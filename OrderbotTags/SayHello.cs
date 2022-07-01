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
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("SayHello")]
    public class SayHello : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Goodbye")]
        [DefaultValue(false)]
        public bool Goodbye { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        private static readonly Random _random = new Random();

        private static string[] Greetings = new string[]
        {
            "/p Hola",
            "/p Bonjour",
            "/p Hallo",
            "/p Ciao",
            "/p Konnichiwa",
            "/p What’s kicking, little chicken?",
            "/p Hello, governor!",
            "/p Whaddup bro?",
            "/p Bonjour monsieur!",
            "/p Ciao babydoll!",
            "/p Bing bing! How’s it going?",
            "/p Good day guys",
            "/p Oooo la la. This guy again",
            "/p Welcome to the club guys",
            "/p What’s sizzling?",
            "/p Whazzup?",
            "/p Ni hao ma?",
            "/p What’s up, buttercup?",
            "/p Hello!",
            "/p Hey",
            "/p Hi, I just met you, and yes, this is crazy. Here’s my number – can we kill this guy, maybe?",
            "/p Hi guys",
            "/p What’s smokin’?",
            "/p How is life sailing?",
            "/p Hiya",
            "/p Hi",
            "/p Hey friends!",
            "/p Yo",
            "/p I come in peace. Okay, yeah maybe not.",
            "/p Hello, my name is Inigo Montoya.",
            "/p I'm Batman",
            "/p ‘Ello, mates",
            "/p How you doin'?",
            "/p What's cookin', good lookin'?",
            "/p Aloha",
            "/p Hey you, yeah you. I like your face.",
            "/p Why, hello there!",
            "/p This fight may be recorded for training purposes.",
            "/p GOOOOOD MORNING, VIETNAM!",
            "/p ‘Sup, homeslice?",
            "/p What’s crackin’?",
            "/p Here's Johnny!",
            "/p Whaddup",
            "/p Greetings and salutations!",
            "/p Top of the mornin’ to ya!",
            "/p Howdy partners.",
            "/p Ahoy there, matey.",
            "/p Anyone else have chicken too?",
            "/p Hey guys, glad to be here. Let's go have some fun.",
            "/p Oh yeah, love fighting this guy"
        };

        private static string[] Farewells = new string[]
        {
            "/p See you later, alligator!",
            "/p After a while, crocodile.",
            "/p Stay out of trouble guys.",
            "/p I’m out of here baby.",
            "/p Okay...bye, fry guys!",
            "/p If I don’t see you around, I'll see you square.",
            "/p Stay classy. Get it? Classy. *snicker*",
            "/p Fare thee well.",
            "/p Catch you on the rebound.",
            "/p Gotta go, buffalo.",
            "/p Peace out!",
            "/p Gotta hit the road. Thanks guys",
            "/p Long live and prosper!",
            "/p Live long and prosper!",
            "/p Well, I'm off!",
            "/p Smoke me a kipper, I'll be back for breakfast.",
            "/p Bye-bye, butterfly.",
            "/p Gotta get going.",
            "/p It has been emotional, bye.",
            "/p Out to the door, dinosaur.",
            "/p Catch you on the flip side.",
            "/p Gotta bolt!",
            "/p See you soon, raccoon.",
            "/p You're still here? It's over. Go home. Go!",
            "/p It's been fun! Don't get run over on your way out!",
            "/p It's been fun! Don't let the door hit you on your way out!",
            "/p Have a good one.",
            "/p It was nice to see you again.",
            "/p Hasta la vista, baby.",
            "/p Later, haters.",
            "/p I gotta bounce.",
            "/p Toodle-pip!",
            "/p Catch you later!",
            "/p See you on the other side.",
            "/p Have fun storming the castle!",
            "/p Adieu, cockatoo!",
            "/p It's been a pleasure and a privilege to meet you guys.",
            "/p Don't get attacked by a bear, it's nighttime!",
            "/p Be good and don't get caught.",
            "/p Thank you for your cooperation. Farewell.",
            "/p I am the weakest link. Goodbye!",
            "/p I look forward to our next meeting.",
            "/p I'm gonna make like a bakery truck and haul buns.",
            "/p Gotta roll!",
            "/p Autobots! Roll out!",
            "/p Oh, and in case I don't see you—good afternoon, good evening, and good night!",
            "/p Influence everyone in a good way!",
            "/p Don't forget to come back!",
            "/p Once more unto the breach, dear friends!",
            "/p See ya, wouldn't wanna be ya.",
            "/p Peace out, girl scout!",
            "/p You guys did awesome, thank you for having me!.",
            "/p Awesome, couldn't have done it without you guys.",
            "/p gg",
            "/p RIP",
            "/p tyfp!",
            "/p GG everyone, thanks a lot.",
            "/p Welp, that was a lot of work. Think I need a shower now."
        };


        public SayHello() : base()
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
            return new ActionRunCoroutine(r => SayHelloTask());
        }

        private async Task SayHelloTask()
        {

            if (Goodbye)
            {
                Log("Saying goodbye the group");
                ChatManager.SendChat(Farewells[_random.Next(0, Farewells.Length)]);
            }
            else
            {
                Log("Greeting the group");
                ChatManager.SendChat(Greetings[_random.Next(0, Greetings.Length)]);
            }

            _isDone = true;

        }
    }
}