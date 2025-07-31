using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.Helpers.NPC;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("EnterSouthHorn")]
    public class EnterSouthHorn : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        private static readonly LlamaLibrary.Logging.LLogger Log = new("Occult Crescent", Colors.DeepPink);

        private const uint SouthHornZoneId = 1252;
        private const uint PhantomVillageZoneId = 1269;
        private const uint UnfamiliarTerritoryQuestId = 70847;
        private static string OccultCrescent => OccultCrescentText[LlamaLibrary.Helpers.Translator.Language];
        private static string Yes => YesText[LlamaLibrary.Helpers.Translator.Language];

        private readonly Npc _jeffroy = new(1053611, 1269, new Vector3(-77.958374f, 5f, -15.396423f)); // Jeffroy
        private readonly Npc _passageToThePhantomVillage = new(2014671, 1185, new Vector3(206.21507f, -17.964502f, 56.969345f)); // Passage to the Phantom Village

        private static readonly Dictionary<ff14bot.Enums.Language, string> OccultCrescentText = new()
        {
            { ff14bot.Enums.Language.Eng, "Journey to the Occult Crescent: south horn." },
            { ff14bot.Enums.Language.Jap, "に突入する" },
            { ff14bot.Enums.Language.Fre, "Explorer l" },
            { ff14bot.Enums.Language.Ger, "betreten" },
            { ff14bot.Enums.Language.Chn, "Journey to the Occult Crescent: south horn." }
        };

        private static readonly Dictionary<ff14bot.Enums.Language, string> YesText = new()
        {
            { ff14bot.Enums.Language.Eng, "Yes" },
            { ff14bot.Enums.Language.Jap, "はい" },
            { ff14bot.Enums.Language.Fre, "Oui" },
            { ff14bot.Enums.Language.Ger, "Ja" },
            { ff14bot.Enums.Language.Chn, "Yes" }
        };

        public EnterSouthHorn() : base()
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
            return new ActionRunCoroutine(r => EnterSouthHornTask());
        }

        private async Task EnterSouthHornTask()
        {
            if (_isDone)
            {
                await Coroutine.Yield();
                return;
            }

            if (!QuestLogManager.IsQuestCompleted(UnfamiliarTerritoryQuestId))
            {
                Log.Error($"You must have completed the quest {DataManager.GetLocalizedQuestName((int)UnfamiliarTerritoryQuestId)} to access South Horn.");
                _isDone = true;
                return;
            }

            while (WorldManager.ZoneId != SouthHornZoneId)
            {
                if (WorldManager.ZoneId != PhantomVillageZoneId)
                {
                    Log.Information("Traveling to Phantom Village.");
                    await LlamaLibrary.Helpers.Navigation.UseNpcTransition(_passageToThePhantomVillage.Location.ZoneId, _passageToThePhantomVillage.Location.Coordinates, _passageToThePhantomVillage.NpcId, 0);
                }

                if (!await LlamaLibrary.Helpers.Navigation.GetToInteractNpcSelectString(_jeffroy))
                {
                    Log.Error($"Failed to get to {DataManager.GetLocalizedNPCName((int)_jeffroy.NpcId)}");
                    return;
                }

                if (ff14bot.RemoteWindows.SelectString.IsOpen)
                {
                    Log.Information($"Selecting {OccultCrescent}");
                    ff14bot.RemoteWindows.SelectString.ClickLineContains(OccultCrescent);
                    await Coroutine.Wait(5000, () => !SelectString.IsOpen);
                    await Coroutine.Wait(5000, () => SelectString.IsOpen);
                }

                if (ff14bot.RemoteWindows.SelectString.IsOpen)
                {
                    Log.Information($"Clicking {Yes}");
                    ff14bot.RemoteWindows.SelectString.ClickLineContains(Yes);
                    await Coroutine.Wait(5000, () => ContentsFinderConfirm.IsOpen);
                }

                if (ff14bot.RemoteWindows.ContentsFinderConfirm.IsOpen)
                {
                    Log.Information("Commencing Duty.");
                    ff14bot.RemoteWindows.ContentsFinderConfirm.Commence();
                    await Coroutine.Wait(10000, () => CommonBehaviors.IsLoading);
                    if (CommonBehaviors.IsLoading)
                    {
                        await Coroutine.Wait(-1, () => !CommonBehaviors.IsLoading);
                        await Coroutine.Wait(-1, () => WorldManager.ZoneId == 1252);
                    }
                }

                if (WorldManager.ZoneId == SouthHornZoneId)
                {
                    Log.Information($"We are now in {WorldManager.CurrentZoneName}.");
                }
            }

            _isDone = true;
        }
    }
}