using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("PickupBattleLeve")]
    public class PickupBattleLeve : LLProfileBehavior
    {
        private bool _isDone;

        private uint[] ids;

        [XmlAttribute("LeveIds")]
        public int[] LeveIds { get; set; }

        [XmlAttribute("NpcId")]
        [XmlAttribute("NpcID")]
        [XmlAttribute("NPCID")]
        public int NpcId { get; set; }

        [DefaultValue(30)]
        [XmlAttribute("Timeout")]
        public int Timeout { get; set; }

        [XmlAttribute("XYZ")]
        public Vector3 Location { get; set; }

        [XmlAttribute("ZoneId")]
        public ushort ZoneId { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        private uint[] Ids
        {
            get { return ids ?? (ids = LeveIds.Select(Convert.ToUInt32).ToArray()); }
        }

        public PickupBattleLeve() : base()
        {
        }

        protected override void OnStart()
        {
            if (LeveIds == null || LeveIds.Length == 0)
            {
                Log.Error("LeveIds is required.");
                _isDone = true;
            }
        }

        protected override void OnDone()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
            ids = null;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => PickupBattleLeveTask());
        }

        private async Task PickupBattleLeveTask()
        {
            LlamaLibrary.Helpers.NPC.Npc npc = new((uint)NpcId, ZoneId, Location);

            if (GuildLeve.Allowances == 0)
            {
                Log.Error($"No more leve allowances. Exiting.");
                _isDone = true;
                return;
            }

            if (!await EnsureBattleLeveWindowOpen(npc))
            {
                Logging.WriteDiagnostic($"Failed to get to {DataManager.GetLocalizedNPCName(NpcId)}");
                _isDone = true;
                return;
            }

            foreach (var leveId in Ids.Distinct())
            {
                if (GuildLeve.HasLeve(leveId))
                {
                    Log.Information($"Already have leve {leveId}; skipping.");
                    continue;
                }

                if (GuildLeve.Allowances == 0)
                {
                    Log.Warning("No leve allowances remain; cannot attempt the remaining requested leves.");
                    break;
                }

                // AcceptLeve can close the list even when the requested leve is not
                // in the current rotation. Reopen it before every subsequent attempt
                // only when the game has closed it.
                if (!await EnsureBattleLeveWindowOpen(npc))
                {
                    Log.Error($"Could not reopen the Battlecraft Leves window before trying {leveId}.");
                    break;
                }

                StatusText = "Picking up " + leveId;
                Log.Information($"Attempting to pick up {leveId}.");

                var allowancesBefore = GuildLeve.Allowances;
                LlamaLibrary.RemoteWindows.GuildLeve.Instance.AcceptLeve(leveId);

                await Coroutine.Wait(
                    EffectiveTimeoutMilliseconds,
                    () => GuildLeve.HasLeve(leveId) ||
                          GuildLeve.Allowances < allowancesBefore ||
                          !LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen);

                // The window can disappear one pulse before the active-leve and
                // allowance memory updates arrive.
                if (!GuildLeve.HasLeve(leveId) && GuildLeve.Allowances >= allowancesBefore)
                {
                    await Coroutine.Wait(
                        1500,
                        () => GuildLeve.HasLeve(leveId) ||
                              GuildLeve.Allowances < allowancesBefore);
                }

                if (GuildLeve.HasLeve(leveId))
                {
                    Log.Information($"Successfully picked up {leveId}.");
                }
                else
                {
                    Log.Warning($"Leve {leveId} was not accepted; it may not be available in the current rotation.");
                }
            }

            await CloseLeveWindows();
            await GeneralFunctions.StopBusy();

            _isDone = true;
        }

        private int EffectiveTimeoutMilliseconds => Math.Max(3, Timeout) * 1000;

        private async Task<bool> EnsureBattleLeveWindowOpen(LlamaLibrary.Helpers.NPC.Npc npc)
        {
            if (LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen)
                return true;

            if (!Conversation.IsOpen &&
                !await LlamaLibrary.Helpers.Navigation.GetToInteractNpcSelectString(npc))
            {
                return false;
            }

            if (!Conversation.IsOpen)
            {
                await Coroutine.Wait(EffectiveTimeoutMilliseconds, () => Conversation.IsOpen);
            }

            if (Conversation.IsOpen)
            {
                Log.Information($"Selecting {LlamaLibrary.Helpers.Translator.BattlecraftLeves}.");
                if (!Conversation.SelectLineContains(LlamaLibrary.Helpers.Translator.BattlecraftLeves))
                {
                    Log.Error($"Could not find {LlamaLibrary.Helpers.Translator.BattlecraftLeves}");
                    return false;
                }
            }

            await Coroutine.Wait(
                EffectiveTimeoutMilliseconds,
                () => LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen);
            return LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen;
        }

        private async Task CloseLeveWindows()
        {
            if (LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen)
            {
                LlamaLibrary.RemoteWindows.GuildLeve.Instance.Close();
                await Coroutine.Wait(
                    EffectiveTimeoutMilliseconds,
                    () => !LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen);
            }

            await Coroutine.Wait(1500, () => Conversation.IsOpen);
            if (Conversation.IsOpen)
            {
                Conversation.SelectQuit();
                await Coroutine.Wait(
                    EffectiveTimeoutMilliseconds,
                    () => !Conversation.IsOpen);
            }
        }
    }
}
