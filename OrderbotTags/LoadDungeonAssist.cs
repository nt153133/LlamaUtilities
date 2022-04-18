using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("LoadDungeonAssist")]
    public class LoadDungeonAssist : LLProfileBehavior
    {
        private bool _isDone;

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        public LoadDungeonAssist() : base()
        {
        }

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
            return new ActionRunCoroutine(r => LoadDungeonAssistTask());
        }

        private async Task LoadDungeonAssistTask()
        {
            var Plugin = PluginManager.Plugins.Where(p => p.Plugin.Name == "DungeonAssist");
            var Plugin2 = PluginManager.Plugins.Where(p => p.Plugin.Name == "Osiris");
            var Plugin3 = PluginManager.Plugins.Where(p => p.Plugin.Name == "SideStep");

            var text1 = string.Empty;
            var text2 = string.Empty;
            var text3 = string.Empty;
            var text4 = string.Empty;
            var text5 = string.Empty;
            var text6 = string.Empty;

            text1 = "Plug-In Dungeon Assist is installed and enabled";
            text2 = "You Need the Dungeon Assist Plug-In to Operate this Profile";
            text3 = "Plug-In Osiris  is installed and enabled";
            text4 = "You Need the Osiris Plug-In to Operate this Profile";
            text5 = "Plug-In SideStep  is installed and enabled";
            text6 = "You Need the SideStep Plug-In to Operate this Profile";

            if (Plugin.Any())
            {
                if (Plugin != null)
                {
                    PluginManager.SetEnabledPlugins("DungeonAssist");
                    Core.OverlayManager.AddToast(() => text1,
                                                 TimeSpan.FromMilliseconds(5000),
                                                 System.Windows.Media.Color.FromRgb(29, 213, 226),
                                                 System.Windows.Media.Color.FromRgb(13, 106, 175),
                                                 new System.Windows.Media.FontFamily("Gautami"));
                }
                else
                {
                    Core.OverlayManager.AddToast(() => text2,
                                                 TimeSpan.FromMilliseconds(5000),
                                                 System.Windows.Media.Color.FromRgb(29, 213, 226),
                                                 System.Windows.Media.Color.FromRgb(13, 106, 175),
                                                 new System.Windows.Media.FontFamily("Gautami"));
                }
            }
            else
            {
                Core.OverlayManager.AddToast(() => text2, TimeSpan.FromMilliseconds(5000), System.Windows.Media.Color.FromRgb(29, 213, 226), System.Windows.Media.Color.FromRgb(13, 106, 175), new System.Windows.Media.FontFamily("Gautami"));
            }

            if (Plugin2.Any())
            {
                if (Plugin2 != null)
                {
                    PluginManager.SetEnabledPlugins("Osiris");
                    Core.OverlayManager.AddToast(() => text3,
                                                 TimeSpan.FromMilliseconds(5000),
                                                 System.Windows.Media.Color.FromRgb(29, 213, 226),
                                                 System.Windows.Media.Color.FromRgb(13, 106, 175),
                                                 new System.Windows.Media.FontFamily("Gautami"));
                }
                else
                {
                    Core.OverlayManager.AddToast(() => text4,
                                                 TimeSpan.FromMilliseconds(5000),
                                                 System.Windows.Media.Color.FromRgb(29, 213, 226),
                                                 System.Windows.Media.Color.FromRgb(13, 106, 175),
                                                 new System.Windows.Media.FontFamily("Gautami"));
                }
            }
            else
            {
                Core.OverlayManager.AddToast(() => text4, TimeSpan.FromMilliseconds(5000), System.Windows.Media.Color.FromRgb(29, 213, 226), System.Windows.Media.Color.FromRgb(13, 106, 175), new System.Windows.Media.FontFamily("Gautami"));
            }

            if (Plugin3.Any())
            {
                if (Plugin3 != null)
                {
                    PluginManager.SetEnabledPlugins("SideStep");
                    Core.OverlayManager.AddToast(() => text5,
                                                 TimeSpan.FromMilliseconds(5000),
                                                 System.Windows.Media.Color.FromRgb(29, 213, 226),
                                                 System.Windows.Media.Color.FromRgb(13, 106, 175),
                                                 new System.Windows.Media.FontFamily("Gautami"));
                }
                else
                {
                    Core.OverlayManager.AddToast(() => text6,
                                                 TimeSpan.FromMilliseconds(5000),
                                                 System.Windows.Media.Color.FromRgb(29, 213, 226),
                                                 System.Windows.Media.Color.FromRgb(13, 106, 175),
                                                 new System.Windows.Media.FontFamily("Gautami"));
                }
            }
            else
            {
                Core.OverlayManager.AddToast(() => text6, TimeSpan.FromMilliseconds(5000), System.Windows.Media.Color.FromRgb(29, 213, 226), System.Windows.Media.Color.FromRgb(13, 106, 175), new System.Windows.Media.FontFamily("Gautami"));
            }

            await Coroutine.Sleep(1000);

            _isDone = true;
        }
    }
}