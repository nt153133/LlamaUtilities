using System.Windows.Media;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using ff14bot.Enums;
using ff14bot.Helpers;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
	[XmlElement("LLInitiateLeve")]
	public class LLInitiateLeveTag : LLProfileBehavior
	{
		private bool _done = false;

		[XmlAttribute("LeveId")]
		public int LeveId { get; set; }

		protected override void OnStart()
		{
			_done = false;
		}

        public override bool IsDone
		{
			get
			{
				return _done;
			}
		}

        protected override void OnDone()
        {
		}

		protected override Composite CreateBehavior()
		{
			return
				new Decorator(
					ret => !_done,
					new ActionRunCoroutine(r => InitiateLeve()));
         }

        protected override void OnResetCachedDone()
		{
			_done = false;
		}

		protected async Task<bool> InitiateLeve()
        {
            await GeneralFunctions.StopBusy();

            if (!LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen)
            {
                LlamaLibrary.RemoteAgents.AgentJournalDetail.Instance.Toggle();
                await Coroutine.Wait(10000, () => LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen);
                if (!LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen)
                {
                    Log.Error($"JournalDetail didn't open, trying again");
                    LlamaLibrary.RemoteAgents.AgentJournalDetail.Instance.Toggle();
                    await Coroutine.Wait(10000, () => LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen);
                }
                if (!LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen)
                {
                    Log.Error($"JournalDetail didn't open, exiting");
                    return _done = true;
                }
            }

			if (LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen)
			{
				var leves = LeveManager.Leves;
				if (leves.Length > 0)
				{
					foreach(ff14bot.Managers.LeveWork leve in leves)
					{
						if(leve.GlobalId == LeveId && leve.Step == 1)
						{
							ulong globalId = (ulong) leve.GlobalId;
                            LlamaLibrary.RemoteWindows.JournalDetail.Instance.SetQuest(globalId); //Set quest
							await Coroutine.Sleep(200);
                            Log.Information($"Activating: '{leve.Name}'");
                            LlamaLibrary.RemoteWindows.JournalDetail.Instance.InitiateLeve(globalId); //Initiate
                            if (await Coroutine.Wait(10000, () => SelectYesno.IsOpen))
							{
								SelectYesno.ClickYes();
							}

                            if (await Coroutine.Wait(10000, () => LlamaLibrary.RemoteWindows.GuildLeveDifficulty.Instance.IsOpen))
                            {
                                LlamaLibrary.RemoteWindows.GuildLeveDifficulty.Instance.Confirm();
                            }
							await Coroutine.Sleep(3000);
                        }
					}
				}
                if (LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen)
                {
                    LlamaLibrary.RemoteAgents.AgentJournalDetail.Instance.Toggle();
                    await Coroutine.Wait(10000, () => !LlamaLibrary.RemoteWindows.JournalDetail.Instance.IsOpen);
                }
			}

            return _done = true;
		}
	}
}
