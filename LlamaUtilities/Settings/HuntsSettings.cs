using System.ComponentModel;
using System.IO;
using ff14bot.Helpers;
using LlamaLibrary.Helpers;
using LlamaUtilities.LlamaUtilities.Localization;

namespace LlamaUtilities.LlamaUtilities.Settings
{
    public class HuntsSettings : JsonSettings
    {
        public static HuntsSettings Instance => _settings ?? (_settings = new HuntsSettings());

        private static HuntsSettings _settings;

        private bool _ARRHunts;

        private bool _ClanHunts;

        private bool _VerteranClanHunts;

        private bool _NutClanHunts;

        private bool _EwHunts;

        private bool _DtHunts;

        public HuntsSettings() : base(Path.Combine(JsonHelper.UniqueCharacterSettingsDirectory, "HuntsSettings.json"))
        {
        }

        [LocalizedDescriptionAttribute("HuntsSettings_ARRHuntsDescription")]
        [DefaultValue(false)]
        public bool ARRHunts
        {
            get => _ARRHunts;
            set
            {
                if (_ARRHunts != value)
                {
                    _ARRHunts = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("HuntsSettings_ClanHuntsDescription")]
        [DefaultValue(false)]
        public bool ClanHunts
        {
            get => _ClanHunts;
            set
            {
                if (_ClanHunts != value)
                {
                    _ClanHunts = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("HuntsSettings_VeteranClanHuntsDescription")]
        [DefaultValue(false)]
        public bool VerteranClanHunts
        {
            get => _VerteranClanHunts;
            set
            {
                if (_VerteranClanHunts != value)
                {
                    _VerteranClanHunts = value;
                    Save();
                }
            }
        }

        [Description("Complete Nut Clan Mark Daily Hunts")]
        [DefaultValue(false)]
        public bool NutClanHunts
        {
            get => _NutClanHunts;
            set
            {
                if (_NutClanHunts != value)
                {
                    _NutClanHunts = value;
                    Save();
                }
            }
        }

        [Description("Complete EW Daily Hunts")]
        [DefaultValue(false)]
        public bool EwHunts
        {
            get => _EwHunts;
            set
            {
                if (_EwHunts != value)
                {
                    _EwHunts = value;
                    Save();
                }
            }
        }

        [Description("Complete DT Daily Hunts")]
        [DefaultValue(false)]
        public bool DtHunts
        {
            get => _DtHunts;
            set
            {
                if (_DtHunts != value)
                {
                    _DtHunts = value;
                    Save();
                }
            }
        }
    }
}