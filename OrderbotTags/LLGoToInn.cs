using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Behavior;
using LlamaLibrary.Helpers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>
    /// The inns supported by LlamaLibrary and <see cref="LLGoToInn"/>.
    /// </summary>
    public enum InnLocation
    {
        LimsaLominsa = 0,
        Limsa = LimsaLominsa,
        Gridania = 1,
        NewGridania = Gridania,
        Uldah = 2,
        UlDah = Uldah,
        Ishgard = 3,
        Foundation = Ishgard,
        Kugane = 4,
        Crystarium = 5,
        TheCrystarium = Crystarium,
        OldSharlayan = 6,
        Sharlayan = OldSharlayan
    }

    /// <summary>
    /// Travels to the selected LlamaLibrary-supported inn and enters its inn room.
    /// </summary>
    [XmlElement("LLGoToInn")]
    public class LLGoToInn : LLProfileBehavior
    {
        private bool _isDone;

        /// <summary>
        /// Gets or sets the inn to enter. Defaults to Limsa Lominsa's Mizzenmast Inn.
        /// </summary>
        [XmlAttribute("Inn")]
        [DefaultValue(InnLocation.LimsaLominsa)]
        public InnLocation Inn { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(_ => GoToInnTask());
        }

        private async Task GoToInnTask()
        {
            if (_isDone)
            {
                await Coroutine.Yield();
                return;
            }

            try
            {
                var innKeeper = GetInnKeeper(Inn);
                if (innKeeper == null)
                {
                    Log.Error($"No LlamaLibrary innkeeper data is available for {Inn}.");
                    return;
                }

                StatusText = $"Traveling to the {Inn} inn";
                Log.Information($"Traveling to {innKeeper.Name} in {innKeeper.Location.ZoneName}.");

                if (!await NavigationHelper.GoToInnRoom(innKeeper))
                {
                    Log.Error($"LlamaLibrary failed to enter the {Inn} inn room.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Unexpected error while traveling to the {Inn} inn: {ex}");
            }
            finally
            {
                StatusText = string.Empty;
                _isDone = true;
            }
        }

        private static LlamaLibrary.Helpers.NPC.Npc GetInnKeeper(InnLocation inn)
        {
            switch (inn)
            {
                case InnLocation.LimsaLominsa:
                    return GetInnKeeperAt(NavigationHelper.StartingInnKeepers, 1);
                case InnLocation.Gridania:
                    return GetInnKeeperAt(NavigationHelper.StartingInnKeepers, 0);
                case InnLocation.Uldah:
                    return GetInnKeeperAt(NavigationHelper.StartingInnKeepers, 2);
                case InnLocation.Ishgard:
                    return GetInnKeeperAt(NavigationHelper.InnKeepers, 0);
                case InnLocation.Kugane:
                    return GetInnKeeperAt(NavigationHelper.InnKeepers, 1);
                case InnLocation.Crystarium:
                    return GetInnKeeperAt(NavigationHelper.InnKeepers, 2);
                case InnLocation.OldSharlayan:
                    return GetInnKeeperAt(NavigationHelper.InnKeepers, 3);
                default:
                    return null;
            }
        }

        private static LlamaLibrary.Helpers.NPC.Npc GetInnKeeperAt(
            System.Collections.Generic.IList<LlamaLibrary.Helpers.NPC.Npc> innKeepers,
            int index)
        {
            return index >= 0 && index < innKeepers.Count ? innKeepers[index] : null;
        }
    }
}
