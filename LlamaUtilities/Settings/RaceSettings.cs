using System.ComponentModel;
using System.IO;
using ff14bot.Helpers;
using LlamaLibrary.Helpers;
using LlamaUtilities.LlamaUtilities.Localization;

namespace LlamaUtilities.LlamaUtilities.Settings
{
    public class RaceSettings : JsonSettings
    {
        public static RaceSettings Instance => _settings ?? (_settings = new RaceSettings());

        private static RaceSettings _settings;

        public RaceSettings() : base(Path.Combine(JsonHelper.UniqueCharacterSettingsDirectory, "RaceSettings.json"))
        {
        }

        private RaceToRun _raceToRun;
        public enum RaceToRun
        {
            Random = 1000021,
            Random_NoReward = 1000025,
            SagoliiRoad = 1000018,
            SagoliiRoad_NoReward = 1000022,
            CostadelSol = 1000019,
            CostadelSol_NoReward = 1000023,
            TranquilPaths = 1000020,
            TranquilPaths_NoReward = 1000024,
        }
        [LocalizedDescriptionAttribute("RaceSettings_RaceChoiceDescription")]
        [DefaultValue(RaceToRun.Random)]
        public RaceToRun RaceChoice
        {
            get => _raceToRun;
            set
            {
                if (_raceToRun != value)
                {
                    _raceToRun = value;
                    Save();
                }
            }
        }

        private bool _runLoop;
        [LocalizedDescriptionAttribute("RaceSettings_LoopDescription")]
        [DefaultValue(true)]
        public bool Loop
        {
            get => _runLoop;
            set
            {
                if (_runLoop != value)
                {
                    _runLoop = value;
                    Save();
                }
            }
        }

        private bool _useAbility1;
        [LocalizedDescriptionAttribute("RaceSettings_UseAbility1Description")]
        [DefaultValue(true)]
        public bool UseAbility1
        {
            get => _useAbility1;
            set
            {
                if (_useAbility1 != value)
                {
                    _useAbility1 = value;
                    Save();
                }
            }
        }

        private bool _useAbility2;
        [LocalizedDescriptionAttribute("RaceSettings_UseAbility2Description")]
        [DefaultValue(true)]
        public bool UseAbility2
        {
            get => _useAbility2;
            set
            {
                if (_useAbility2 != value)
                {
                    _useAbility2 = value;
                    Save();
                }
            }
        }

        private bool _goLeft;
        [LocalizedDescriptionAttribute("RaceSettings_GoLeftDescription")]
        [DefaultValue(false)]
        public bool GoLeft
        {
            get => _goLeft;
            set
            {
                if (_goLeft != value)
                {
                    _goLeft = value;
                    Save();
                }
            }
        }

        private int _cureStamina;
        [LocalizedDescriptionAttribute("RaceSettings_CureStaminaDescription")]
        [DefaultValue(90)]
        public int CureStamina
        {
            get => _cureStamina;
            set
            {
                if (_cureStamina != value)
                {
                    _cureStamina = value;
                    Save();
                }
            }
        }

    }
}