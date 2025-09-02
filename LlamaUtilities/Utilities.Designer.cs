
namespace LlamaUtilities.LlamaUtilities
{
    partial class Utilities
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            tabControl1 = new System.Windows.Forms.TabControl();
            tabRetainers = new System.Windows.Forms.TabPage();
            btnRetainers = new System.Windows.Forms.Button();
            pgRetainers = new System.Windows.Forms.PropertyGrid();
            tabPage1 = new System.Windows.Forms.TabPage();
            btnHuntStart = new System.Windows.Forms.Button();
            pgHunts = new System.Windows.Forms.PropertyGrid();
            tabMateria = new System.Windows.Forms.TabPage();
            tabControlMateria = new System.Windows.Forms.TabControl();
            tabPageRemove = new System.Windows.Forms.TabPage();
            groupBoxRemoveFilter = new System.Windows.Forms.GroupBox();
            filterCb = new System.Windows.Forms.ComboBox();
            groupBox1 = new System.Windows.Forms.GroupBox();
            btnRemoveMateria = new System.Windows.Forms.Button();
            materiaListBox = new System.Windows.Forms.ListBox();
            btnRefresh = new System.Windows.Forms.Button();
            itemCb = new System.Windows.Forms.ComboBox();
            tabAffix = new System.Windows.Forms.TabPage();
            groupBox2 = new System.Windows.Forms.GroupBox();
            buttonAffix = new System.Windows.Forms.Button();
            MateriaCb5 = new System.Windows.Forms.ComboBox();
            MateriaCb4 = new System.Windows.Forms.ComboBox();
            MateriaCb3 = new System.Windows.Forms.ComboBox();
            MateriaCb2 = new System.Windows.Forms.ComboBox();
            MateriaCb1 = new System.Windows.Forms.ComboBox();
            button3 = new System.Windows.Forms.Button();
            affixCb = new System.Windows.Forms.ComboBox();
            bindingSourceAffix = new System.Windows.Forms.BindingSource(components);
            tabInventory = new System.Windows.Forms.TabPage();
            lblDesynth = new System.Windows.Forms.Label();
            pgInventory = new System.Windows.Forms.PropertyGrid();
            btnDesynth = new System.Windows.Forms.Button();
            btnCoffers = new System.Windows.Forms.Button();
            btnExtract = new System.Windows.Forms.Button();
            btnReduce = new System.Windows.Forms.Button();
            tabCustom = new System.Windows.Forms.TabPage();
            btnCustomDeliveries = new System.Windows.Forms.Button();
            pgCustomDeliveries = new System.Windows.Forms.PropertyGrid();
            tabGC = new System.Windows.Forms.TabPage();
            btnGcTurin = new System.Windows.Forms.Button();
            propertyGrid1 = new System.Windows.Forms.PropertyGrid();
            tabHousing = new System.Windows.Forms.TabPage();
            btnHousing = new System.Windows.Forms.Button();
            tabFC = new System.Windows.Forms.TabPage();
            btnFCWorkshop = new System.Windows.Forms.Button();
            tabPage2 = new System.Windows.Forms.TabPage();
            label2 = new System.Windows.Forms.Label();
            expLabel = new System.Windows.Forms.Label();
            rankLabel = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            breederButton = new System.Windows.Forms.Button();
            counterButton = new System.Windows.Forms.Button();
            raceStart = new System.Windows.Forms.Button();
            pgChocoRacer = new System.Windows.Forms.PropertyGrid();
            bindingSourceInventory = new System.Windows.Forms.BindingSource(components);
            bindingSourceInventoryMateria = new System.Windows.Forms.BindingSource(components);
            tabControl1.SuspendLayout();
            tabRetainers.SuspendLayout();
            tabPage1.SuspendLayout();
            tabMateria.SuspendLayout();
            tabControlMateria.SuspendLayout();
            tabPageRemove.SuspendLayout();
            groupBoxRemoveFilter.SuspendLayout();
            groupBox1.SuspendLayout();
            tabAffix.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)bindingSourceAffix).BeginInit();
            tabInventory.SuspendLayout();
            tabCustom.SuspendLayout();
            tabGC.SuspendLayout();
            tabHousing.SuspendLayout();
            tabFC.SuspendLayout();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)bindingSourceInventory).BeginInit();
            ((System.ComponentModel.ISupportInitialize)bindingSourceInventoryMateria).BeginInit();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabRetainers);
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabMateria);
            tabControl1.Controls.Add(tabInventory);
            tabControl1.Controls.Add(tabCustom);
            tabControl1.Controls.Add(tabGC);
            tabControl1.Controls.Add(tabHousing);
            tabControl1.Controls.Add(tabFC);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            tabControl1.Location = new System.Drawing.Point(0, 0);
            tabControl1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new System.Drawing.Size(606, 444);
            tabControl1.TabIndex = 0;
            // 
            // tabRetainers
            // 
            tabRetainers.Controls.Add(btnRetainers);
            tabRetainers.Controls.Add(pgRetainers);
            tabRetainers.Location = new System.Drawing.Point(4, 24);
            tabRetainers.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabRetainers.Name = "tabRetainers";
            tabRetainers.Size = new System.Drawing.Size(598, 416);
            tabRetainers.TabIndex = 7;
            tabRetainers.Text = "Retainers";
            tabRetainers.UseVisualStyleBackColor = true;
            // 
            // btnRetainers
            // 
            btnRetainers.Location = new System.Drawing.Point(330, 367);
            btnRetainers.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnRetainers.Name = "btnRetainers";
            btnRetainers.Size = new System.Drawing.Size(110, 27);
            btnRetainers.TabIndex = 1;
            btnRetainers.Text = "Start";
            btnRetainers.UseVisualStyleBackColor = true;
            btnRetainers.Click += btnRetainers_Click;
            // 
            // pgRetainers
            // 
            pgRetainers.Location = new System.Drawing.Point(9, 3);
            pgRetainers.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pgRetainers.Name = "pgRetainers";
            pgRetainers.Size = new System.Drawing.Size(430, 213);
            pgRetainers.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(btnHuntStart);
            tabPage1.Controls.Add(pgHunts);
            tabPage1.Location = new System.Drawing.Point(4, 24);
            tabPage1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage1.Size = new System.Drawing.Size(598, 416);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Daily Hunts";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // btnHuntStart
            // 
            btnHuntStart.Location = new System.Drawing.Point(309, 370);
            btnHuntStart.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnHuntStart.Name = "btnHuntStart";
            btnHuntStart.Size = new System.Drawing.Size(111, 29);
            btnHuntStart.TabIndex = 1;
            btnHuntStart.Text = "Start";
            btnHuntStart.UseVisualStyleBackColor = true;
            btnHuntStart.Click += btnHuntStart_Click;
            // 
            // pgHunts
            // 
            pgHunts.Location = new System.Drawing.Point(10, 14);
            pgHunts.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pgHunts.Name = "pgHunts";
            pgHunts.Size = new System.Drawing.Size(411, 275);
            pgHunts.TabIndex = 0;
            // 
            // tabMateria
            // 
            tabMateria.Controls.Add(tabControlMateria);
            tabMateria.Location = new System.Drawing.Point(4, 24);
            tabMateria.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabMateria.Name = "tabMateria";
            tabMateria.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabMateria.Size = new System.Drawing.Size(598, 416);
            tabMateria.TabIndex = 1;
            tabMateria.Text = "Materia";
            tabMateria.UseVisualStyleBackColor = true;
            // 
            // tabControlMateria
            // 
            tabControlMateria.Controls.Add(tabPageRemove);
            tabControlMateria.Controls.Add(tabAffix);
            tabControlMateria.Location = new System.Drawing.Point(9, 7);
            tabControlMateria.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabControlMateria.Name = "tabControlMateria";
            tabControlMateria.SelectedIndex = 0;
            tabControlMateria.Size = new System.Drawing.Size(580, 398);
            tabControlMateria.TabIndex = 1;
            // 
            // tabPageRemove
            // 
            tabPageRemove.Controls.Add(groupBoxRemoveFilter);
            tabPageRemove.Controls.Add(groupBox1);
            tabPageRemove.Location = new System.Drawing.Point(4, 24);
            tabPageRemove.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPageRemove.Name = "tabPageRemove";
            tabPageRemove.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPageRemove.Size = new System.Drawing.Size(572, 370);
            tabPageRemove.TabIndex = 0;
            tabPageRemove.Text = "Remove";
            tabPageRemove.UseVisualStyleBackColor = true;
            tabPageRemove.Click += tabPageRemove_Click;
            // 
            // groupBoxRemoveFilter
            // 
            groupBoxRemoveFilter.Controls.Add(filterCb);
            groupBoxRemoveFilter.Location = new System.Drawing.Point(404, 7);
            groupBoxRemoveFilter.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            groupBoxRemoveFilter.Name = "groupBoxRemoveFilter";
            groupBoxRemoveFilter.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            groupBoxRemoveFilter.Size = new System.Drawing.Size(160, 303);
            groupBoxRemoveFilter.TabIndex = 1;
            groupBoxRemoveFilter.TabStop = false;
            groupBoxRemoveFilter.Text = "Inventory Filter";
            // 
            // filterCb
            // 
            filterCb.FormattingEnabled = true;
            filterCb.Location = new System.Drawing.Point(7, 22);
            filterCb.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            filterCb.Name = "filterCb";
            filterCb.Size = new System.Drawing.Size(145, 23);
            filterCb.TabIndex = 0;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(btnRemoveMateria);
            groupBox1.Controls.Add(materiaListBox);
            groupBox1.Controls.Add(btnRefresh);
            groupBox1.Controls.Add(itemCb);
            groupBox1.Location = new System.Drawing.Point(7, 7);
            groupBox1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            groupBox1.Size = new System.Drawing.Size(390, 303);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Remove Materia";
            // 
            // btnRemoveMateria
            // 
            btnRemoveMateria.Location = new System.Drawing.Point(10, 213);
            btnRemoveMateria.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnRemoveMateria.Name = "btnRemoveMateria";
            btnRemoveMateria.Size = new System.Drawing.Size(166, 29);
            btnRemoveMateria.TabIndex = 3;
            btnRemoveMateria.Text = "Remove All Materia";
            btnRemoveMateria.UseVisualStyleBackColor = true;
            btnRemoveMateria.Click += btnRemoveMateria_Click;
            // 
            // materiaListBox
            // 
            materiaListBox.FormattingEnabled = true;
            materiaListBox.ItemHeight = 15;
            materiaListBox.Location = new System.Drawing.Point(10, 52);
            materiaListBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            materiaListBox.Name = "materiaListBox";
            materiaListBox.Size = new System.Drawing.Size(346, 154);
            materiaListBox.TabIndex = 2;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new System.Drawing.Point(267, 20);
            btnRefresh.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new System.Drawing.Size(91, 24);
            btnRefresh.TabIndex = 1;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // itemCb
            // 
            itemCb.FormattingEnabled = true;
            itemCb.Location = new System.Drawing.Point(10, 20);
            itemCb.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            itemCb.Name = "itemCb";
            itemCb.Size = new System.Drawing.Size(243, 23);
            itemCb.TabIndex = 0;
            // 
            // tabAffix
            // 
            tabAffix.Controls.Add(groupBox2);
            tabAffix.Location = new System.Drawing.Point(4, 24);
            tabAffix.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabAffix.Name = "tabAffix";
            tabAffix.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabAffix.Size = new System.Drawing.Size(572, 370);
            tabAffix.TabIndex = 1;
            tabAffix.Text = "Affix";
            tabAffix.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(buttonAffix);
            groupBox2.Controls.Add(MateriaCb5);
            groupBox2.Controls.Add(MateriaCb4);
            groupBox2.Controls.Add(MateriaCb3);
            groupBox2.Controls.Add(MateriaCb2);
            groupBox2.Controls.Add(MateriaCb1);
            groupBox2.Controls.Add(button3);
            groupBox2.Controls.Add(affixCb);
            groupBox2.Location = new System.Drawing.Point(9, 8);
            groupBox2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            groupBox2.Size = new System.Drawing.Size(366, 329);
            groupBox2.TabIndex = 0;
            groupBox2.TabStop = false;
            groupBox2.Text = "Affix";
            // 
            // buttonAffix
            // 
            buttonAffix.Location = new System.Drawing.Point(214, 220);
            buttonAffix.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            buttonAffix.Name = "buttonAffix";
            buttonAffix.Size = new System.Drawing.Size(140, 27);
            buttonAffix.TabIndex = 7;
            buttonAffix.Text = "Affix";
            buttonAffix.UseVisualStyleBackColor = true;
            buttonAffix.Click += buttonAffix_Click;
            // 
            // MateriaCb5
            // 
            MateriaCb5.FormattingEnabled = true;
            MateriaCb5.Location = new System.Drawing.Point(14, 189);
            MateriaCb5.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MateriaCb5.Name = "MateriaCb5";
            MateriaCb5.Size = new System.Drawing.Size(339, 23);
            MateriaCb5.TabIndex = 6;
            // 
            // MateriaCb4
            // 
            MateriaCb4.FormattingEnabled = true;
            MateriaCb4.Location = new System.Drawing.Point(14, 158);
            MateriaCb4.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MateriaCb4.Name = "MateriaCb4";
            MateriaCb4.Size = new System.Drawing.Size(339, 23);
            MateriaCb4.TabIndex = 5;
            // 
            // MateriaCb3
            // 
            MateriaCb3.FormattingEnabled = true;
            MateriaCb3.Location = new System.Drawing.Point(14, 127);
            MateriaCb3.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MateriaCb3.Name = "MateriaCb3";
            MateriaCb3.Size = new System.Drawing.Size(339, 23);
            MateriaCb3.TabIndex = 4;
            // 
            // MateriaCb2
            // 
            MateriaCb2.FormattingEnabled = true;
            MateriaCb2.Location = new System.Drawing.Point(14, 96);
            MateriaCb2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MateriaCb2.Name = "MateriaCb2";
            MateriaCb2.Size = new System.Drawing.Size(339, 23);
            MateriaCb2.TabIndex = 3;
            // 
            // MateriaCb1
            // 
            MateriaCb1.FormattingEnabled = true;
            MateriaCb1.Location = new System.Drawing.Point(14, 65);
            MateriaCb1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            MateriaCb1.Name = "MateriaCb1";
            MateriaCb1.Size = new System.Drawing.Size(339, 23);
            MateriaCb1.TabIndex = 2;
            // 
            // button3
            // 
            button3.Location = new System.Drawing.Point(272, 22);
            button3.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            button3.Name = "button3";
            button3.Size = new System.Drawing.Size(82, 27);
            button3.TabIndex = 1;
            button3.Text = "Refresh";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // affixCb
            // 
            affixCb.DataSource = bindingSourceAffix;
            affixCb.FormattingEnabled = true;
            affixCb.Location = new System.Drawing.Point(14, 24);
            affixCb.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            affixCb.Name = "affixCb";
            affixCb.Size = new System.Drawing.Size(250, 23);
            affixCb.TabIndex = 0;
            affixCb.SelectedIndexChanged += affixCb_SelectedIndexChanged;
            // 
            // tabInventory
            // 
            tabInventory.Controls.Add(lblDesynth);
            tabInventory.Controls.Add(pgInventory);
            tabInventory.Controls.Add(btnDesynth);
            tabInventory.Controls.Add(btnCoffers);
            tabInventory.Controls.Add(btnExtract);
            tabInventory.Controls.Add(btnReduce);
            tabInventory.Location = new System.Drawing.Point(4, 24);
            tabInventory.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabInventory.Name = "tabInventory";
            tabInventory.Size = new System.Drawing.Size(598, 416);
            tabInventory.TabIndex = 2;
            tabInventory.Text = "Inventory";
            tabInventory.UseVisualStyleBackColor = true;
            // 
            // lblDesynth
            // 
            lblDesynth.AutoSize = true;
            lblDesynth.Location = new System.Drawing.Point(159, 270);
            lblDesynth.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblDesynth.Name = "lblDesynth";
            lblDesynth.Size = new System.Drawing.Size(225, 15);
            lblDesynth.TabIndex = 5;
            lblDesynth.Text = "<- Desynth all trust gear and possibly fish";
            // 
            // pgInventory
            // 
            pgInventory.Location = new System.Drawing.Point(153, 16);
            pgInventory.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pgInventory.Name = "pgInventory";
            pgInventory.Size = new System.Drawing.Size(434, 230);
            pgInventory.TabIndex = 4;
            // 
            // btnDesynth
            // 
            btnDesynth.Location = new System.Drawing.Point(9, 263);
            btnDesynth.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnDesynth.Name = "btnDesynth";
            btnDesynth.Size = new System.Drawing.Size(142, 28);
            btnDesynth.TabIndex = 3;
            btnDesynth.Text = "Desynth";
            btnDesynth.UseVisualStyleBackColor = true;
            btnDesynth.Click += btnDesynth_Click;
            // 
            // btnCoffers
            // 
            btnCoffers.Location = new System.Drawing.Point(4, 85);
            btnCoffers.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnCoffers.Name = "btnCoffers";
            btnCoffers.Size = new System.Drawing.Size(142, 28);
            btnCoffers.TabIndex = 2;
            btnCoffers.Text = "Open All Coffers";
            btnCoffers.UseVisualStyleBackColor = true;
            btnCoffers.Click += btnCoffers_Click;
            // 
            // btnExtract
            // 
            btnExtract.Location = new System.Drawing.Point(4, 51);
            btnExtract.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnExtract.Name = "btnExtract";
            btnExtract.Size = new System.Drawing.Size(142, 28);
            btnExtract.TabIndex = 1;
            btnExtract.Text = "Extract Materia";
            btnExtract.UseVisualStyleBackColor = true;
            btnExtract.Click += btnExtract_Click;
            // 
            // btnReduce
            // 
            btnReduce.Location = new System.Drawing.Point(4, 16);
            btnReduce.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnReduce.Name = "btnReduce";
            btnReduce.Size = new System.Drawing.Size(142, 28);
            btnReduce.TabIndex = 0;
            btnReduce.Text = "Reduce All";
            btnReduce.UseVisualStyleBackColor = true;
            btnReduce.Click += btnReduce_Click;
            // 
            // tabCustom
            // 
            tabCustom.Controls.Add(btnCustomDeliveries);
            tabCustom.Controls.Add(pgCustomDeliveries);
            tabCustom.Location = new System.Drawing.Point(4, 24);
            tabCustom.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabCustom.Name = "tabCustom";
            tabCustom.Size = new System.Drawing.Size(598, 416);
            tabCustom.TabIndex = 3;
            tabCustom.Text = "Custom Deliveries";
            tabCustom.UseVisualStyleBackColor = true;
            // 
            // btnCustomDeliveries
            // 
            btnCustomDeliveries.Location = new System.Drawing.Point(345, 378);
            btnCustomDeliveries.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnCustomDeliveries.Name = "btnCustomDeliveries";
            btnCustomDeliveries.Size = new System.Drawing.Size(88, 27);
            btnCustomDeliveries.TabIndex = 1;
            btnCustomDeliveries.Text = "Start";
            btnCustomDeliveries.UseVisualStyleBackColor = true;
            btnCustomDeliveries.Click += btnCustomDeliveries_Click;
            // 
            // pgCustomDeliveries
            // 
            pgCustomDeliveries.Location = new System.Drawing.Point(4, 3);
            pgCustomDeliveries.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pgCustomDeliveries.Name = "pgCustomDeliveries";
            pgCustomDeliveries.Size = new System.Drawing.Size(429, 368);
            pgCustomDeliveries.TabIndex = 0;
            // 
            // tabGC
            // 
            tabGC.Controls.Add(btnGcTurin);
            tabGC.Controls.Add(propertyGrid1);
            tabGC.Location = new System.Drawing.Point(4, 24);
            tabGC.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabGC.Name = "tabGC";
            tabGC.Size = new System.Drawing.Size(598, 416);
            tabGC.TabIndex = 4;
            tabGC.Text = "Gc Turnin";
            tabGC.UseVisualStyleBackColor = true;
            // 
            // btnGcTurin
            // 
            btnGcTurin.Location = new System.Drawing.Point(336, 378);
            btnGcTurin.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnGcTurin.Name = "btnGcTurin";
            btnGcTurin.Size = new System.Drawing.Size(103, 27);
            btnGcTurin.TabIndex = 1;
            btnGcTurin.Text = "Start";
            btnGcTurin.UseVisualStyleBackColor = true;
            btnGcTurin.Click += btnGcTurin_Click;
            // 
            // propertyGrid1
            // 
            propertyGrid1.Location = new System.Drawing.Point(4, 3);
            propertyGrid1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            propertyGrid1.Name = "propertyGrid1";
            propertyGrid1.Size = new System.Drawing.Size(435, 253);
            propertyGrid1.TabIndex = 0;
            // 
            // tabHousing
            // 
            tabHousing.Controls.Add(btnHousing);
            tabHousing.Location = new System.Drawing.Point(4, 24);
            tabHousing.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabHousing.Name = "tabHousing";
            tabHousing.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabHousing.Size = new System.Drawing.Size(598, 416);
            tabHousing.TabIndex = 5;
            tabHousing.Text = "Housing";
            tabHousing.UseVisualStyleBackColor = true;
            // 
            // btnHousing
            // 
            btnHousing.Location = new System.Drawing.Point(322, 354);
            btnHousing.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnHousing.Name = "btnHousing";
            btnHousing.Size = new System.Drawing.Size(111, 27);
            btnHousing.TabIndex = 0;
            btnHousing.Text = "Start";
            btnHousing.UseVisualStyleBackColor = true;
            btnHousing.Click += btnHousing_Click;
            // 
            // tabFC
            // 
            tabFC.Controls.Add(btnFCWorkshop);
            tabFC.Location = new System.Drawing.Point(4, 24);
            tabFC.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabFC.Name = "tabFC";
            tabFC.Size = new System.Drawing.Size(598, 416);
            tabFC.TabIndex = 6;
            tabFC.Text = "FCWorkshop";
            tabFC.UseVisualStyleBackColor = true;
            // 
            // btnFCWorkshop
            // 
            btnFCWorkshop.Location = new System.Drawing.Point(327, 377);
            btnFCWorkshop.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            btnFCWorkshop.Name = "btnFCWorkshop";
            btnFCWorkshop.Size = new System.Drawing.Size(98, 28);
            btnFCWorkshop.TabIndex = 0;
            btnFCWorkshop.Text = "Start";
            btnFCWorkshop.UseVisualStyleBackColor = true;
            btnFCWorkshop.Click += btnFCWorkshop_Click;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(label2);
            tabPage2.Controls.Add(expLabel);
            tabPage2.Controls.Add(rankLabel);
            tabPage2.Controls.Add(label1);
            tabPage2.Controls.Add(breederButton);
            tabPage2.Controls.Add(counterButton);
            tabPage2.Controls.Add(raceStart);
            tabPage2.Controls.Add(pgChocoRacer);
            tabPage2.Location = new System.Drawing.Point(4, 24);
            tabPage2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new System.Windows.Forms.Padding(4, 3, 4, 3);
            tabPage2.Size = new System.Drawing.Size(598, 416);
            tabPage2.TabIndex = 8;
            tabPage2.Text = "ChocoRacer";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            label2.Location = new System.Drawing.Point(408, 350);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(178, 59);
            label2.TabIndex = 7;
            label2.Text = "This Bot only supports Choco Cure II, Cure III, Dash II and Super Sprint.";
            // 
            // expLabel
            // 
            expLabel.AutoSize = true;
            expLabel.Location = new System.Drawing.Point(385, 43);
            expLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            expLabel.Name = "expLabel";
            expLabel.Size = new System.Drawing.Size(66, 15);
            expLabel.TabIndex = 6;
            expLabel.Text = "Choco Exp:";
            // 
            // rankLabel
            // 
            rankLabel.AutoSize = true;
            rankLabel.Location = new System.Drawing.Point(385, 28);
            rankLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            rankLabel.Name = "rankLabel";
            rankLabel.Size = new System.Drawing.Size(36, 15);
            rankLabel.TabIndex = 5;
            rankLabel.Text = "Rank:";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(0, 0);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(38, 15);
            label1.TabIndex = 4;
            label1.Text = "label1";
            // 
            // breederButton
            // 
            breederButton.Location = new System.Drawing.Point(9, 284);
            breederButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            breederButton.Name = "breederButton";
            breederButton.Size = new System.Drawing.Size(166, 28);
            breederButton.TabIndex = 3;
            breederButton.Text = "GoTo Breeder";
            breederButton.UseVisualStyleBackColor = true;
            breederButton.Click += breederButton_Click;
            // 
            // counterButton
            // 
            counterButton.Location = new System.Drawing.Point(9, 249);
            counterButton.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            counterButton.Name = "counterButton";
            counterButton.Size = new System.Drawing.Size(166, 28);
            counterButton.TabIndex = 2;
            counterButton.Text = "GoTo Race Counter";
            counterButton.UseVisualStyleBackColor = true;
            counterButton.Click += counterButton_Click;
            // 
            // raceStart
            // 
            raceStart.Location = new System.Drawing.Point(258, 249);
            raceStart.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            raceStart.Name = "raceStart";
            raceStart.Size = new System.Drawing.Size(98, 28);
            raceStart.TabIndex = 1;
            raceStart.Text = "Start";
            raceStart.UseVisualStyleBackColor = true;
            raceStart.Click += raceStart_Click;
            // 
            // pgChocoRacer
            // 
            pgChocoRacer.Location = new System.Drawing.Point(9, 7);
            pgChocoRacer.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            pgChocoRacer.Name = "pgChocoRacer";
            pgChocoRacer.Size = new System.Drawing.Size(346, 235);
            pgChocoRacer.TabIndex = 0;
            // 
            // bindingSourceInventory
            // 
            bindingSourceInventory.CurrentChanged += bindingSourceInventory_CurrentChanged;
            // 
            // Utilities
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(606, 444);
            Controls.Add(tabControl1);
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Text = "Utilities";
            Load += Utilities_Load;
            tabControl1.ResumeLayout(false);
            tabRetainers.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabMateria.ResumeLayout(false);
            tabControlMateria.ResumeLayout(false);
            tabPageRemove.ResumeLayout(false);
            groupBoxRemoveFilter.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            tabAffix.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)bindingSourceAffix).EndInit();
            tabInventory.ResumeLayout(false);
            tabInventory.PerformLayout();
            tabCustom.ResumeLayout(false);
            tabGC.ResumeLayout(false);
            tabHousing.ResumeLayout(false);
            tabFC.ResumeLayout(false);
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)bindingSourceInventory).EndInit();
            ((System.ComponentModel.ISupportInitialize)bindingSourceInventoryMateria).EndInit();
            ResumeLayout(false);
        }

        private System.Windows.Forms.Label label2;

        private System.Windows.Forms.ComboBox filterCb;

        private System.Windows.Forms.GroupBox groupBoxRemoveFilter;

        private System.Windows.Forms.Button buttonAffix;

        private System.Windows.Forms.BindingSource bindingSourceInventoryMateria;

        private System.Windows.Forms.ComboBox MateriaCb1;
        private System.Windows.Forms.ComboBox MateriaCb2;
        private System.Windows.Forms.ComboBox MateriaCb3;
        private System.Windows.Forms.ComboBox MateriaCb4;
        private System.Windows.Forms.ComboBox MateriaCb5;

        private System.Windows.Forms.ComboBox affixCb;
        private System.Windows.Forms.Button button3;

        private System.Windows.Forms.ComboBox comboBox1;

        private System.Windows.Forms.TabPage tabAffix;
        private System.Windows.Forms.GroupBox groupBox2;

        private System.Windows.Forms.TabControl tabControlMateria;


        private System.Windows.Forms.PropertyGrid pgHunts;
        private System.Windows.Forms.Button btnHuntStart;

        private System.Windows.Forms.BindingSource bindingSourceInventory;

        private System.Windows.Forms.BindingSource bindingSourceAffix;

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabMateria;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox itemCb;
        private System.Windows.Forms.ListBox materiaListBox;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnRemoveMateria;
        private System.Windows.Forms.TabPage tabInventory;
        private System.Windows.Forms.Button btnReduce;
        private System.Windows.Forms.Button btnExtract;
        private System.Windows.Forms.Button btnCoffers;
        private System.Windows.Forms.TabPage tabCustom;
        private System.Windows.Forms.TabPage tabGC;
        private System.Windows.Forms.TabPage tabHousing;
        private System.Windows.Forms.TabPage tabPageRemove;

        private System.Windows.Forms.PropertyGrid pgCustomDeliveries;
        private System.Windows.Forms.PropertyGrid propertyGrid1;
        private System.Windows.Forms.Button btnCustomDeliveries;
        private System.Windows.Forms.Button btnGcTurin;
        private System.Windows.Forms.Button btnHousing;
        private System.Windows.Forms.TabPage tabRetainers;
        private System.Windows.Forms.Button btnRetainers;
        private System.Windows.Forms.PropertyGrid pgRetainers;
        private System.Windows.Forms.TabPage tabFC;
        private System.Windows.Forms.Button btnFCWorkshop;
        private System.Windows.Forms.Label lblDesynth;
        private System.Windows.Forms.PropertyGrid pgInventory;
        private System.Windows.Forms.Button btnDesynth;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.PropertyGrid pgChocoRacer;
        private System.Windows.Forms.Button raceStart;
        private System.Windows.Forms.Button breederButton;
        private System.Windows.Forms.Button counterButton;
        private System.Windows.Forms.Label rankLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label expLabel;
    }
}