using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.NeoProfiles;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.RemoteWindows;
using LlamaLibrary.Structs;
using Newtonsoft.Json;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    [XmlElement("SplendorousOrder")]
    public class SplendorousOrder : LLProfileBehavior
    {
        private bool _isDone;

        [XmlAttribute("ItemToCraft")]
        public int ItemID { get; set; }

        [XmlAttribute("Total")]
        public int Total { get; set; }

        [XmlAttribute("RewardItemId")]
        public int RewardItemId { get; set; }

        [XmlAttribute("Job")]
        public string Job { get; set; }

        [XmlAttribute("Fish")]
        [DefaultValue(false)]
        public bool Fish { get; set; }

        public override bool HighPriority => true;

        public override bool IsDone => _isDone;

        private int amountToOrder;

        public SplendorousOrder() : base()
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
            return new ActionRunCoroutine(r => StartSplendorousOrder());
        }

        private async Task StartSplendorousOrder()
        {
            var outList = new List<LisbethOrder>();

            if (Fish)
            {
                amountToOrder = Math.Min(Total - (int)Math.Ceiling((decimal)((ConditionParser.ItemCount((uint)RewardItemId)) / 2)), (int)(InventoryManager.FreeSlots - 4));

            }
            else
            {
                amountToOrder = Math.Min(Total - (int)Math.Ceiling((decimal)((ConditionParser.ItemCount((uint)RewardItemId)) / 3)), (int)(InventoryManager.FreeSlots - 4));
            }

            LisbethOrder order = new LisbethOrder(1, 1, ItemID, amountToOrder, Job);

            outList.Add(order);

            var lisbethOrder = JsonConvert.SerializeObject(outList, Formatting.None);

            Log.Information($"{lisbethOrder}");

            if (lisbethOrder.ToString() == "")
            {
                Log.Warning("Not Calling lisbeth.");
            }
            else
            {
                Log.Information("Calling lisbeth");
                if (!await Lisbeth.ExecuteOrders(lisbethOrder.ToString()))
                {
                    Log.Error("Lisbeth order failed, Dumping order to GCSupply.json");
                    using (var outputFile = new StreamWriter("GCSupply.json", false))
                    {
                        await outputFile.WriteAsync(lisbethOrder.ToString());
                    }
                }
                else
                {
                    Log.Information("Lisbeth order should be done");
                }
            }

            _isDone = true;
        }
    }
}