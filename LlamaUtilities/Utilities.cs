﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using LlamaLibrary.Extensions;
using LlamaLibrary.JsonObjects;
using LlamaLibrary.Logging;
using LlamaLibrary.Managers;
using LlamaUtilities.LlamaUtilities.Settings;
using LlamaUtilities.LlamaUtilities.Tasks;
using Newtonsoft.Json;

namespace LlamaUtilities.LlamaUtilities
{
    public partial class Utilities : Form
    {
        private BagSlot _selectedBagSlot;
        private BagSlot _selectedBagSlotAffix;
        private IEnumerable<BagSlot> _selectedFilter = Filters.First().Key;
        private static readonly LLogger Log = new LLogger("Utilities Form", Colors.Pink);
        private string ChocooboRank => RaceChocoboManager.Rank.ToString();
        private string ChocooboCurrentExp => RaceChocoboManager.ExperienceCurrent.ToString();
        private string ChocooboMaxExp => RaceChocoboManager.ExperienceMax.ToString();

        private static readonly Dictionary<IEnumerable<BagSlot>, string> Filters = new Dictionary<IEnumerable<BagSlot>, string>()
        {
            { InventoryManager.FilledInventoryAndArmory, "All Items" },
            { InventoryManager.FilledArmorySlots, "Armory" },
            { InventoryManager.FilledSlots, "Inventory" }
        };

        public Utilities()
        {
            InitializeComponent();
        }

