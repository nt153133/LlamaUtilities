#if RB_DT
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.NeoProfiles;
using ff14bot.Objects;
using ff14bot.Pathing;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers.NPC;
using LlamaLibrary.LlamaManagers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeSharp;
using Action = TreeSharp.Action;

namespace zzi.profiles
{
    [XmlElement("PointMenu")]
    public class PointMenu : ProfileBehavior
    {
        public override bool IsDone
        {
            get
            {
                if (IsQuestComplete)
                    return true;
                if (IsStepComplete)
                    return true;
                return false;
            }
        }

        private bool DoneTalking;
        private uint TalkTargetObjectId;
        private bool dialogwasopen;
        private string QuestGiver;
        private Composite CutsceneDetection;
        protected override void OnStart()
        {
            index = 0;
            QuestGiver = DataManager.GetLocalizedNPCName(NpcId);

            CutsceneDetection = new Decorator(r => (Talk.DialogOpen || QuestLogManager.InCutscene) && !dialogwasopen, new Action(r => { dialogwasopen = true; TalkTargetObjectId = Core.Target.ObjectId; return RunStatus.Failure; }));

            TreeHooks.Instance.InsertHook("TreeStart", 0, CutsceneDetection);
        }

        protected override void OnDone()
        {
            TreeHooks.Instance.RemoveHook("TreeStart", CutsceneDetection);
        }


        [XmlAttribute("NpcId")]
        public int NpcId { get; set; }

        //[XmlAttribute("TargetIds")]
        //public uint[] TargetIds { get; set; }

        private uint index = 0;

        [XmlAttribute("InteractDistance")]
        [DefaultValue(5f)]
        public float InteractDistance { get; set; }

        [XmlAttribute("XYZ")]
        public Vector3 XYZ { get; set; }

        [XmlAttribute("InteractOptions")]
        public int[] InteractOptions { get; set; }

        public override string StatusText { get { return "PointMenu with " + QuestGiver; } }

        public GameObject NPC
        {
            get
            {
                var npc = GameObjectManager.GetObjectsByNPCId((uint)NpcId).FirstOrDefault(r => r.IsVisible && r.IsTargetable);
                return npc;
            }
        }

        private async Task<bool> moveToNpc()
        {
            var movetoParam = new MoveToParameters(XYZ, QuestGiver) { DistanceTolerance = 7f };

            var npcObject = GameObjectManager.GetObjectByNPCId((uint)NpcId);
            if (npcObject != null && npcObject.IsTargetable && npcObject.IsVisible)
            {
                movetoParam.Location = npcObject.Location;
                return await CommonTasks.MoveAndStop(movetoParam, () => npcObject.IsWithinInteractRange, $"[{GetType().Name}] Moving to {XYZ} so we can talk to {QuestGiver}");
            }

            return await CommonTasks.MoveAndStop(movetoParam, 7f, true, $"[{GetType().Name}] Moving to {XYZ} so we can talk to {QuestGiver}");
        }

        protected override Composite CreateBehavior()
        {
            var queue = new Stack<int>(InteractOptions.Reverse());
            return new PrioritySelector(
                ctx => NPC,

                new Decorator(r => Talk.DialogOpen, new Action(r => { dialogwasopen = true; TalkTargetObjectId = Core.Target.ObjectId; Talk.Next(); return RunStatus.Success; })),
                new Decorator(r => !QuestLogManager.InCutscene && SelectYesno.IsOpen, new Action(r => { SelectYesno.ClickYes(); return RunStatus.Success; })),
                new Decorator(r => PointMenuManager.Window != null && PointMenuManager.Window.IsVisible, new ActionRunCoroutine(async s => {
                        //click our target
                        var k = queue.Pop();
                        PointMenuManager.Interact((ulong)k);

                        return true;
                    })
                ),
                //new Decorator(r => dialogwasopen && (!Core.Player.HasTarget || Core.Target?.ObjectId != TalkTargetObjectId), new Action(r => { DoneTalking = true; return RunStatus.Success; })),

                //CommonBehaviors.MoveAndStop(ret => XYZ, ret => InteractDistance, true, ret => $"[{GetType().Name}] Moving to {XYZ} so we can talk to {QuestGiver}"),
                new ActionRunCoroutine(r => moveToNpc()),
            // If we're in interact range, and the NPC/Placeable isn't here... wait 30s.
                new Decorator(ret => NPC == null, new Sequence(new SucceedLogger(r => $"Waiting at {Core.Player.Location} for {QuestGiver} to spawn"), new WaitContinue(5, ret => NPC != null || QuestLogManager.InCutscene || SelectYesno.IsOpen || Talk.DialogOpen, new Action(ret => RunStatus.Failure)))),
                new Decorator(r => QuestLogManager.InCutscene, new Action(r => RunStatus.Success )),
                new Action(ret => NPC.Interact())

            );
        }

    }
}
#endif