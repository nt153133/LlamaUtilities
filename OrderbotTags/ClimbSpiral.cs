using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using TreeSharp;

namespace LlamaBotBases.OrderbotTags
{
    [XmlElement("ClimbSpiral")]
    public class ClimbSpiral : LLProfileBehavior
    {
        [XmlAttribute("XYZ")]
        public Vector3 XYZ
        {
            get => Position;
            set => Position = value;
        }

        public Vector3 Position = Vector3.Zero;

        [XmlAttribute("Heading")]
        [XmlAttribute("StartHeading")]
        public float StartHeading { get; set; }

        [XmlAttribute("Radians")]
        [XmlAttribute("Radian")]
        [DefaultValue(0.15)]
        public double Radians { get; set; } = 0.15;

        [XmlAttribute("Clockwise")]
        [DefaultValue(false)]
        public bool Clockwise { get; set; } = false;

        [XmlAttribute("StepNum")]
        [XmlAttribute("Count")]
        [DefaultValue(6)]
        public int StepNum { get; set; } = 6;

        [XmlAttribute("StepHeight")]
        [DefaultValue(1.435f)]
        public float StepHeight { get; set; } = 1.435f;

        [XmlAttribute("StepDistance")]
        [DefaultValue(3.25f)]
        public float StepDistance { get; set; } = 3.25f;

        [XmlAttribute("TimeOut")]
        [XmlAttribute("Timeout")]
        [DefaultValue(10_000)]
        public int TimeoutMs { get; set; } = 10_000;

        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public ClimbSpiral() : base()
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
            return new ActionRunCoroutine(r => ClimbSpiralStairs());
        }

        private Stopwatch _stopwatch;

        private async Task<bool> ClimbSpiralStairs()
        {
            if (_isDone)
            {
                await Coroutine.Yield();
                return false;
            }

            if (Core.Player.IsMounted)
            {
                await CommonTasks.StopAndDismount();
            }

            _stopwatch = Stopwatch.StartNew();

            Log.Information("ClimbSpiral moving to start.");
            MovementManager.SetFacing2D(XYZ);
            MovementManager.MoveForwardStart();
            while (Core.Player.Distance2D(XYZ) > 0.5f)
            {
                await Coroutine.Yield();
                if (_stopwatch.ElapsedMilliseconds > TimeoutMs)
                {
                    LogError("Taken too long to get to the start point. Stopping bot.");
                }
            }
            MovementManager.MoveStop();
            MovementManager.SetFacing(StartHeading);
            await Coroutine.Yield();
            var pointArray = new Vector3[StepNum];
            double currentHeading = StartHeading;
            var currentPoint = XYZ;
            for (int i = 0; i < pointArray.Length; i++)
            {
                var curPointVec2 = new Vector3(currentPoint.X, 0, currentPoint.Z);
                var newX = (float)(curPointVec2.X + (Math.Sin(currentHeading) * StepDistance));
                var newZ = (float)(curPointVec2.Z + (Math.Cos(currentHeading) * StepDistance));
                currentHeading += Radians;
                var nextPoint = new Vector3(newX, 0, newZ);
                pointArray[i] = new Vector3(nextPoint.X, currentPoint.Y - StepHeight, nextPoint.Z);
                currentPoint = pointArray[i];
            }
            for (int i = 0; i < pointArray.Length; i++)
            {
                var curPoint = pointArray[i];
                Log.Information($"ClimbSpiral moving to point #{i}, {pointArray[i].ToString()}");
                MovementManager.SetFacing2D(curPoint);
                MovementManager.MoveForwardStart();
                while (Core.Player.Distance2D(curPoint) > StepDistance / (i == pointArray.Length - 1 ? 3 : 2))
                {
                    await Coroutine.Yield();
                    MovementManager.SetFacing2D(curPoint);
                    if (_stopwatch.ElapsedMilliseconds > TimeoutMs)
                    {
                        LogError("We've taken too long to climb this staircase. Stopping bot.");
                    }
                    MovementManager.Jump();
                }

                MovementManager.MoveStop();
            }

            _isDone = true;
            return false;
        }
    }
}