        public static void BindField(Control control, string propertyName, object dataSource, string dataMember)
        {
            Binding bd;

            for (var index = control.DataBindings.Count - 1; index == 0; index--)
            {
                bd = control.DataBindings[index];
                if (bd.PropertyName == propertyName)
                {
                    control.DataBindings.Remove(bd);
                }
            }

            try
            {
                control.DataBindings.Add(propertyName, dataSource, dataMember, false, DataSourceUpdateMode.OnPropertyChanged);
            }
            catch (Exception e)
            {
                Log.Information($"{control.Name} {propertyName}");
                Log.Exception(e);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            bindingSourceInventory.Clear();
            foreach (var bagSlot in _selectedFilter)
            {
                try
                {
                    if (bagSlot.Item.MateriaSlots > 0 && bagSlot.HasMateria())
                    {
                        bindingSourceInventory.Add(bagSlot);
                    }
                }
                catch (Exception ex)
                {
                    Log.Information(bagSlot.ToString());
                    Log.Exception(ex);
                }
            }

            itemCb.DisplayMember = "Name";
            itemCb.DataSource = bindingSourceInventory;
            itemCb.Update();
            itemCb_SelectionChangeCommitted(this, null);

            if (_selectedBagSlot != null)
            {
                materiaListBox.DataSource = _selectedBagSlot.Materia();
                materiaListBox.DisplayMember = "ItemName";
            }
        }

        private void btnRemoveMateria_Click(object sender, EventArgs e)
        {
            if (!_selectedBagSlot.IsValid || !_selectedBagSlot.IsFilled || !_selectedBagSlot.HasMateria())
            {
                return;
            }

            (uint, ushort) stuff = ((uint)_selectedBagSlot.BagId, _selectedBagSlot.Slot);
            var taskInfo = JsonConvert.SerializeObject(stuff);

            var task = new BotTask()
            {
                Type = TaskType.MateriaRemove,
                TaskInfo = taskInfo
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private static void StartBotBase()
        {
            if (BotManager.Current.Name == UtilitiesBase._name)
            {
                BotManager.Current.Start();
                TreeRoot.Start();
            }
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 1)
            {
                bindingSourceInventory.Clear();
                foreach (var bagSlot in InventoryManager.FilledInventoryAndArmory.Where(i => i.Item.MateriaSlots > 0 && i.HasMateria()))
                {
                    bindingSourceInventory.Add(bagSlot);
                }

                itemCb.DisplayMember = "Name";
                itemCb.DataSource = bindingSourceInventory;

                if (_selectedBagSlot != null)
                {
                    materiaListBox.DataSource = _selectedBagSlot.Materia();
                    materiaListBox.DisplayMember = "ItemName";
                }
            }
        }

        private void Utilities_Load(object sender, EventArgs e)
        {
            //nameLabel.Text = $"Chocobo Name: {RaceChocoboManager.NameFirst} {RaceChocoboManager.NameLast}";
            rankLabel.Text = $"Chocobo Rank: {ChocooboRank}";
            expLabel.Text = $"Chocobo EXP: {ChocooboCurrentExp}/{ChocooboMaxExp}";
            //ability1Label.Text = $"Hereditary: {RaceChocoboManager.AbilityHereditary}";
            //ability2Label.Text = $"Aquired: {RaceChocoboManager.AbilityLearned}";


            //Ideally this would be done using the localization property, but the stupid winform designer keeps crashing
            tabRetainers.Text = Resources.Localization.Utilities_TabRetainers;
            btnRetainers.Text = Resources.Localization.Utilities_BtnStart;
            tabPage1.Text = Resources.Localization.Utilities_TabDailyHunts;
            btnHuntStart.Text = Resources.Localization.Utilities_BtnStart;
            tabMateria.Text = Resources.Localization.Utilities_TabMateria;
            tabPageRemove.Text = Resources.Localization.Utilities_TabRemove;
            groupBoxRemoveFilter.Text = Resources.Localization.Utilities_InventoryFilter;
            groupBox1.Text = Resources.Localization.Utilities_RemoveMateria;
            btnRemoveMateria.Text = Resources.Localization.Utilities_BtnRemoveAllMateria;
            btnRefresh.Text = Resources.Localization.Utilities_BtnRefresh;
            tabAffix.Text = Resources.Localization.Utilities_TabAffix;
            button3.Text = Resources.Localization.Utilities_BtnRefresh;
            tabInventory.Text = Resources.Localization.Utilities_TabInventory;
            lblDesynth.Text = Resources.Localization.Utilities_LblDesynth;
            btnDesynth.Text = Resources.Localization.Utilities_BtnDesynth;
            btnCoffers.Text = Resources.Localization.Utilities_BtnOpenAllCoffers;
            btnExtract.Text = Resources.Localization.Utilities_BtnExtractMateria;
            btnReduce.Text = Resources.Localization.Utilities_BtnReduceAll;
            tabCustom.Text = Resources.Localization.Utilities_TabCustomDeliveries;
            btnCustomDeliveries.Text = Resources.Localization.Utilities_BtnStart;
            tabGC.Text = Resources.Localization.Utilities_TabGcTurnin;
            btnGcTurin.Text = Resources.Localization.Utilities_BtnStart;
            tabHousing.Text = Resources.Localization.Utilities_TabHousing;
            btnHousing.Text = Resources.Localization.Utilities_BtnStart;
            tabFC.Text = Resources.Localization.Utilities_TabFCWorkshop;
            btnFCWorkshop.Text = Resources.Localization.Utilities_BtnStart;
            tabPage2.Text = Resources.Localization.Utilities_TabChocoRacer;
            label2.Text = Resources.Localization.Utilities_LblChocoRacerInfo;
            expLabel.Text = Resources.Localization.Utilities_LblChocoExp;
            breederButton.Text = Resources.Localization.Utilities_BtnGoToBreeder;
            counterButton.Text = Resources.Localization.Utilities_BtnGoToRaceCounter;
            raceStart.Text = Resources.Localization.Utilities_BtnStart;




            tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;
            itemCb.SelectionChangeCommitted += new System.EventHandler(itemCb_SelectionChangeCommitted);
            filterCb.SelectionChangeCommitted += new System.EventHandler(filterCb_SelectionChangeCommitted);
            affixCb.SelectionChangeCommitted += new System.EventHandler(affixCb_SelectionChangeCommitted);
            tabControlMateria.SelectedIndexChanged += TabMateria_SelectedIndexChanged;
            pgHunts.SelectedObject = HuntsSettings.Instance;
            pgRetainers.SelectedObject = RetainerSettings.Instance;
            pgInventory.SelectedObject = ReduceSettings.Instance;
            pgCustomDeliveries.SelectedObject = DeliveriesSettings.Instance;
            pgChocoRacer.SelectedObject = RaceSettings.Instance;

            /*
            BindField(zhloeCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoZhloeDeliveries));
            BindField(mnaagoCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoMnaagoDeliveries));
            BindField(kurenaiCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoKurenaiDeliveries));
            BindField(adkiraghCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoAdkiraghDeliveries));
            BindField(kaishirrCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoKaishirrDeliveries));
            BindField(ehlltouCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoEhlltouDeliveries));
            BindField(charlemendCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoCharlemendDeliveries));
            BindField(amelianceCheckBox, "Checked", DeliveriesSettings.Instance, nameof(DeliveriesSettings.DoAmelianceeliveries));
*/
            filterCb.DataSource = new BindingSource(Filters, null);
            filterCb.DisplayMember = "Value";
            filterCb.ValueMember = "Key";
            filterCb.SelectedIndex = 0;

            bindingSourceInventory.Clear();

            foreach (var bagSlot in InventoryManager.FilledInventoryAndArmory.Where(i => i.Item.MateriaSlots > 0 && i.HasMateria()))
            {
                bindingSourceInventory.Add(bagSlot);
            }

            itemCb.DisplayMember = "Name";
            itemCb.DataSource = bindingSourceInventory;

            if (_selectedBagSlot != null)
            {
                materiaListBox.DataSource = _selectedBagSlot.Materia();
                materiaListBox.DisplayMember = "ItemName";
            }
        }

        private void itemCb_SelectionChangeCommitted(object sender, EventArgs e)
        {
            _selectedBagSlot = (BagSlot)itemCb.SelectedItem;
            if (_selectedBagSlot == null || !_selectedBagSlot.IsValid)
            {
                return;
            }

            materiaListBox.DataSource = _selectedBagSlot.Materia();
            materiaListBox.DisplayMember = "ItemName";
        }

        private void filterCb_SelectionChangeCommitted(object sender, EventArgs e)
        {
            _selectedFilter = ((KeyValuePair<IEnumerable<BagSlot>, string>)filterCb.SelectedItem).Key;
        }

        private void bindingSourceInventory_CurrentChanged(object sender, EventArgs e)
        {
        }

        private void btnHuntStart_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.Hunts,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnReduce_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.Reduce,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnExtract_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.Extract,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnCoffers_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.Coffers,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnHousing_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.Housing,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnCustomDeliveries_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.CustomDeliveries,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnGcTurin_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.GcTurnin,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnRetainers_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.Retainers,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnFCWorkshop_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.FCWorkshop,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void btnDesynth_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.Desynth,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void raceStart_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.ChocoRace,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private async void counterButton_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.GetToCounter,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private async void breederButton_Click(object sender, EventArgs e)
        {
            var task = new BotTask()
            {
                Type = TaskType.GetToBreeder,
                TaskInfo = ""
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            bindingSourceAffix.Clear();
            foreach (var bagSlot in InventoryManager.EquippedItems.Where(i => i.IsFilled && i.Item.MateriaSlots > 0 && i.MateriaCount() < 5))
            {
                bindingSourceAffix.Add(bagSlot);
            }

            affixCb.DisplayMember = "Name";
            affixCb.DataSource = bindingSourceAffix;
            affixCb.Update();

            if (bindingSourceAffix.Count > 0)
            {
                affixCb.SelectedIndex = 0;
                affixCb_SelectionChangeCommitted(this, null);
            }

            if (_selectedBagSlotAffix != null)
            {
                SetComboBoxes(_selectedBagSlotAffix);
            }


        }

        private void affixCb_SelectedIndexChanged(object sender, EventArgs e)
        {
            /*_selectedBagSlotAffix = (BagSlot) affixCb.SelectedItem;
            if (_selectedBagSlot == null || !_selectedBagSlot.IsValid)
            {
                Log.Information($"It's null {affixCb.SelectedIndex} {bindingSourceAffix.List[affixCb.SelectedIndex].ToString()}");
                return;
            }

            // affixLb.DataSource = MateriaBase.Materia(_selectedBagSlotAffix);
            // affixLb.DisplayMember = "ItemName";
            SetComboBoxes(_selectedBagSlotAffix);*/
        }

        private void affixCb_SelectionChangeCommitted(object sender, EventArgs e)
        {
            _selectedBagSlotAffix = (BagSlot)affixCb.SelectedItem;
            if (_selectedBagSlotAffix == null || !_selectedBagSlotAffix.IsValid)
            {
                Log.Information($"It's null {affixCb.SelectedIndex} {bindingSourceAffix.List[affixCb.SelectedIndex]}");
                return;
            }

            // affixLb.DataSource = MateriaBase.Materia(_selectedBagSlotAffix);
            // affixLb.DisplayMember = "ItemName";
            SetComboBoxes(_selectedBagSlotAffix);
        }

        private void SetComboBoxes(BagSlot slot)
        {
            var list = slot.Materia();
            //var inventoryMateria =
            bindingSourceInventoryMateria.DataSource = InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia);
            var materia = InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).OrderBy(i => i.index).ThenBy(l => l.MateriaItem.Tier).ToList();
            materia.Add(new MateriaToAffix(null));
            switch (list.Count)
            {
                case 0:
                    MateriaCb1.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList(); ;
                    MateriaCb1.Enabled = true;
                    MateriaCb1.DisplayMember = "Display";
                    MateriaCb2.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb2.Enabled = true;
                    MateriaCb2.DisplayMember = "Display";
                    MateriaCb3.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb3.Enabled = true;
                    MateriaCb3.DisplayMember = "Display";
                    MateriaCb4.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb4.Enabled = true;
                    MateriaCb4.DisplayMember = "Display";
                    MateriaCb5.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb5.Enabled = true;
                    MateriaCb5.DisplayMember = "Display";
                    break;

                case 1:
                    MateriaCb1.DataSource = list.ToArray();
                    MateriaCb1.SelectedIndex = 0;
                    MateriaCb1.DisplayMember = "ItemName";
                    MateriaCb1.Refresh();
                    MateriaCb1.Enabled = false;
                    MateriaCb2.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb2.DisplayMember = "Display";
                    MateriaCb3.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb3.DisplayMember = "Display";
                    MateriaCb4.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb4.DisplayMember = "Display";
                    MateriaCb5.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb5.DisplayMember = "Display";
                    MateriaCb2.Enabled = true;
                    MateriaCb3.Enabled = true;
                    MateriaCb4.Enabled = true;
                    MateriaCb5.Enabled = true;
                    break;

                case 2:
                    MateriaCb1.DataSource = list.ToArray();
                    MateriaCb1.SelectedIndex = 0;
                    MateriaCb1.DisplayMember = "ItemName";
                    MateriaCb1.Refresh();
                    MateriaCb1.Enabled = false;

                    MateriaCb2.DataSource = list.ToArray();
                    MateriaCb2.SelectedIndex = 1;
                    MateriaCb2.DisplayMember = "ItemName";
                    MateriaCb2.Enabled = false;
                    MateriaCb3.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb3.DisplayMember = "Display";
                    MateriaCb4.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb4.DisplayMember = "Display";
                    MateriaCb5.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb5.DisplayMember = "Display";
                    MateriaCb3.Enabled = true;
                    MateriaCb4.Enabled = true;
                    MateriaCb5.Enabled = true;
                    break;

                case 3:

                    MateriaCb1.DataSource = list.ToArray();
                    MateriaCb1.SelectedIndex = 0;
                    MateriaCb1.DisplayMember = "ItemName";
                    MateriaCb1.Refresh();
                    MateriaCb1.Enabled = false;

                    MateriaCb2.DataSource = list.ToArray();
                    MateriaCb2.SelectedIndex = 1;
                    MateriaCb2.DisplayMember = "ItemName";
                    MateriaCb2.Enabled = false;

                    MateriaCb3.DataSource = list.ToArray();
                    MateriaCb3.SelectedIndex = 2;
                    MateriaCb3.DisplayMember = "ItemName";
                    MateriaCb3.Enabled = false;
                    MateriaCb4.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb4.DisplayMember = "Display";
                    MateriaCb5.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb5.DisplayMember = "Display";
                    MateriaCb4.Enabled = true;
                    MateriaCb5.Enabled = true;
                    break;

                case 4:

                    MateriaCb1.DataSource = list.ToArray();
                    MateriaCb1.SelectedIndex = 0;
                    MateriaCb1.DisplayMember = "ItemName";
                    MateriaCb1.Refresh();
                    MateriaCb1.Enabled = false;

                    MateriaCb2.DataSource = list.ToArray();
                    MateriaCb2.SelectedIndex = 1;
                    MateriaCb2.DisplayMember = "ItemName";
                    MateriaCb2.Enabled = false;

                    MateriaCb3.DataSource = list.ToArray();
                    MateriaCb3.SelectedIndex = 2;
                    MateriaCb3.DisplayMember = "ItemName";
                    MateriaCb3.Enabled = false;

                    MateriaCb4.Enabled = false;
                    MateriaCb4.DataSource = list.ToArray();
                    MateriaCb4.SelectedIndex = 3;
                    MateriaCb4.DisplayMember = "ItemName";
                    MateriaCb5.DataSource = materia.ToList(); //InventoryManager.FilledSlots.Where(i => i.Item.EquipmentCatagory == ItemUiCategory.Materia).Select(r => new MateriaToAffix(r)).ToList();
                    MateriaCb5.DisplayMember = "Display";
                    MateriaCb5.Enabled = true;
                    break;

                default:
                    break;
            }
        }

        private void buttonAffix_Click(object sender, EventArgs e)
        {
            var list = _selectedBagSlotAffix.Materia();
            var materiaToAdd = new List<BagSlot>();
            //var inventoryMateria =
            //bindingSourceInventoryMateria.DataSource =InventoryManager.FilledSlots.Where(i=> i.Item.EquipmentCatagory == ItemUiCategory.Materia);

            switch (list.Count)
            {
                case 0:
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb1.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb2.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb3.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb4.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb5.SelectedItem).BagSlot);
                    break;

                case 1:
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb2.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb3.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb4.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb5.SelectedItem).BagSlot);
                    break;

