using System.ComponentModel;
using System.IO;
using ff14bot.Enums;
using ff14bot.Helpers;
using LlamaLibrary.Helpers;
using LlamaLibrary.JsonObjects;

namespace LlamaUtilities.LlamaUtilities.Settings
{
    public class DeliveriesSettings : JsonSettings<DeliveriesSettings>
    {
        public static DeliveriesSettings Instance => _settings ?? (_settings = new DeliveriesSettings());

        private static DeliveriesSettings _settings;

        private static DohClasses _job;

        public DeliveriesSettings() : base(Path.Combine(JsonHelper.UniqueCharacterSettingsDirectory, "DeliveriesSettings.json"))
        {
        }

        [Description("Job To use")]
        [DefaultValue(ClassJobType.Carpenter)]
        [DisplayName(" Job To use")]
        public DohClasses CraftingClass
        {
            get => _job;
            set
            {
                if (_job != value)
                {
                    _job = value;
                    Save();
                }
            }
        }

        private bool _doZhloe;
        [Description("Zhloe Aliapoh")]
        [DisplayName("Zhloe Aliapoh")]
        public bool DoZhloeDeliveries
        {
            get => _doZhloe;
            set
            {
                if (value == _doZhloe)
                {
                    return;
                }

                _doZhloe = value;
                OnPropertyChanged();
            }
        }

        private bool _doMnaago;
        [Description("Mnaago")]
        [DisplayName("Mnaago")]
        public bool DoMnaagoDeliveries
        {
            get => _doMnaago;
            set
            {
                if (value == _doMnaago)
                {
                    return;
                }

                _doMnaago = value;
                OnPropertyChanged();
            }
        }

        private bool _doKurenai;
        [Description("Kurenai")]
        [DisplayName("Kurenai")]
        public bool DoKurenaiDeliveries
        {
            get => _doKurenai;
            set
            {
                if (value == _doKurenai)
                {
                    return;
                }

                _doKurenai = value;
                OnPropertyChanged();
            }
        }

        private bool _doAdkiragh;
        [Description("Adkiragh")]
        [DisplayName("Adkiragh")]
        public bool DoAdkiraghDeliveries
        {
            get => _doAdkiragh;
            set
            {
                if (value == _doAdkiragh)
                {
                    return;
                }

                _doAdkiragh = value;
                OnPropertyChanged();
            }
        }

        private bool _doKaishirr;
        [Description("Kai-shirr")]
        [DisplayName("Kai-shirr")]
        public bool DoKaishirrDeliveries
        {
            get => _doKaishirr;
            set
            {
                if (value == _doKaishirr)
                {
                    return;
                }

                _doKaishirr = value;
                OnPropertyChanged();
            }
        }

        private bool _doEhlltou;
        [Description("Ehll Tou")]
        [DisplayName("Ehll Tou")]
        public bool DoEhlltouDeliveries
        {
            get => _doEhlltou;
            set
            {
                if (value == _doEhlltou)
                {
                    return;
                }

                _doEhlltou = value;
                OnPropertyChanged();
            }
        }

        private bool _doCharlemend;
        [Description("Charlemend")]
        [DisplayName("Charlemend")]
        public bool DoCharlemendDeliveries
        {
            get => _doCharlemend;
            set
            {
                if (value == _doCharlemend)
                {
                    return;
                }

                _doCharlemend = value;
                OnPropertyChanged();
            }
        }

        private bool _doAmeliance;
        [Description("Ameliance")]
        [DisplayName("Ameliance")]
        public bool DoAmelianceeliveries
        {
            get => _doAmeliance;
            set
            {
                if (value == _doAmeliance)
                {
                    return;
                }

                _doAmeliance = value;
                OnPropertyChanged();
            }
        }

        private bool _doAnden;
        [Description("Anden")]
        [DisplayName("Anden")]
        public bool DoAndenDeliveries
        {
            get => _doAnden;
            set
            {
                if (value == _doAnden)
                {
                    return;
                }

                _doAnden = value;
                OnPropertyChanged();
            }
        }

        private bool _doMargrat;
        [Description("Margrat")]
        [DisplayName("Margrat")]
        public bool DoMargratDeliveries
        {
            get => _doMargrat;
            set
            {
                if (value == _doMargrat)
                {
                    return;
                }

                _doMargrat = value;
                OnPropertyChanged();
            }
        }
    }
}