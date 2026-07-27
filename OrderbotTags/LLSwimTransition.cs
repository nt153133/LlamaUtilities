using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>
    /// Moves to a swimming transition point, then ascends out of or descends into
    /// a diving area.
    /// </summary>
    [XmlElement("LLSwimTransition")]
    public class LLSwimTransition : LLProfileBehavior
    {
        private const float DistanceTolerance = 3f;

        private bool _isDone;
        private bool _isAscending;
        private bool _isDescending;

        /// <summary>
        /// Location from which to perform the swimming transition.
        /// </summary>
        [XmlAttribute("XYZ", true)]
        public Vector3 XYZ { get; set; }

        /// <summary>
        /// Ascend until the player is no longer diving.
        /// </summary>
        [XmlAttribute("Ascending")]
        [DefaultValue(false)]
        public bool Ascending { get; set; }

        /// <summary>
        /// Descend until the player begins diving.
        /// </summary>
        [XmlAttribute("Descending")]
        [DefaultValue(false)]
        public bool Descending { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        protected override void OnStart()
        {
            if (Ascending == Descending)
            {
                LogError("Exactly one of Ascending or Descending must be true.");
                _isDone = true;
            }
        }

        protected override void OnDone()
        {
            StopMovement();
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
            _isAscending = false;
            _isDescending = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(_ => Run());
        }

        private async Task<bool> Run()
        {
            if (_isDone)
            {
                await Coroutine.Yield();
                return false;
            }

            // If a profile resumes after the transition, do not move back to the
            // transition point and repeat it.
            if ((Ascending && !MovementManager.IsDiving) ||
                (Descending && MovementManager.IsDiving))
            {
                _isDone = true;
                return false;
            }

            Log.Information($"Moving to swimming transition point {XYZ}.");
            while (Core.Player.Location.Distance(XYZ) > DistanceTolerance)
            {
                Navigator.PlayerMover.MoveTowards(XYZ);
                await Coroutine.Yield();
            }

            Navigator.PlayerMover.MoveStop();

            try
            {
                if (Ascending)
                {
                    Log.Information("Ascending until the player is no longer diving.");
                    _isAscending = true;
                    MovementManager.StartAscending();

                    while (MovementManager.IsDiving)
                    {
                        await Coroutine.Yield();
                    }
                }
                else
                {
                    Log.Information("Descending until the player begins diving.");
                    _isDescending = true;
                    MovementManager.StartDescending();

                    while (!MovementManager.IsDiving)
                    {
                        await Coroutine.Yield();
                    }
                }

                while (CommonBehaviors.IsLoading)
                {
                    await Coroutine.Yield();
                }
            }
            finally
            {
                StopMovement();
            }

            _isDone = true;
            return false;
        }

        private void StopMovement()
        {
            Navigator.PlayerMover.MoveStop();

            if (_isAscending)
            {
                MovementManager.StopAscending();
                _isAscending = false;
            }

            if (_isDescending)
            {
                MovementManager.StopDescending();
                _isDescending = false;
            }
        }
    }
}
