using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("CloseHowTos")]
    public class CloseHowTos : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        private static readonly string[] TutorialWindows =
        {
            "HowToNotice",
            "HowTo",
            "PlayGuide",
            "JobHudNotice",
            "ContentsTutorial"
        };

        public CloseHowTos() : base()
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
            return new ActionRunCoroutine(r => CloseWindows());
        }

        private async Task CloseWindows()
        {
            bool hasTutorialWindow = TutorialWindows
                .Any(window => RaptureAtkUnitManager.GetWindowByName(window) != null);

            if (hasTutorialWindow)
            {
                await LlamaLibrary.Helpers.GeneralFunctions.CloseHowTos();
            }

            if (JournalAccept.IsOpen)
            {
                JournalAccept.Decline();
            }

            _isDone = true;
        }
    }
}