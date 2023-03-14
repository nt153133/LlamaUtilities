//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.Utilities.Helpers;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.BotBases;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using LlamaLibrary.Helpers;
using TreeSharp;
using Action = TreeSharp.Action;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("GatheringSkillOrder")]
    public class GatheringSkillOrder
    {
        [DefaultValue(false)]
        [XmlAttribute("AllOrNone")]
        public bool AllOrNone { get; set; }

        [DefaultValue(null)]
        [XmlElement(XmlEngine.GENERIC_BODY)]
        public List<GatheringSkill> GatheringSkills { get; set; }
    }

    [XmlElement("GatheringSkill")]
    public class GatheringSkill
    {
        [DefaultValue(1)]
        [XmlAttribute("TimesToCast")]
        public int TimesToCast { get; set; }

        [DefaultValue("")]
        [XmlAttribute("SpellName")]
        public string SpellName { get; set; }

        [XmlAttribute("SpellId")]
        public uint SpellId { get; set; }
    }

    [XmlElement("LLGather")]
    public class LLGather : LLProfileBehavior
    {
        #region Overrides of ProfileBehavior

        public override bool IsDone
        {
            get
            {
                //if (_isdone)
                //    return true;

                //Logging.Write("IsDone {0}",HotSpots[0]);

                if (GatheringManager.WindowOpen)
                    return false;

                if (Loops > 0 && runs >= Loops)
                {
                    Logging.Write("[GatherTag] Reached desired number of loops.");
                    return true;
                }

                if (GetCondition() != null)
                {
                    return !GetCondition()();
                }

                return false;
            }
        }

        #endregion

        protected Func<bool> Condition;

        private Func<bool> GetCondition()
        {
            try
            {
                if (Condition == null)
                {
                    if (!String.IsNullOrWhiteSpace(WhileCondition))
                    {
                        Condition = ScriptManager.GetCondition(WhileCondition);
                    }
                }

                return Condition;
            }
            catch (Exception ex)
            {
                Logging.WriteDiagnostic(ScriptManager.FormatSyntaxErrorException(ex));
                TreeRoot.Stop("Unable to compile condition for GatherTag!");
                throw;
            }
        }

        #region XML

        [XmlAttribute("while")]
        public string WhileCondition { get; set; }

        [DefaultValue(3f)]
        [XmlAttribute("InteractDistance")]
        public float InteractDistance { get; set; }

        [XmlElement("HotSpots")]
        public List<HotSpot> HotSpots { get; set; }

        [DefaultValue(null)]
        [XmlElement("BlackSpots")]
        public List<BlackSpot> BlackSpots { get; set; }

        [XmlElement("GatherObject")]
        public string GatherObject { get; set; }

        [XmlElement("ItemNames")]
        public List<string> ItemNames { get; set; }

        [XmlAttribute("Loops")]
        [DefaultValue(-1)]
        public int Loops { get; set; }

        [DefaultValue(-1)]
        [XmlElement("Slot")]
        public int Slot { get; set; }

        [XmlElement("GatheringSkillOrder")]
        public GatheringSkillOrder SkillOrder { get; set; }

        #endregion

        protected override Composite CreateBehavior()
        {
            return new PrioritySelector(MaxSkillCheck, PickOre);
        }

        protected override void OnResetCachedDone()
        {
            //Logging.Write("ResetCachedDone  {0}", HotSpots[0]);
            runs = -1;
            _isdone = false;
        }

        private bool _isdone;
        private Composite gg;
        private CircularQueue<HotSpot> quedSpots = new CircularQueue<HotSpot>();
        private HotSpot starting;
        private int runs = -1;
        private Dictionary<string, int> spellCastDictionary = new Dictionary<string, int>();
        private Dictionary<string, int> desiredspellCastDictionary = new Dictionary<string, int>();

        protected override void OnStart()
        {
            //Logging.Write("OnStart  {0}", HotSpots[0]);
            gg = MovetoOre;
            runs = -1;
            _revealSpell = null;
            movetoVector = Vector3.Zero;
            castblock = false;

            if (BlackSpots == null)
                BlackSpots = new List<BlackSpot>();

            if (SkillOrder == null)
                SkillOrder = new GatheringSkillOrder();

            GatherSpells.Clear();
            spellCastDictionary.Clear();
            desiredspellCastDictionary.Clear();
            quedSpots.Clear();

            foreach (var bundle in _GatherSpells)
            {
                foreach (var items in bundle.Value)
                {
                    GatherSpells[items] = bundle.Key;
                }
            }

            if (SkillOrder != null && SkillOrder.GatheringSkills != null)
            {
                GPSum = 0;
                foreach (var skill in SkillOrder.GatheringSkills)
                {
                    SpellData data;

                    if (skill.SpellId > 0)
                    {
                        if (ActionManager.CurrentActions.TryGetValue(skill.SpellId, out data))
                        {
                            GPSum += data.Cost * (skill.TimesToCast);
                            spellCastDictionary[data.Name] = 0;
                            desiredspellCastDictionary[data.Name] = skill.TimesToCast;
                        }
                    }
                    else
                    {
                        if (ActionManager.CurrentActions.TryGetValue(skill.SpellName, out data))
                        {
                            GPSum += data.Cost * (skill.TimesToCast);
                            spellCastDictionary[data.Name] = 0;
                            desiredspellCastDictionary[data.Name] = skill.TimesToCast;
                        }
                    }
                }

                if (Core.Player.MaxGP < GPSum && SkillOrder.AllOrNone)
                {
                    Logging.Write(Colors.Red, @"Player GP too low to use all actions, no actions will be used!");
                }
            }

            starting = HotSpots[0];
            foreach (var spot in HotSpots)
                quedSpots.Enqueue(spot);
            TreeHooks.Instance.AddHook("PoiAction", gg);
        }

        protected override void OnDone()
        {
            //Logging.Write("OnDone  {0}", HotSpots[0]);
            _isdone = true;
            TreeHooks.Instance.RemoveHook("PoiAction", gg);
        }

        private bool ShortcircuitRunback(HotSpot target)
        {
            var distance = Core.Player.Location.Distance2D(target);
            //Assume the players vision is a conservative 120yalms
            return distance <= Math.Abs(target.Radius - 120);
        }

        private string _revealSpell = null;

        private string RevealSpell
        {
            get
            {
                if (_revealSpell != null)
                    return _revealSpell;

                if (Core.Player.ClassLevel >= 46)
                {
                    if (Core.Player.CurrentJob == ClassJobType.Miner)
                        _revealSpell = "Truth of Mountains";

                    if (Core.Player.CurrentJob == ClassJobType.Botanist)
                        _revealSpell = "Truth of Forests";

                    return _revealSpell;
                }

                _revealSpell = "";
                return _revealSpell;
            }
        }

        private Composite MaxSkillCheck
        {
            get { return new ActionRunCoroutine(r => BuffRevealSkill()); }
        }

        private async Task<bool> BuffRevealSkill()
        {
            if (!string.IsNullOrEmpty(RevealSpell) && !Core.Player.HasAura(RevealSpell))
            {
                if (Core.Player.IsMounted)
                {
                    Logging.Write("Dismounting to buff {0}", RevealSpell);
                    while (Core.Player.IsMounted)
                    {
                        ActionManager.Dismount();
                        if (await Coroutine.Wait(5000, () => !Core.Player.IsMounted))
                        {
                            await Coroutine.Sleep(500);
                            break;
                        }
                    }
                }

                if (!Core.Player.HasAura(RevealSpell))
                {
                    Logging.Write("Buffing {0}", RevealSpell);
                    while (!Core.Player.HasAura(RevealSpell))
                    {
                        ActionManager.DoAction(RevealSpell, null);
                        if (await Coroutine.Wait(5000, () => Core.Player.HasAura(RevealSpell)))
                        {
                            await Coroutine.Sleep(500);
                            break;
                        }
                    }
                }

                return true;
            }

            return false;
        }

        private Composite PickOre
        {
            get
            {
                return new PrioritySelector(new Decorator(r => movetoVector != Vector3.Zero,
                                                          new PrioritySelector(
                                                                               //new Decorator(r => movetoVector.Distance2D(Core.Player.Location) > 20 , CommonBehaviors.CreateMountBehavior()),
                                                                               CommonBehaviors.CreateMountBehavior(r => movetoVector),
                                                                               CommonBehaviors.MoveAndStop(z => movetoVector, 2f, true, "Moving to HotSpot"),
                                                                               new Decorator(r => movetoVector.Distance2D(Core.Player.Location) < 2, new Action(r => movetoVector = Vector3.Zero)))),
                                            new Decorator(r => Poi.Current.Unit == null || ((!Poi.Current.Unit.IsValid || !Poi.Current.Unit.IsVisible) && !GatheringManager.WindowOpen), new Action(r => ChooseNewOre())));
            }
        }

        private Vector3 movetoVector = Vector3.Zero;
        private WaitTimer herpderp = new WaitTimer(new TimeSpan(0, 0, 0, 2, 200));

        private RunStatus ChooseNewOre()
        {
            if (GatheringManager.WindowOpen)
                return RunStatus.Success;

            if (movetoVector == Vector3.Zero)
            {
                //Restart the timer if it was fired more then 5 seconds ago
                if (herpderp.IsFinished && DateTime.Now.Subtract(herpderp.EndTime).Seconds > 5)
                {
                    herpderp.Reset();
                    return RunStatus.Success;
                }

                //if timer is running
                if (!herpderp.IsFinished)
                    return RunStatus.Success;
            }

            List<GatheringPointObject> objs;
            ResetDics();

            if (HotSpots != null && HotSpots.Any())
            {
                objs = GameObjectManager.GetObjectsOfType<GatheringPointObject>()
                    .Where(r => (GatherObject == r.EnglishName || GatherObject == r.Name) && r.IsVisible && !Blacklist.Contains(r) && !BlackSpots.Any(z => z.WithinBlackSpot2D(r.Location)) && HotSpots.Any(z => z.WithinHotSpot2D(r.Location)))
                    .OrderBy(r => r.Distance2D()).ToList();
            }
            else
            {
                throw new Exception("Setup some hotspots please.");
            }

            castblock = false;

            var targets = objs.ToArray();
            if (targets.Any())
            {
                movetoVector = Vector3.Zero;
                var target = targets.FirstOrDefault();
                Poi.Current = new Poi(target, PoiType.Gather);
            }
            else
            {
                if (movetoVector == Vector3.Zero)
                {
                    Dequeue:
                    var nextToVisit = quedSpots.Dequeue();
                    if (nextToVisit == starting)
                        runs++;

                    if (ShortcircuitRunback(nextToVisit))
                    {
                        //Logging.Write("ShortcircuitRunback");
                        return RunStatus.Success;
                    }

                    movetoVector = nextToVisit;
                    //Poi.Current = new Poi(nextToVisit,PoiType.Hotspot);
                }

                return RunStatus.Success;
            }

            return RunStatus.Failure;
        }

        private void GatherPreferedItems()
        {
            //We are there, lets blacklist it so we dont select it again once we are done
            if (!Blacklist.Contains(Poi.Current.Unit))
            {
                Blacklist.Add(Poi.Current.Unit, BlacklistFlags.All, new TimeSpan(0, 0, 0, 30), "So we don't auto target");
            }

            if (Slot != -1)
            {
                var item = GatheringManager.GetGatheringItemByIndex((uint)Slot);
                if (item != null)
                {
                    item.GatherItem();
                }
            }
            else
            {
                var windowItems = GatheringManager.GatheringWindowItems.ToArray();
                GatheringItem firstItem = null;
                //GatheringItem firstItem = ItemNames.Select(itemname => windowItems.FirstOrDefault(r => r.ItemData.EnglishName == itemname || r.ItemData.CurrentLocaleName == itemname)).FirstOrDefault(desired => desired != null && (!desired.ItemData.Unique || desired.ItemData.ItemCount() == 0));
                foreach (var itemName in ItemNames)
                {
                    var item = windowItems.FirstOrDefault(r => r.ItemData != null && r.ItemData.EnglishName == itemName || r.ItemData.CurrentLocaleName == itemName);
                    if (item != null)
                    {
                        if ((!item.ItemData.Unique || item.ItemData.ItemCount() == 0))
                        {
                            firstItem = item;
                            break;
                        }
                    }
                }

                if (firstItem != null)
                {
                    firstItem.GatherItem();
                }
                else
                {
                    if (ItemNames.Any())
                    {
                        //Only display this when we have a list of items we care about
                        Logging.Write("Couldnt Find desired item...picking first one...");
                    }

                    var item = GatheringManager.GatheringWindowItems.FirstOrDefault();
                    if (item != null)
                        item.GatherItem();
                }
            }
        }

        private Composite MovetoOre
        {
            get
            {
                return new Decorator(r => Poi.Current.Type == PoiType.Gather,
                                     new PrioritySelector(new Decorator(r => (!Poi.Current.Unit.IsValid || !Poi.Current.Unit.IsVisible), new Action(r => Poi.Clear("Node is gone"))),
                                                          new Decorator(r => Poi.Current.Location.Distance2D(Core.Player.Location) > 20 && !GatheringManager.WindowOpen && ActionManager.IsSprintReady && !Core.Player.IsMounted &&
                                                                             !Core.Player.IsCasting && MovementManager.IsMoving,
                                                                        new Action(r => ActionManager.Sprint())),
                                                          new Decorator(r => Poi.Current.Location.Distance2D(Core.Player.Location) > InteractDistance,
                                                                        new ActionRunCoroutine(obj => Lisbeth.TravelToZones(WorldManager.ZoneId, Poi.Current.Location))),

                                                          // CommonBehaviors.MoveAndStop(r => Poi.Current.Location, r => InteractDistance, true, "Moving to gather"),
                                                          new Decorator(r => MovementManager.IsFlying && !MovementManager.IsDiving, new ActionRunCoroutine(r => CommonTasks.Land())),
                                                          MineIt));
            }
        }
        //new Decorator(r => Poi.Current.Location.Distance2D(Core.Player.Location) > 3, new Action(r=>Navigator.MoveToPointWithin(Poi.Current.Location,5f,"Moving to gather"))),

        #region skill lists

        private bool castblock;
        private long GPSum;

        private bool shouldpause
        {
            get
            {
                //We cant cast when we open the window or after a swing for a short peroid.
                var spell = ActionToUseData;

                if (spell == null)
                    return false;

                return GatheringManager.ShouldPause(spell);
            }
        }

        private SkillBrand? BonusActionSetting
        {
            get
            {
                SkillBrand setting;
                var spell = ActionToUse;
                if (string.IsNullOrEmpty(spell))
                    return null;

                GatherSpells.TryGetValue(spell, out setting);
                return setting;
            }
        }

        private RunStatus HandleBonus
        {
            get
            {
                var setting = BonusActionSetting;
                if (setting.HasValue)
                {
                    SkillBrand current = setting.Value;
                    bool cast = false;

                    //if (Core.Player.HasAura(220) && current == "More High Quality Items")

                    if (FreshNodeSkills.Contains(current))
                    {
                        if (GatheringManager.SwingsRemaining == GatheringManager.MaxSwings)
                            cast = true;
                    }
                    else if (current == SkillBrand.MoreSwings)
                    {
                        if (GatheringManager.SwingsRemaining < GatheringManager.MaxSwings)
                            cast = true;
                    }
                    else if (BonusActionSetting == SkillBrand.GuaranteedItem)
                    {
                        if (GatheringManager.GatheringCombo >= 4)
                            cast = true;
                    }
                    else if (current != default(SkillBrand))
                    {
                        //otherwise just go for it
                        cast = true;
                    }

                    if (!castblock && SkillOrder.AllOrNone)
                    {
                        if (Core.Player.CurrentGP < GPSum)
                            return RunStatus.Failure;
                    }

                    if (cast)
                    {
                        var spell = ActionToUse;
                        var canGo = ActionManager.CanCast(spell, null);
                        if (canGo)
                        {
                            Log("Casting {0}", spell);
                            var result = ActionManager.DoAction(spell, null);
                            castblock = true;
                            if (result)
                            {
                                spellCastDictionary[spell]++;
                            }

                            return RunStatus.Success;
                        }
                    }
                }

                return RunStatus.Failure;
            }
        }

        private string ActionToUse
        {
            get
            {
                foreach (var item in spellCastDictionary)
                {
                    var part2 = desiredspellCastDictionary[item.Key];
                    if (item.Value < part2)
                    {
                        return item.Key;
                    }
                }

                return null;
            }
        }

        private SpellData ActionToUseData
        {
            get
            {
                var local = ActionToUse;
                if (string.IsNullOrEmpty(local))
                    return null;

                SpellData outy;
                ActionManager.CurrentActions.TryGetValue(local, out outy);
                return outy;
            }
        }

        internal enum SkillBrand
        {
            None = 0,
            MoreHq,
            MoreSwings,
            MoreItems,
            HigherGatherChance,
            BoostCrystals,
            GuaranteedItem,
            Reveal,
        }

        internal static List<SkillBrand> FreshNodeSkills = new List<SkillBrand>() { SkillBrand.Reveal, SkillBrand.BoostCrystals, SkillBrand.MoreHq, SkillBrand.HigherGatherChance };
        internal static readonly Dictionary<string, SkillBrand> GatherSpells = new Dictionary<string, SkillBrand>();

        internal static readonly Dictionary<SkillBrand, List<string>> _GatherSpells = new Dictionary<SkillBrand, List<string>>
        {
            {
                SkillBrand.MoreHq, new List<string>
                {
                    "Mind of the Mountaineer",
                    "Mind of the Pioneer",
                    "Prune II", "Prune",
                    "Dredge II", "Dredge",
                    "Unearth III", "Unearth II", "Unearth",
                    "Leaf Turn III", "Leaf Turn II", "Leaf Turn"
                }
            },
            {
                SkillBrand.MoreSwings, new List<string>
                {
                    "Solid Reason",
                    "Ageless Words"
                }
            },
            {
                SkillBrand.MoreItems, new List<string>
                {
                    "Pick Clean", //both
                    "Bountiful Harvest II", "Bountiful Harvest", //bot
                    "King's Yield II", "King's Yield", //miner
                    "Blessed Harvest II", "Blessed Harvest", //bot
                    "Bountiful Yield II", "Bountiful Yield" //miner
                }
            },
            {
                SkillBrand.HigherGatherChance, new List<string>
                {
                    "Sharp Vision III", "Sharp Vision II", "Sharp Vision",
                    "Field Mastery III", "Field Mastery II", "Field Mastery"
                }
            },
            {
                SkillBrand.BoostCrystals, new List<string>
                {
                    "The Twelve's Bounty",
                    "The Giving Land"
                }
            },
            {
                SkillBrand.GuaranteedItem, new List<string>
                {
                    "Deep Vigor",
                    "Brunt Force"
                }
            },
            {
                SkillBrand.Reveal, new List<string>
                {
                    "Toil of the Mountaineer",
                    "Toil of the Pioneer"
                }
            },
            { SkillBrand.None, new List<string>() }
        };

        private void ResetDics()
        {
            foreach (var val in spellCastDictionary.Keys.ToList())
            {
                spellCastDictionary[val] = 0;
            }
        }

        private Composite BonusAction
        {
            get
            {
                return new PrioritySelector(
                                            //http://xivdb.com/?status/220/Gathering-Fortune-Up
                                            new Decorator(r => shouldpause, new ActionAlwaysSucceed()),
                                            new Action(r => HandleBonus)
                                            //new Decorator(r => BonusActionSetting == "More Swings" && GatheringManager.SwingsRemaining < GatheringManager.MaxSwings && ActionManager.ActionUseable(DesiredSkill, null), new Action(r => ActionManager.DoAction(DesiredSkill.Name, null))),
                                            //new Decorator(r => BonusActionSetting == "Guaranteed Item" && GatheringManager.GatheringCombo >= 4 && ActionManager.ActionUseable(DesiredSkill, null), new Action(r => ActionManager.DoAction(DesiredSkill.Name, null))),
                                            //new Decorator(r => FreshNodeSkills.Contains(BonusActionSetting) && GatheringManager.SwingsRemaining == GatheringManager.MaxSwings && ActionManager.ActionUseable(DesiredSkill, null), new Action(r => ActionManager.DoAction(DesiredSkill.Name, null)))
                                           );
            }
        }

        #endregion

        private async Task<bool> Interact()
        {
            if (!GatheringManager.WindowOpen)
            {
                if (Core.Player.CurrentTarget != Poi.Current.Unit && Poi.Current.Unit.Distance2D() <= 10)
                {
                    Log("Targeting {0}", Poi.Current.Unit);
                    Poi.Current.Unit.Target();
                }

                await CommonTasks.StopAndDismount();
                int attempts = 0;

                Interact:
                Log("Interacting with {0}", Poi.Current.Unit);
                Poi.Current.Unit.Interact();

                if (await Coroutine.Wait(5000, () => GatheringManager.WindowOpen))
                {
                    await Coroutine.Sleep(1000);
                    await Coroutine.Yield();
                    return false;
                }
                else
                {
                    attempts++;
                    if (attempts == 5)
                    {
                        Blacklist.Add(Poi.Current.Unit, BlacklistFlags.All, TimeSpan.FromMinutes(30), "Couldn't interact");
                        Poi.Clear("Failture to interact");
                        return true;
                    }

                    goto Interact;
                }

                return true;
            }

            return false;
        }

        private Composite MineIt
        {
            get
            {
                return new PrioritySelector(new Decorator(r => !GatheringManager.WindowOpen, new ActionRunCoroutine(r => Interact())),
                                            BonusAction,
                                            new Decorator(r => GatheringManager.WindowOpen, new Action(r => GatherPreferedItems())));
            }
        }
    }
}