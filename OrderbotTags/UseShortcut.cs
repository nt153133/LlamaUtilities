using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("UseShortcut")]
    public class UseShortcut : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("NpcId")]
        [XmlAttribute("NpcID")]
        public int ShortcutId { get; set; }

        [XmlAttribute("Distance")]
        [XmlAttribute("distance")]
        [DefaultValue(10)]
        public float Distance { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public UseShortcut() : base()
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
            return new ActionRunCoroutine(r => UseShortcutTask());
        }

        private async Task UseShortcutTask()
        {
            uint[] npcIds = { (uint)ShortcutId };
            var shortcutNpc = GameObjectManager.GameObjects.Where(r => r.IsTargetable && r.InLineOfSight() && Core.Me.Location.Distance2D(r.Location) <= Distance && npcIds.Contains(r.NpcId)).OrderBy(r => r.Distance()).FirstOrDefault();
            while (Core.Me.Location.Distance2D(shortcutNpc.Location) > 1.5f)
            {
                await Coroutine.Yield();
                Navigator.PlayerMover.MoveTowards(shortcutNpc.Location);
            }

            Navigator.PlayerMover.MoveStop();
            var location = Core.Me.Location;
            shortcutNpc.Interact();

            if (!await Coroutine.Wait(10000, () => SelectYesno.IsOpen || CommonBehaviors.IsLoading || !Core.Me.Location.Equals(location) || QuestLogManager.InCutscene))
            {
                Log.Error("Nothing happened");
                _isDone = true;
                return;
            }

            if (SelectYesno.IsOpen)
            {
                ff14bot.RemoteWindows.SelectYesno.ClickYes();
                await Coroutine.Wait(10000, () => !SelectYesno.IsOpen);
            }

            if (!await Coroutine.Wait(10000, () => CommonBehaviors.IsLoading || !Core.Me.Location.Equals(location) || QuestLogManager.InCutscene))
            {
                Log.Information("Timed out waiting for loading screen or position change");
                Log.Information($"IsLoading: {CommonBehaviors.IsLoading}");
                Log.Information($"Position: {Core.Me.Location}");
                Log.Information($"Last Position: {location}");
                Log.Information($"InCutscene: {QuestLogManager.InCutscene}");
                _isDone = true;
                return;
            }

            while (CommonBehaviors.IsLoading)
            {
                await Coroutine.Wait(20000, () => !CommonBehaviors.IsLoading);
                await Coroutine.Sleep(500);
            }

            while (QuestLogManager.InCutscene)
            {
                Log.Information("Waiting for cutscene to end");
                await Coroutine.Wait(20000, () => !QuestLogManager.InCutscene);
            }

            _isDone = true;
        }
    }
}