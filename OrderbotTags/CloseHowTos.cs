using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using LlamaLibrary.Logging;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("CloseHowTos")]
    public class CloseHowTos : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public CloseHowTos() : base() { }

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
					if (RaptureAtkUnitManager.GetWindowByName("HowToNotice") != null)
					{
							do
							{
									var windowbyname = RaptureAtkUnitManager.GetWindowByName("HowToNotice");

									if (windowbyname != null)
									{
											windowbyname.SendAction(1, 3, 0);
									}

									await Coroutine.Wait(5000, () => RaptureAtkUnitManager.GetWindowByName("HowTo") != null);

									windowbyname = RaptureAtkUnitManager.GetWindowByName("HowTo");

									if (windowbyname != null)
									{
											windowbyname.SendAction(1, 3uL, 0xFFFFFFFFuL);
									}

									await Coroutine.Sleep(500);
							} while (RaptureAtkUnitManager.GetWindowByName("HowToNotice") != null);
					}

					if (RaptureAtkUnitManager.GetWindowByName("HowTo") != null)
					{
							RaptureAtkUnitManager.GetWindowByName("HowTo").SendAction(1, 3uL, 0xFFFFFFFFuL);
					}
					
					if (RaptureAtkUnitManager.GetWindowByName("PlayGuide") != null)
					{
							RaptureAtkUnitManager.GetWindowByName("PlayGuide").SendAction(1, 3uL, 0xFFFFFFFFuL);
					}

          _isDone = true;
        }
    }
}