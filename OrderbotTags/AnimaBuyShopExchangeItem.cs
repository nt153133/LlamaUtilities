using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Behavior;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.NeoProfiles;
using ff14bot.RemoteWindows;
using LlamaLibrary.Helpers;
using LlamaLibrary.Logging;
using LlamaLibrary.RemoteWindows;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags;

[XmlElement("AnimaBuyShopExchangeItem")]
public class AnimaBuyShopExchangeItem : LLProfileBehavior
{
    // This is a version of the BuyShopExchangeItem that allows for 2 SelectStrings
    private bool _isDone;

    private static readonly string NameValue = "Anima Weapons";
    private static readonly LLogger Log = new(NameValue, Colors.MediumPurple);
    public override bool IsDone => _isDone;

    [XmlAttribute("NpcId")]
    public int NpcId { get; set; }

    [XmlAttribute("ItemId")]
    public int ItemId { get; set; }

    [XmlAttribute("SelectString1")]
    public int SelectString1 { get; set; }

    [XmlAttribute("SelectString2")]
    public int SelectString2 { get; set; }

    [XmlAttribute("Count")]
    [XmlAttribute("count")]
    [DefaultValue(1)]
    public int Count { get; set; }

    [XmlAttribute("Dialog")]
    [XmlAttribute("dialog")]
    [DefaultValue(false)]
    public bool Dialog { get; set; } = false;

    public override bool HighPriority => true;

    public AnimaBuyShopExchangeItem() : base()
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
        return new ActionRunCoroutine(r => BuyItem(ItemId, NpcId, Count, SelectString1, SelectString2, Dialog));
    }

    private async Task BuyItem(int itemId, int npcId, int count, int selectString1, int selectString2, bool dialog)
    {
        var unit = GameObjectManager.GetObjectsByNPCId((uint) npcId).OrderBy(r => r.Distance()).FirstOrDefault();

        if (unit == null)
        {
            _isDone = true;
            return;
        }

        if (!ShopExchangeItem.Instance.IsOpen && unit.Location.Distance(Core.Me.Location) > 4f)
        {
            await Navigation.OffMeshMove(unit.Location);
            await Coroutine.Sleep(500);
        }

        unit.Interact();

        if (dialog)
        {
            await Coroutine.Wait(5000, () => Talk.DialogOpen);

            while (Talk.DialogOpen)
            {
                Talk.Next();
                await Coroutine.Sleep(1000);
            }
        }

        await Coroutine.Wait(5000, () => ShopExchangeItem.Instance.IsOpen || SelectIconString.IsOpen);

        if (SelectIconString.IsOpen)
        {
            Conversation.SelectLine((uint) selectString1);

            await Coroutine.Wait(5000, () => ShopExchangeItem.Instance.IsOpen || SelectString.IsOpen);

            if (SelectString.IsOpen)
            {
                Conversation.SelectLine((uint) selectString2);

                if (dialog)
                {
                    await Coroutine.Wait(5000, () => Talk.DialogOpen);

                    while (Talk.DialogOpen)
                    {
                        Talk.Next();
                        await Coroutine.Sleep(1000);
                    }
                }

                await Coroutine.Wait(5000, () => ShopExchangeItem.Instance.IsOpen);

                if (ShopExchangeItem.Instance.IsOpen)
                {
                    //Log.Information("ShopExchangeItem opened");
                    await ShopExchangeItem.Instance.Purchase((uint) itemId, (uint) count);
                }

                await Coroutine.Wait(2000, () => ShopExchangeItem.Instance.IsOpen);
                if (ShopExchangeItem.Instance.IsOpen)
                {
                    await Coroutine.Sleep(1000);
                    ShopExchangeItem.Instance.Close();
                }
                await Coroutine.Wait(2000, () => SelectString.IsOpen);
                if (SelectString.IsOpen)
                {
                    await Coroutine.Sleep(1000);
                    SelectString.ClickSlot(7);
                }
            }
        }
        else if (ShopExchangeItem.Instance.IsOpen)
        {
            await ShopExchangeItem.Instance.Purchase((uint) itemId, (uint) count);
        }

        await GeneralFunctions.StopBusy();

        _isDone = true;
    }
}