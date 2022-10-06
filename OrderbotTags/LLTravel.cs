//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

using System.ComponentModel;
using System.Threading.Tasks;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot.Behavior;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LLTravel")]
    public class LLTravel : LLProfileBehavior
    {
        [XmlAttribute("XYZ")]
        public Vector3 XYZ { get; set; }

        [XmlAttribute("ZoneId")]
        public int ZoneId { get; set; }

        [XmlAttribute("Land")]
        [DefaultValue(true)]
        public bool Land { get; set; }

        public override bool HighPriority => true;

        private bool _isDone;

        public override bool IsDone => _isDone;

        public LLTravel() : base()
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
            return new ActionRunCoroutine(r => LLTravelTask());
        }

        private async Task LLTravelTask()
        {
            await LlamaLibrary.Helpers.Navigation.FlyToWithZone((uint)ZoneId, XYZ);

            if (Land)
            {
                await CommonTasks.Land();
            }

            _isDone = true;
        }
    }
}