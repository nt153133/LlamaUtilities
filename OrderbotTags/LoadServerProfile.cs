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
using LlamaLibrary.Extensions;
using TreeSharp;
using Action = TreeSharp.Action;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LoadServerProfile")]
    public class LoadServerProfile : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Path")]
        public string ProfileName { get; set; }

        public override bool IsDone => _isDone;

        public LoadServerProfile() : base()
        {
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => LoadServerProfileTask());
        }

        private async Task LoadServerProfileTask()
        {
            NeoProfileManager.Load(ProfileName, false);
            NeoProfileManager.UpdateCurrentProfileBehavior();

            _isDone = true;
        }
    }
}