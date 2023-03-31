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
    [XmlElement("AffixMateria")]
    public class AffixMateria : LLProfileBehavior
    {
        private bool _isDone;

        private static readonly string NameValue = "AffixMateria";
        private static readonly LLogger Log = new(NameValue, Colors.MediumPurple);

        private static int _lastValidItem;
        private static int _lastValidMateria;

        [XmlAttribute("EquipmentId")]
        [XmlAttribute("EquipmentID")]
        public int EquipemntItem { get; set; }

        [XmlAttribute("MateriaId")]
        [XmlAttribute("MateriaID")]
        public int MateriaItem { get; set; }

        public override bool HighPriority => true;

        public AffixMateria() : base()
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
            return new ActionRunCoroutine(r => AffixMatera());
        }

        private async Task AffixMatera()
        {
            var materiaToUse = InventoryManager.FilledSlots.FirstOrDefault(i => i.RawItemId == (uint)MateriaItem);
            var equipmentToMeld = InventoryManager.FilledSlots.FirstOrDefault(i => i.RawItemId == (uint)EquipemntItem);

            Log.Information($"Trying to affix {materiaToUse.Name} to {equipmentToMeld.Name}");
            if (!await OpenMeldWindow(equipmentToMeld))
            {
                Log.Error("Failed to open meld window!");
                TreeRoot.Stop("Materia Melding Failed");
                _isDone = true;
                return;
            }

            if (!await OpenMateriaAttachDialog())
            {
                Log.Error("Failed to open materia attach dialog!");
                TreeRoot.Stop("Materia Melding Failed");
                return;
            }

            Log.Debug("Sending BagSlot Affix");
            await Coroutine.Wait(1500, () => AgentMeld.Instance.CanMeld);
            if (!materiaToUse.IsValid || !materiaToUse.IsFilled) return;
            equipmentToMeld.AffixMateria(materiaToUse, true);
            await Coroutine.Wait(20000, () => !AgentMeld.Instance.Ready);
            await Coroutine.Wait(20000, () => AgentMeld.Instance.Ready);
            await Coroutine.Wait(7000, () => !MateriaAttachDialog.Instance.IsOpen);
            MateriaAttach.Instance.Close();
            await Coroutine.Wait(7000, () => !MateriaAttach.Instance.IsOpen);
            await Coroutine.Wait(7000, () => !LlamaLibrary.Utilities.Inventory.IsBusy );
            Log.Information($"Done");

            _isDone = true;
            return;
        }

        private static async Task<bool> OpenMeldWindow(BagSlot itemToAffix)
        {
            if (MateriaAttach.Instance.IsOpen) return true;
            for (int i = 0; i < 2; i++)
            {
                Log.Debug("Opening meld window");
                itemToAffix.OpenMeldInterface();
                await Coroutine.Wait(5000, () => MateriaAttach.Instance.IsOpen);
                if (MateriaAttach.Instance.IsOpen)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<bool> OpenMateriaAttachDialog()
        {
            if (MateriaAttachDialog.Instance.IsOpen) return true;
            Log.Debug("Opening materia attach dialog");
            // Try to select based on materia alone first... we should have them open due to meld requesting the specific item needed?
            for (int i = _lastValidMateria; i < 10; i++)
            {
                MateriaAttach.Instance.ClickMateria(i);
                int attachWait = _lastValidMateria > 0 && i == _lastValidMateria ? 1500 : 200;
                await Coroutine.Wait(attachWait, () => MateriaAttachDialog.Instance.IsOpen);
                if (MateriaAttachDialog.Instance.IsOpen)
                {
                    _lastValidMateria = i;
                    goto exitLoop;
                }
            }
            for (int i = _lastValidItem; i < 12; i++)
            {
                MateriaAttach.Instance.ClickItem(i);
                int clickWait = _lastValidItem > 0 && i == _lastValidItem ? 500 : 250;
                await Coroutine.Sleep(clickWait);
                for (int j = _lastValidMateria; j < 10; j++)
                {
                    MateriaAttach.Instance.ClickMateria(j);
                    int attachWait = _lastValidMateria > 0 && j == _lastValidMateria ? 1500 : 200;
                    await Coroutine.Wait(attachWait, () => MateriaAttachDialog.Instance.IsOpen);
                    if (MateriaAttachDialog.Instance.IsOpen)
                    {
                        _lastValidMateria = j;
                        _lastValidItem = i;
                        goto exitLoop;
                    }
                }
            }
            exitLoop:
            await Coroutine.Wait(3000, () => AgentMeld.Instance.CanMeld || AgentMeld.Instance.Ready);
            return MateriaAttachDialog.Instance.IsOpen;
        }

        public override bool IsDone => _isDone;
    }
}