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
            return new ActionRunCoroutine(r => PickupBattleLeveTask());
        }

        private async Task PickupBattleLeveTask()
        {
            LlamaLibrary.Helpers.NPC.Npc npc = new((uint)NpcId, ZoneId, Location);

            if (GuildLeve.Allowances == 0)
            {
                Log.Error($"No more leve allowances. Exiting.");
                return;
            }

            if (!await LlamaLibrary.Helpers.Navigation.GetToInteractNpcSelectString(npc))
            {
                Logging.WriteDiagnostic($"Failed to get to {DataManager.GetLocalizedNPCName(NpcId)}");
                return;
            }

            if (Conversation.IsOpen)
            {
                Log.Information($"Selecting {LlamaLibrary.Helpers.Translator.BattlecraftLeves}.");
                if (!Conversation.SelectLineContains(LlamaLibrary.Helpers.Translator.BattlecraftLeves))
                {
                    Log.Error($"Could not find {LlamaLibrary.Helpers.Translator.BattlecraftLeves}");
                    return;
                }

                await Coroutine.Wait(1000, () => LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen);
            }

            if (LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen)
            {
                foreach (var leveId in Ids.Where(id => !GuildLeve.HasLeve(id)))
                {
                    if (GuildLeve.Allowances > 0)
                    {
                        StatusText = "Picking up " + leveId;
                        Log.Information($"Picking up {leveId}");

                        await Coroutine.Sleep(1000);
                        LlamaLibrary.RemoteWindows.GuildLeve.Instance.AcceptLeve(leveId);
                        await Coroutine.Yield(); // so our memory lock updates and level allowances change
                    }
                }

                await Coroutine.Sleep(1000);
                if (LlamaLibrary.RemoteWindows.GuildLeve.Instance.IsOpen)
                {
                    LlamaLibrary.RemoteWindows.GuildLeve.Instance.Close();
                    await Coroutine.Wait(1000, () => Conversation.IsOpen);
                    if (Conversation.IsOpen)
                    {
                        Conversation.SelectQuit();
                        await Coroutine.Wait(1000, () => !Conversation.IsOpen);
                    }
                }
            }

            await GeneralFunctions.StopBusy();

            _isDone = true;
        }
    }
}