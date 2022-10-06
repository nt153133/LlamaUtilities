using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
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
            var shortcutNpc = GameObjectManager.GameObjects.Where(r => r.IsTargetable && Core.Me.Location.Distance2D(r.Location) <= Distance && npcIds.Contains(r.NpcId)).OrderBy(r => r.Distance()).FirstOrDefault();
            while (Core.Me.Location.Distance2D(shortcutNpc.Location) > 1.5f)
            {
                await Coroutine.Yield();
                Navigator.PlayerMover.MoveTowards(shortcutNpc.Location);
            }
            Navigator.PlayerMover.MoveStop();
            shortcutNpc.Interact();
            await Coroutine.Wait(10000, () => SelectYesno.IsOpen);
            ff14bot.RemoteWindows.SelectYesno.ClickYes();
            await Coroutine.Wait(10000, () => CommonBehaviors.IsLoading);
            while (CommonBehaviors.IsLoading)
            {
                await Coroutine.Wait(20000, () => !CommonBehaviors.IsLoading);
                await Coroutine.Sleep(500);
            }

            _isDone = true;
        }
    }
}