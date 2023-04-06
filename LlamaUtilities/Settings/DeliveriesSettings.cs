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
    }
}