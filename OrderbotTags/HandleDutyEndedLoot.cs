namespace LlamaUtilities.OrderbotTags
{
    using System.Threading.Tasks;
    using System.Windows.Media;
    using Clio.XmlEngine;
    using LlamaLibrary.Helpers;
    using TreeSharp;

    [XmlElement("HandleDutyEndedLoot")]
    public class HandleDutyEndedLoot : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        protected override Color LogColor => Colors.Chartreuse;

        public HandleDutyEndedLoot() : base()
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
            return new ActionRunCoroutine(r => HandleDutyEndedLootTask());
        }

        private async Task HandleDutyEndedLootTask()
        {
            if (!PlatypusHelper.HasPlatypus)
            {
                Log.Warning("HandleDutyEndedLootTask called but Platypus is not installed so cannot proceed.");

                _isDone = true;
                return;
            }

            Log.Information("Duty ended, handling loot via Platypus.");

            await PlatypusHelper.QolOpenTreasureCoffersInDuty();

            await PlatypusHelper.QolWaitUntilAllLootIsGone();

            _isDone = true;
        }
    }
}