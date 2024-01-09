using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.Helpers.NPC;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("NameChocobo")]
    public class NameChocobo : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;


        public NameChocobo() : base()
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
            return new ActionRunCoroutine(r => NameThatChocobo());
        }

        // List of chocobos depending on which grand company you enrolled in
        public static List<KeyValuePair<GrandCompany, Npc>> Chocobos = new List<KeyValuePair<GrandCompany, Npc>>()
        {
            new KeyValuePair<GrandCompany, Npc>(GrandCompany.Order_Of_The_Twin_Adder, new Npc(1006001, 132, new Vector3(35.38562f, -0.8990279f, 67.55164f))),
            new KeyValuePair<GrandCompany, Npc>(GrandCompany.Maelstrom, new Npc(1006002, 129, new Vector3(45.82075f, 20f, -4.997596f))),
            new KeyValuePair<GrandCompany, Npc>(GrandCompany.Immortal_Flames, new Npc(1006003, 130, new Vector3(51.62122f,4f,-142.2294f))),
        };
        private async Task NameThatChocobo()
        {
            var name = await LlamaLibrary.Utilities.RetainerHire.GetName();

            var choco = Chocobos.FirstOrDefault(x => x.Key == Core.Me.GrandCompany).Value;

            if (!await LlamaLibrary.Helpers.Navigation.GetToInteractNpc(choco, LlamaLibrary.RemoteWindows.InputString.Instance))
            {

                Log.Information($"Failed to get to {DataManager.GetLocalizedNPCName((int)choco.NpcId)}");
                return;
            }

            if (InputString.Instance.IsOpen)
            {
                Log.Debug("Waiting a sec to enter name");
                await Coroutine.Sleep(1000);
                InputString.Instance.Confirm(name);
            }

            await Coroutine.Wait(5000, () => SelectYesno.IsOpen);

            if (SelectYesno.IsOpen)
            {
                Log.Debug("Selecting Yes");
                SelectYesno.Yes();
            }

            _isDone = true;
        }
    }
}