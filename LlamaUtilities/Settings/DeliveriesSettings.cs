using System.ComponentModel;
using System.IO;
using ff14bot.Enums;
using ff14bot.Helpers;
using LlamaLibrary.Helpers;

namespace LlamaUtilities.LlamaUtilities.Settings
{
    public class DeliveriesSettings : JsonSettings
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
    }
}