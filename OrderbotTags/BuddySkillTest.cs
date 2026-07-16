using System;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.RemoteWindows;
using TreeSharp;
using BuddyWindow = LlamaLibrary.RemoteWindows.Buddy;
using BuddySkillWindow = LlamaLibrary.RemoteWindows.BuddySkill;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("AssignBuddySkill")]
    [XmlElement("BuddySkillTest")]
    public class AssignBuddySkill : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("Role")]
        [XmlAttribute("role")]
        public string Role { get; set; } = "Attacker";

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        protected override void OnStart()
        {
            Logging.WriteDiagnostic($"[AssignBuddySkill] Preparing to assign a companion skill point to {Role}.");
        }

        protected override void OnDone()
        {
            Logging.WriteDiagnostic("[AssignBuddySkill] Finished.");
        }

        protected override void OnResetCachedDone()
        {
            _isDone = false;
        }

        protected override Composite CreateBehavior()
        {
            return new ActionRunCoroutine(_ => AssignSkill());
        }

        private async Task AssignSkill()
        {
            BuddyWindow buddy = null;

            if (!TryGetRole(Role, out var role))
            {
                Logging.WriteDiagnostic($"[AssignBuddySkill] Unknown companion skill role '{Role}'. Use Defender, Attacker, or Healer.");
                _isDone = true;
                return;
            }

            try
            {
                buddy = BuddyWindow.Instance;
                var skills = BuddySkillWindow.Instance;

                if (!buddy.IsOpen)
                {
                    Logging.WriteDiagnostic("[AssignBuddySkill] Opening companion window.");
                    await buddy.Open();
                    await Coroutine.Sleep(1000);
                }

                if (!buddy.IsOpen)
                {
                    Logging.WriteDiagnostic("[AssignBuddySkill] Could not open the companion window.");
                    return;
                }

                if (!skills.IsOpen)
                {
                    Logging.WriteDiagnostic("[AssignBuddySkill] Opening companion skills tab.");
                    buddy.OpenSkillsTab();
                    await Coroutine.Wait(5000, () => skills.IsOpen);
                }

                if (!skills.IsOpen)
                {
                    Logging.WriteDiagnostic("[AssignBuddySkill] Could not open the companion skills tab.");
                    return;
                }

                var skillPoints = skills.SkillPoints;
                var roleLevel = skills.GetRoleLevel(role);
                var nextSkillCost = skills.GetNextSkillCost(role);

                Logging.WriteDiagnostic($"[AssignBuddySkill] {role}: level {roleLevel}, available SP {skillPoints}, next skill cost {nextSkillCost}.");

                if (roleLevel >= 10)
                {
                    Logging.WriteDiagnostic($"[AssignBuddySkill] {role} is already level 10.");
                    return;
                }

                if (!skills.CanLearnNextSkill(role))
                {
                    Logging.WriteDiagnostic($"[AssignBuddySkill] Not enough companion SP to learn the next {role} skill. Need {nextSkillCost}, have {skillPoints}.");
                    return;
                }

                Logging.WriteDiagnostic($"[AssignBuddySkill] Learning the next {role} skill.");
                skills.LearnNextSkill(role);

                if (!await Coroutine.Wait(20000, IsSelectYesnoOpen))
                {
                    Logging.WriteDiagnostic("[AssignBuddySkill] Confirmation window did not appear.");
                    return;
                }

                await Coroutine.Sleep(500);
                ClickYes();
                await Coroutine.Wait(10000, () => !IsSelectYesnoOpen());
                await Coroutine.Sleep(500);

                Logging.WriteDiagnostic($"[AssignBuddySkill] {role}: level {skills.GetRoleLevel(role)}, remaining SP {skills.SkillPoints}.");
            }
            finally
            {
                if (buddy != null && buddy.IsOpen)
                {
                    Logging.WriteDiagnostic("[AssignBuddySkill] Closing companion window.");
                    buddy.Close();
                    await Coroutine.Wait(5000, () => !buddy.IsOpen);
                }

                _isDone = true;
            }
        }

        private static bool TryGetRole(string value, out BuddySkillRole role)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "defender":
                case "tank":
                    role = BuddySkillRole.Defender;
                    return true;
                case "attacker":
                case "attack":
                case "dps":
                    role = BuddySkillRole.Attacker;
                    return true;
                case "healer":
                    role = BuddySkillRole.Healer;
                    return true;
                default:
                    role = BuddySkillRole.Attacker;
                    return false;
            }
        }

        private static bool IsSelectYesnoOpen()
        {
            return SelectYesno.IsOpen || RaptureAtkUnitManager.GetWindowByName("SelectYesno") != null;
        }

        private static void ClickYes()
        {
            if (SelectYesno.IsOpen)
            {
                SelectYesno.ClickYes();
                return;
            }

            var selectYesno = RaptureAtkUnitManager.GetWindowByName("SelectYesno");
            if (selectYesno == null)
            {
                Logging.WriteDiagnostic("[AssignBuddySkill] Confirmation window closed before it could be accepted.");
                return;
            }

            selectYesno.SendAction(1, 3, 0);
        }
    }
}
