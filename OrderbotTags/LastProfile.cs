using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.NeoProfiles;
using ff14bot.Settings;
using LlamaLibrary.Extensions;
using TreeSharp;
using Action = TreeSharp.Action;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LastProfile")]
    public class LastProfile : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Path")]
        public string ProfileName { get; set; }

        public override bool IsDone => _isDone;

        public LastProfile() : base()
        {
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => LastProfileTask());
        }

        private async Task LastProfileTask()
        {
            if (CharacterSettings.Instance.LastNeoProfile == null)
            {
                Log.Error("Last profile not found. Exiting");
                _isDone = true;
            }
            Log.Information($"Loading last profile");
            NeoProfileManager.Load(CharacterSettings.Instance.LastNeoProfile, false);
            NeoProfileManager.UpdateCurrentProfileBehavior();

            _isDone = true;
        }
    }
}