<a class="bmc-button" target="_blank" href="https://www.buymeacoffee.com/soACz8y"><img src="https://cdn.buymeacoffee.com/buttons/bmc-new-btn-logo.svg" alt="Buy me a coffee"><span style="margin-left:5px;font-size:28px !important;">Buy me a coffee</span></a>

# LlamaUtilities
Llama Utilities botbase for RebornBuddy along with LL orderbot tags

## Installation

### Automatic Setup

The easiest way to install LlamaLibrary is to install the [updateBuddy](https://loader.updatebuddy.net/UpdateBuddy.zip) plugin. It would be installed in the **/plugins** folder of your rebornBuddy folder as such:
```
RebornBuddy
└── Plugins
    └── updateBuddy
        ├── git2-a2bde63.dll
        ├── LibGit2Sharp.dll
        ├── Loader.cs
        └── UpdateBuddy.dll
```

It will automatically install the files into the correct folders and keep them up to date.

### Manual Setup

For those of you that don't want to use [repoBuddy](https://github.com/Zimgineering/repoBuddy) here's the manual installtion method. 

First off, make sure you remove any previous versions of LlamaLibrary you may have in the **/BotBases** folder.

Download the zip from [LlamaUtilities](https://github.com/nt153133/LlamaUtilities) and create a folder in **/BotBases** called **LlamaUtilities** and either unzip the contents of the zip into that folder, or check out using a SVN client to that folder.

## LLFate profile tag

`LLFate` runs FATEs inside the map where the tag starts and reevaluates its `While`
condition during selection and travel. Profiles remain responsible for teleporting or
routing into the intended map before invoking the tag.

```xml
<LLFate MinLevel="1"
        MaxLevel="50"
        Timeout="7200"
        While="not HasAtLeast(15168,10) and IsOnMap(135)"
        FateIds="667,666,333"
        BlacklistIds="1303"
        HuntBetweenFates="false" />
```

- `FateIds` limits selection to the listed FATE row IDs. `BlacklistIds` excludes IDs;
  the legacy `Blacklist` spelling is also accepted for profile compatibility.
- `HuntBetweenFates` is `false` by default. When enabled, `HuntRadius` (default `50`)
  limits optional non-FATE targets so downtime hunting cannot roam far from a newly
  spawned event.
- When `While` becomes false, LLFate cancels both ground and flight movement before
  returning control to the profile. It does not start a trip to an aetheryte.
- Session reporting counts a FATE when RB exposes its completion state or when LLFate
  observed the player participating in that tracked event through its disappearance.
  The latter is necessary because the client often removes the live FATE wrapper before
  a `COMPLETE` status can be sampled.
- Stale or incomplete live FATE snapshots are rejected before navigation. Leaving the
  starting map unexpectedly stops the bot rather than allowing an old destination to
  carry the character across zone boundaries.
