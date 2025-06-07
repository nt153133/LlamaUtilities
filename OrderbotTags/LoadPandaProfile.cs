﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.NeoProfiles;
using ff14bot.RemoteWindows;
using LlamaLibrary.Logging;
using TreeSharp;
using static LlamaLibrary.Helpers.GeneralFunctions;

namespace LlamaUtilities.OrderbotTags;

[XmlElement("LoadPandaProfile")]
public class LoadPandaProfile : LLProfileBehavior
{
    internal static readonly string NameValue = "DomesticHelper";
    private static readonly LLogger Log = new(NameValue, Colors.MediumPurple);
    private bool _isDone;

    [XmlAttribute("ProfileName")]
    [XmlAttribute("Name")]
    public string ProfileName { get; set; }

    [XmlAttribute("QueueType")]
    [DefaultValue(0)]
    public int QueueType { get; set; }
    // Queue Type - 0 for standard, 1 for Undersized, 2 for Duty Support, 3 for Trust

    [XmlAttribute("GoToBarracks")]
    [DefaultValue(false)]
    public bool GoToBarracks { get; set; }

    [XmlAttribute("EquipGear")]
    [DefaultValue(false)]
    public bool EquipGear { get; set; }

    [XmlAttribute("SayHello")]
    [DefaultValue(false)]
    public bool SayHello { get; set; }

    [XmlAttribute("SayHelloCustom")]
    [DefaultValue(false)]
    public bool SayHelloCustom { get; set; }

    [XmlAttribute("SayHelloMessages")]
    [DefaultValue("hi/hiya")]
    public string SayHelloMessages { get; set; }

    public override bool IsDone => _isDone;

    public override bool HighPriority => true;

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
        return new ActionRunCoroutine(r => LoadServerProfileByNameTask());
    }

    private async Task LoadServerProfileByNameTask()
    {

        if (EquipGear)
        {
            await InventoryEquipBest(false, true);
        }
        await LlamaLibrary.Helpers.LoadServerProfile.LoadProfile(ProfileName, QueueType, GoToBarracks, SayHello, SayHelloCustom, SayHelloMessages);

    }

}