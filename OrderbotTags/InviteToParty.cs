using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using LlamaLibrary.Logging;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("InviteToParty")]
    public class InviteToParty : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Name")]
        [XmlAttribute("name")]
        public string PlayerName { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public InviteToParty() : base() { }

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
            return new ActionRunCoroutine(r => InvitePerson(PlayerName));
        }

        private async Task InvitePerson(string name)
        {
            var player = GameObjectManager.GetObjectsOfType<BattleCharacter>().Where(p =>
                                                                                         p.Name.Contains(name)).ToList();

            if (player.Any())
            {
                player.First().Target();
                Logging.WriteDiagnostic($"Inviting {player.First()}");
                ChatManager.SendChat("/invite <t>");
            }
            else
            {
                Logging.WriteDiagnostic($"Didn't find player.");
            }

            _isDone = true;
        }
    }
}