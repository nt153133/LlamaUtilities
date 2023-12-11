using System.ComponentModel;
using System.IO;
using ff14bot.Helpers;
using LlamaLibrary.Helpers;

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
        [Description("Which racing course would you like to run?")]
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
        [Description("Continually run races until the bot is stopped.")]
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

        private bool _goLeft;
        [Description("Continually press left while sprinting.")]
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
        [Description("What stamina should we use Choco Cure at?")]
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