using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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

        private bool _doNitowikwe;
        [Description("Nitowikwe is the custom delivery NPC located in Shaaloani.")]
        [Category("Dawntrail")]
        [DisplayName("Nitowikwe"),Display(Order = 1)]
        public bool DoNitowikweDeliveries
        {
            get => _doNitowikwe;
            set
            {
                if (value == _doNitowikwe)
                {
                    return;
                }

                _doNitowikwe = value;
                OnPropertyChanged();
            }
        }

        private bool _doAnden;
        [Description("Anden is the custom delivery NPC located in Il Mheg.")]
        [Category("Endwalker")]
        [DisplayName("Anden"),Display(Order = 2)]
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

        private bool _doAmeliance;
        [Description("Ameliance is the custom delivery NPC located in Old Sharlayan.")]
        [Category("Endwalker")]
        [DisplayName("Ameliance"),Display(Order = 2)]
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

        private bool _doMargrat;
        [Description("Margrat is the custom delivery NPC located in Labyrinthos.")]
        [Category("Endwalker")]
        [DisplayName("Margrat"),Display(Order = 2)]
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

        private bool _doKaishirr;
        [Description("Kai-shirr is the custom delivery NPC located in Eulmore.")]
        [Category("Shadowbringers")]
        [DisplayName("Kai-shirr"),Display(Order = 4)]
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

        private bool _doMnaago;
        [Description("Mnaago is the custom delivery NPC located in Rhalgr's Reach.")]
        [Category("Stormblood")]
        [DisplayName("Mnaago"),Display(Order = 3)]
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
        [Description("Kurenai is the custom delivery NPC located in The Ruby Sea.")]
        [Category("Stormblood")]
        [DisplayName("Kurenai"),Display(Order = 3)]
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
        [Description("Adkiragh is the custom delivery NPC located in Idyllshire.")]
        [Category("Stormblood")]
        [DisplayName("Adkiragh"),Display(Order = 3)]
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

        private bool _doZhloe;
        [Description("Zhloe Aliapoh is the custom delivery NPC located in Idyllshire.")]
        [Category("Heavensward")]
        [DisplayName("Zhloe Aliapoh"),Display(Order = 5)]
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

        private bool _doEhlltou;
        [Description("Ehll Tou is the custom delivery NPC located in The Firmament.")]
        [Category("Heavensward")]
        [DisplayName("Ehll Tou"),Display(Order = 5)]
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
        [Description("Charlemend is the custom delivery NPC located in The Firmament.")]
        [Category("Heavensward")]
        [DisplayName("Charlemend"),Display(Order = 5)]
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

        [Description("Job To use")]
        [DefaultValue(ClassJobType.Carpenter)]
        [DisplayName("Job To use")]
        [Category("Misc"),Display(Order = 6)]
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

    }
}