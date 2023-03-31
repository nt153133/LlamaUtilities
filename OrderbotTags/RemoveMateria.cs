using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using LlamaLibrary.Extensions;
using LlamaLibrary.Logging;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("RemoveMateria")]
    public class RemoveMateria : LLProfileBehavior
    {
        private bool _isDone;

        private static readonly string NameValue = "RemoveMateria";
        private static readonly LLogger Log = new(NameValue, Colors.MediumPurple);

        private static int _lastValidItem;
        private static int _lastValidMateria;

        [XmlAttribute("EquipmentId")]
        [XmlAttribute("EquipmentID")]
        public int EquipemntItem { get; set; }

        public override bool HighPriority => true;

        public RemoveMateria() : base()
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
            return new ActionRunCoroutine(r => RemoveMateriaTask());
        }

        private async Task RemoveMateriaTask()
        {
            var equipmentToMeld = InventoryManager.FilledSlots.FirstOrDefault(i => i.RawItemId == (uint)EquipemntItem);
            Log.Information($"Removing materia from {equipmentToMeld.Name}");


            BagSlotExtensions.RemoveMateria(equipmentToMeld);
            await Coroutine.Wait(1000, () => LlamaLibrary.Utilities.Inventory.IsBusy );

            if (LlamaLibrary.Utilities.Inventory.IsBusy)
            {
                Log.Information($"Waiting to not be busy");
                await Coroutine.Wait(20000, () => !LlamaLibrary.Utilities.Inventory.IsBusy );
            }
            Log.Information($"Done");

            _isDone = true;
            return;
        }


        public override bool IsDone => _isDone;
    }
}