                case 2:
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb3.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb4.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb5.SelectedItem).BagSlot);
                    break;

                case 3:
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb4.SelectedItem).BagSlot);
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb5.SelectedItem).BagSlot);
                    break;

                case 4:
                    materiaToAdd.Add(((MateriaToAffix)MateriaCb5.SelectedItem).BagSlot);
                    break;

                default:
                    break;
            }

            var slotMateriaList = new List<(uint, ushort)>
            {
                ((uint)_selectedBagSlotAffix.BagId, _selectedBagSlotAffix.Slot),
            };

            foreach (var slot in materiaToAdd)
            {
                if (slot == null)
                {
                    break;
                }

                slotMateriaList.Add(((uint)slot.BagId, slot.Slot));
            }

            // MateriaBase.MateriaToAdd = materiaToAdd;
            // MateriaBase.MateriaTask = MateriaTask.Affix;
            // MateriaBase.ItemToAffixMateria = _selectedBagSlotAffix;

            var taskInfo = JsonConvert.SerializeObject(slotMateriaList);

            var task = new BotTask()
            {
                Type = TaskType.MateriaAffix,
                TaskInfo = taskInfo
            };

            UtilitiesBase.BotTask = task;
            StartBotBase();
        }

        private void tabPageRemove_Click(object sender, EventArgs e)
        {
        }
        private void TabMateria_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControlMateria.SelectedIndex == 1)
            {
                bindingSourceAffix.Clear();
                foreach (var bagSlot in InventoryManager.EquippedItems.Where(i => i.IsFilled && i.Item.MateriaSlots > 0 && i.MateriaCount() < 5))
                {
                    bindingSourceAffix.Add(bagSlot);
                }

                if (bindingSourceAffix.Count > 0)
                {
                    affixCb.SelectedIndex = 0;
                    affixCb_SelectionChangeCommitted(this, null);
                }

                if (_selectedBagSlotAffix != null)
                {
                    affixCb.DisplayMember = "Name";
                    affixCb.DataSource = bindingSourceAffix;
                    SetComboBoxes(_selectedBagSlotAffix);
                }
            }
            //MessageBox.Show("You are in the TabControl.SelectedIndexChanged event.");
        }


    }

    public class MateriaToAffix
    {
        public BagSlot BagSlot;
        public MateriaItem MateriaItem;
        public int index;

        public string Display => BagSlot == null ? "Stop" : $"{BagSlot.Item.CurrentLocaleName} +{MateriaItem.Value} {MateriaItem.Stat}";

        public MateriaToAffix(BagSlot slot)
        {
            if (slot == null)
            {
                BagSlot = null;
                return;
            }

            BagSlot = slot;
            MateriaItem = LlamaLibrary.ResourceManager.MateriaList.Value.SelectMany(i => i.Value).FirstOrDefault(r => r.Key == slot.RawItemId);
            index = LlamaLibrary.ResourceManager.MateriaList.Value.FirstOrDefault(r => r.Value.Any(k => k.Key == slot.RawItemId)).Key;
        }
    }
}