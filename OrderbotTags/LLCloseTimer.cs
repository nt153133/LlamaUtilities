using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Behavior;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>
    /// Confirms the SelectYesno prompt that can appear when a timed activity ends,
    /// then waits for any resulting zone transition to finish.
    /// </summary>
    [XmlElement("LLCloseTimer")]
    public class LLCloseTimer : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Timeout")]
        public int Timeout { get; set; } = 10000;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

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
            return new ActionRunCoroutine(_ => CloseTimer());
        }

        private async Task CloseTimer()
        {
            await Coroutine.Wait(Timeout, () => SelectYesno.IsOpen);
            if (SelectYesno.IsOpen)
            {
                SelectYesno.ClickYes();
                await Coroutine.Wait(Timeout, () => CommonBehaviors.IsLoading);
                await Coroutine.Wait(Timeout, () => !CommonBehaviors.IsLoading);
            }

            _isDone = true;
        }
    }
}
