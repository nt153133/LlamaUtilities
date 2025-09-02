using System.ComponentModel;
using System.IO;
using ff14bot.Enums;
using ff14bot.Helpers;
using LlamaLibrary.Enums;
using LlamaUtilities.LlamaUtilities.Localization;

namespace LlamaUtilities.LlamaUtilities.Settings
{
    public class RetainerSettings : JsonSettings
    {
        private static RetainerSettings _settings;

        private bool _deposit;

        private bool _depositSaddle;

        private bool _debug;

        private bool _gil;

        private bool _merge;
        private bool _role;

        private bool _category;

        private int _numOfRetainers;
        private MyItemRole _itemRole;
        private ItemUiCategory _itemCategory;
        private bool _ventures;
        private bool _loop;

        public RetainerSettings() : base(Path.Combine(CharacterSettingsDirectory, "RetainerSettings.json"))
        {
        }

        public static RetainerSettings Instance => _settings ?? (_settings = new RetainerSettings());

        [LocalizedDescriptionAttribute("RetainerSettings_DepositFromPlayerDescription")]
        [DefaultValue(true)] //shift +x
        public bool DepositFromPlayer
        {
            get => _deposit;
            set
            {
                if (_deposit != value)
                {
                    _deposit = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("RetainerSettings_DepositFromSaddleBagsDescription")]
        [DefaultValue(false)] //shift +x
        public bool DepositFromSaddleBags
        {
            get => _depositSaddle;
            set
            {
                if (_depositSaddle != value)
                {
                    _depositSaddle = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("RetainerSettings_DebugLoggingDescription")]
        [DefaultValue(false)] //shift +x
        public bool DebugLogging
        {
            get => _debug;
            set
            {
                if (_debug != value)
                {
                    _debug = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("RetainerSettings_GetGilDescription")]
        [DefaultValue(true)] //shift +x
        public bool GetGil
        {
            get => _gil;
            set
            {
                if (_gil != value)
                {
                    _gil = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("RetainerSettings_ReassignVenturesDescription")]
        [DefaultValue(true)] //shift +x
        public bool ReassignVentures
        {
            get => _ventures;
            set
            {
                if (_ventures != value)
                {
                    _ventures = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("RetainerSettings_DontOrganizeRetainersDescription")]
        [DefaultValue(false)] //shift +x
        [Browsable(false)]
        public bool DontOrganizeRetainers
        {
            get => _merge;
            set
            {
                if (_merge != value)
                {
                    _merge = value;
                    Save();
                }
            }
        }

        [LocalizedDescriptionAttribute("RetainerSettings_LoopDescription")]
        [DefaultValue(true)] //shift +x
        public bool Loop
        {
            get => _loop;
            set
            {
                if (_loop != value)
                {
                    _loop = value;
                    Save();
                }
            }
        }
    }
}