using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Managers;
using LlamaLibrary.Helpers;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("TurnInSplendorous")]
    public class TurnInSplendorous : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public TurnInSplendorous() : base() { }

        protected override void OnStart()
        {
        }

        protected override void OnDone()
        {
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(r => TurnInSplendorousTask());
        }

        private async Task TurnInSplendorousTask()
        {
            if (QuestLogManager.IsQuestCompleted(70338))
            {
                await GeneralFunctions.TurninSplendorous651Crafting();
            }
            if (QuestLogManager.IsQuestCompleted(70303))
            {
                await GeneralFunctions.TurninSplendorousCrafting();
            }
            else
            {
                await GeneralFunctions.TurninCNSplendorousCrafting();
            }

            _isDone = true;
        }
    }
}