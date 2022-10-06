using System;
using System.Linq;
using System.Threading.Tasks;
using Clio.Utilities;
using Clio.XmlEngine;
using LlamaLibrary.Helpers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("RandomMoveTo")]
    public class RandomMoveTo : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("XYZ1")]
        public Vector3 XYZ1 { get; set; }

        [XmlAttribute("XYZ2")]
        public Vector3 XYZ2 { get; set; }

        [XmlAttribute("XYZ3")]
        public Vector3 XYZ3 { get; set; }

        private static Vector3[] Locations;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        private static readonly Random _random = new Random();

        public RandomMoveTo() : base()
        {
        }

        protected override void OnStart()
        {
            var list = new Vector3[]
            {
                XYZ1,
                XYZ2,
                XYZ3
            };

            Locations = list.Where(i => !i.Equals(Vector3.Zero)).ToArray();
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
            return new ActionRunCoroutine(r => RandomMoveToTask());
        }

        private async Task RandomMoveToTask()
        {
            //ChatManager.SendChat(Farewells[_random.Next(0, Farewells.Length)]);
            await Navigation.FlightorMove(Locations[_random.Next(0, Locations.Length)]);

            _isDone = true;
        }
    }

}