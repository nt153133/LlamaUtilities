using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Objects;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("VoteMVP")]
    public class VoteMVP : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Names")]
        [XmlAttribute("names")]
        [XmlAttribute("Name")]
        [XmlAttribute("name")]
        public string[] PlayerNames { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public VoteMVP() : base()
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

            if (PlayerNames != null)
            {
                return new ActionRunCoroutine(r => VotePerson(PlayerNames));
            }
            else
            {
                return new ActionRunCoroutine(r => VoteAnyone());
            }
        }

        private async Task VoteAnyone()
        {
            if (await Coroutine.Wait(60000, () => AgentVoteMVP.Instance.CanToggle || VoteMvp.Instance.IsOpen))
            {
                await AgentVoteMVP.Instance.OpenAndVote();
            }

            _isDone = true;
        }

        private async Task VotePerson(string[] names)
        {
            if (await Coroutine.Wait(60000, () => AgentVoteMVP.Instance.CanToggle || VoteMvp.Instance.IsOpen))
            {
                await AgentVoteMVP.Instance.HandleMvpVote(names);
            }
            _isDone = true;
        }

    }
}