using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.Utilities;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.NeoProfiles;
using ff14bot.Objects;
using ff14bot.RemoteWindows;
using LlamaLibrary.Extensions;
using LlamaLibrary.Helpers;
using LlamaLibrary.JsonObjects.Lisbeth;
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
        public SourceType Job { get; set; }

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
                var alreadyHas = Total - (int)Math.Ceiling((decimal)(ConditionParser.ItemCount((uint)RewardItemId)));
                amountToOrder = Math.Min(alreadyHas/2, (int)(InventoryManager.FreeSlots - 5));

            }
            else
            {
                var alreadyHas = Total - (int)Math.Ceiling((decimal)(ConditionParser.ItemCount((uint)RewardItemId)));
                amountToOrder = Math.Min( alreadyHas/3, (int)(InventoryManager.FreeSlots - 5));
            }

            var orderToTest = new List<Order>();
            var orderITem = new Order()
            {
                Item = (uint) ItemID, Amount = (uint) amountToOrder, Enabled = true, Group = 1, Type = Job, Collectable = true,
            };
            orderToTest.Add(orderITem);

           var finalorder = orderToTest.GetOrderJson();

            Log.Information($"{orderToTest}");

            if (finalorder == "")
            {
                Log.Warning("Not Calling lisbeth.");
            }
            else
            {
                Log.Information("Calling lisbeth");
                if (!await Lisbeth.ExecuteOrders(finalorder))
                {
                    Log.Error("Lisbeth order failed, Dumping order to GCSupply.json");
                    using (var outputFile = new StreamWriter("GCSupply.json", false))
                    {
                        await outputFile.WriteAsync(finalorder);
                    }
                }
                else
                {
                    Log.Information("Lisbeth order should be done");
                }
            }



            var location = new Vector3(397.2991f,45.96232f,-145.3118f) ;
            var pos = location;
            while (Core.Me.Location.Distance2D(pos) > 0.5)
            {
                await CommonTasks.MoveTo(pos);
                await Coroutine.Sleep(30);
            }
            await CommonTasks.StopMoving();
            await Coroutine.Wait(-1, () => Core.Player.Location.Z < -390);

            _isDone = true;
        }
    }
}