using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using LlamaLibrary.Helpers;
using TreeSharp;
using Action = TreeSharp.Action;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("RandomMoveTo")]
    public class RandomMoveTo : LLProfileBehavior
    {
        private bool _done;

        [XmlAttribute("XYZ1")]
        public Vector3 XYZ1 { get; set; }

        [XmlAttribute("XYZ2")]
        public Vector3 XYZ2 { get; set; }

        [XmlAttribute("XYZ3")]
        public Vector3 XYZ3 { get; set; }

        private static Vector3[] Locations;

        private static readonly Random _random = new Random();

        [XmlAttribute("Name")]
        public string Name { get; set; }


        [DefaultValue(3)]
        [XmlAttribute("Distance")]
        public float Distance { get; set; }

        [DefaultValue(true)]
        [XmlAttribute("UseMesh")]
        public bool UseMesh { get; set; }

        private static Vector3 location;
        private ushort startmap;
        protected override void OnStart()
        {
            var list = new Vector3[]
            {
                XYZ1,
                XYZ2,
                XYZ3
            };

            Locations = list.Where(i => !i.Equals(Vector3.Zero)).ToArray();
            location = Locations[_random.Next(0, Locations.Length)];
            startmap = WorldManager.ZoneId;
        }

        public override bool IsDone { get { return _done; } }

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(

                CommonBehaviors.HandleLoading,
                new Decorator(r => WorldManager.ZoneId != startmap, new Action(r => _done = true)),
                new Decorator(ret => Navigator.InPosition(Core.Player.Location,location,Distance),
                    new Sequence(
                        new Action(ret => Navigator.Clear()),
                        new Sleep(1000),
                        new Action(ret =>
                        {
                            _done = true;
                        })
                        )),

              new Decorator(r=>UseMesh, CommonBehaviors.MoveAndStop(ret => location, Distance,stopInRange:true, destinationName: Name)),
              new Decorator(r=>!UseMesh,new Action(r =>
              {

                  Core.Player.Face(location);
                  MovementManager.MoveForwardStart();
              }))
                );
        }

        /// <summary>
        /// This gets called when a while loop starts over so reset anything that is used inside the IsDone check
        /// </summary>
        protected override void OnResetCachedDone()
        {
            _done = false;
        }

        protected override void OnDone()
        {
            // Force a stop!
            Navigator.PlayerMover.MoveStop();
        }
    }

}