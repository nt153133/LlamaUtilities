using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("InfuseSphereScroll")]
    public class InfuseSphereScroll : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("WeaponId")]
        [XmlAttribute("WeaponID")]
        public int WeaponId { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public InfuseSphereScroll() : base()
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
            return new ActionRunCoroutine(r => InfuseTask());
        }

        private async Task InfuseTask()
        {
            while (await Infuse((uint)WeaponId))
            {
                Log.Information("Infusing");
            }

            //if window is still open close it
            if (RelicSphereScroll.Instance.IsOpen)
            {
                Log.Information("Closing Window");
                RelicSphereScroll.Instance.Close();
                await Coroutine.Wait(15000, () => !RelicSphereScroll.Instance.IsOpen);
            }

            _isDone = true;
        }

        public async Task<bool> Infuse(uint itemId)
        {
            if (!RelicSphereScroll.Instance.IsOpen)
            {
                var item = InventoryManager.FilledSlots.FirstOrDefault(i => i.RawItemId == itemId);
                if (item != null)
                {
                    await UseItem(item);
                    await Coroutine.Wait(15000, () => RelicSphereScroll.Instance.IsOpen);
                }
                else
                {
                    Log.Error($"{DataManager.GetItem(itemId).CurrentLocaleName} not found.");
                    return false;
                }
            }

            if (!RelicSphereScroll.Instance.IsOpen)
            {
                Log.Error($"RelicSphereScroll is not open");
                return false;
            }

            Log.Information("RelicSphereScroll is open");

            Log.Information($"Current Infused: {RelicSphereScroll.Instance.CurrentInfuse}/{RelicSphereScroll.Instance.MaxInfuse}");

            if (RelicSphereScroll.Instance.CurrentInfuse == RelicSphereScroll.Instance.MaxInfuse)
            {
                Log.Information("Max Infused");
                return false;
            }

            var materia = RelicSphereScroll.Instance.GetMateriaOptions();

            Log.Information($"Materia Options: {materia.Length}");

            if (materia.Length == 0)
            {
                Log.Information("No Materia Options");
                return false;
            }

            if (materia.All(i => !i.Highlighted))
            {
                Log.Error("No Highlighted Materia");
                return false;
            }

            var materiaToUse = materia.FirstOrDefault(i => i.Highlighted);

            Log.Information($"Materia to use: {materiaToUse?.Name}");

            if (materiaToUse == null)
            {
                Log.Error("No Materia to use");
                return false;
            }

            var item1 = DataManager.GetItem(materiaToUse.Name);

            if (item1 == null)
            {
                Log.Error($"No item found with name {materiaToUse.Name}");
                return false;
            }

            RelicSphereScroll.Instance.SelectMateria(materiaToUse.Index);

            if (!await Coroutine.Wait(15000, () => RelicSphereScroll.Instance.ItemId == item1.Id))
            {
                Log.Error("RelicSphereScroll did not change");
                return false;
            }

            RelicSphereScroll.Instance.Infuse();

            await Coroutine.Wait(15000, () => !RelicSphereScroll.Instance.IsOpen || SelectYesno.IsOpen);

            if (SelectYesno.IsOpen)
            {
                SelectYesno.ClickYes();
                await Coroutine.Wait(15000, () => !RelicSphereScroll.Instance.IsOpen);
            }

            return !RelicSphereScroll.Instance.IsOpen;
        }

        public async Task UseItem(BagSlot slot)
        {
            if (!slot.IsValid)
            {
                return;
            }

            await Coroutine.Wait(10000, () => slot.CanUse() && !MovementManager.IsOccupied);
            slot.UseItem();
        }
    }
}