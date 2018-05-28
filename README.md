# DBFilesClient.NET [![NuGet version](https://badge.fury.io/nu/DBFilesClient.NET.svg)](https://badge.fury.io/nu/DBFilesClient.NET)

:bangbang: Support for DBFilesClient.NET has been discontinued. For an up-to-date libary, see [DBClientFiles.NET](http://github.com/Warpten/DBClientFiles.NET).

A blazing-fast DBC/DB2 reader for World of Warcraft.
Name is totally not stolen from [LordJZ's old 4.3.4 library](http://github.com/LordJZ/DBFilesClient.NET).

The library supports WDBC (Vanilla+), WDB2 (Cata+), WDB5 (WoD+), and WDB6 (Legion+) file formats.

## Warning for 7.3.0.24473 (PTR) users

Since PTR build 7.3.0.24473, the format of WDB6 files has once again changed - but this time, without a version bump. The solution the library implements to work around it is designed in an attempt to maintain backwards-compatibility with DB2 files from previous builds.
This was implemented in commit [e346d6565](https://github.com/Warpten/DBFilesClient.NET/commit/e346d65656a1b14859b27e35f14650e7b5c4210c), and while that build fixes a relatively big bug in WDB2 handling, it will **not** be deployed to NuGet at least until 7.3.0 hits live.

TL;DR: If you are not actively datamining PTR, stick to the [NuGet](https://www.nuget.org/packages/DBFilesClient.NET) package. I can only hope Blizzard pushes out WDB7 before 7.3.0 rolls out, for I have no idea how to efficiently handle this sudden change. -- 8/20/17

## Usage

Regular instantiation goes by

```csharp
var instance = new Storage<AreaTriggerEntry>("AreaTrigger.db2");
```

Note that `Storage<T>` behaves like a `Dictionary<int, T>`.

### File formats and structures

For all file formats, you should provide reference-type structures to the library. Fields (and not properties, though that is subject to changes in the future) will get loaded in the order they are defined.

Bidimentional arrays are not supported; neither are collections.

#### WDBC & WDB2

Nothing special here, just mark arrays with MarshalAsAttribute, specifying the size.

```c#
public sealed class AreaTableEntry
{
    [MarshalAs(UnManagedType.ByValArray, SizeConst = ...)]
    public uint[] Flags;
    public string ZoneName;
    public float AmbientMultiplier;
    public string AreaName;
    public ushort MapID;
    ...
}
```

#### WDB5

Due to the nature of the WDB5 format, arrays do not *always* need an explicit size defined through attributes. Size is most of the time inferred from the file.

However, the library has difficulty to determine the size of arrays at the end of the record, because every record in a WDB5 file is aligned to the size of its largest field. You **will** need to decorate your array with `MarshalAsAttribute` most of the time, but if all the fields in that file are of the same size, the attribute will be ignored.

To put it simply, decorating an array with `MarshalAsAttribute` is necessary if your structure ends with that array.

```c#
public sealed class AreaTableEntry
{
    // MarshalAsAttribute here is ignored
    [MarshalAs(UnManagedType.ByValArray, SizeConst = ...)]
    public uint[] Flags;
    public string ZoneName;
    public float AmbientMultiplier;
    public string AreaName;
    public ushort MapID;
    ...
    // Necessary 9 out of 10 times
    [MarshalAs(UnManagedType.ByValArray, SizeConst = ...)]
    public int[] Element;
}
```

#### WDB6

Everything related to WDB5 still applies. This format mostly introduces field reordering, because of the birth of a « common » block (which I will now refer to as « off-stream »). Given the added complexity this introduces, you should notice a rough 30% loading speed decrease, averaging. I will wait until I have more structures available to run proper benchmarks.

## Advanced

Starting with version 1.1.0.0, DBFilesClient.NET now allows you to use somewhat complex types in your structures declaration.
To expand on the feature, simply declare a type that inherits `IObjectType<T>`, where `T` is the value type of the underlying type on the record.

A common example usage of such a feature would be foreign keys.

```c#
public class ForeignKey<T, U> : IObjectType<U>
{
    public ForeignKey(U underlyingValue) : base(underlyingValue) { }
    
    public T Value => ...;
}
```

Which you would use like this:

```c#
public sealed class AreaTableEntry
{
    ...
    public ForeignKey<MapEntry, ushort> MapID;
    ...
}
```

With added effort, assuming you had access to some sort of representation of the [CAS file system](https://wowdev.wiki/CASC), you would be able to map a field to a BLTE file entry.

Other examples of `IObjectType<T>` usages can be found under the [test project](https://github.com/Warpten/DBFilesClient.NET/tree/master/Tests/DataTypes).

## Performance

Here are a few examples (randomly selected) of loading speeds for various files.
Expected record count in the tables below is calculated depending on field meta:
* If the file has an offset map, we count there.
* Otherwise, we use the record count in header.

We then add the amount of entries in the copy table.

#### WDB6

> Seed measurements as of commit af4909c4927d7fdec51660e9b029b13d92cfa39d
> 
> Made on an i7-7700HQ with 16 GB of RAM (SSD).

```
File name                        Average time to load     Minimum time       Maximum time       Record count
------------------------------------------------------------------------------------------------------------
CreatureDisplayInfo              00:00:00.0604927         00:00:00.0537344   00:00:00.0668479   64001
CriteriaTree                     00:00:00.0318721         00:00:00.0201606   00:00:00.0481298   45800
Item                             00:00:00.0266174         00:00:00.0201141   00:00:00.0378913   122686
ItemModifiedAppearance           00:00:00.0249730         00:00:00.0135512   00:00:00.0490496   81961
ItemSearchName                   00:00:00.0789017         00:00:00.0626334   00:00:00.1036024   79394
ItemSpecOverride                 00:00:00.0079788         00:00:00.0058986   00:00:00.0161451   38956
SoundKit                         00:00:00.0656281         00:00:00.0535618   00:00:00.0776808   74960
SpellCastTimes                   00:00:00.0019794         00:00:00.0001766   00:00:00.0179368   132
SpellCategories                  00:00:00.0084619         00:00:00.0067918   00:00:00.0110348   41938
SpellEffect                      00:00:00.4155679         00:00:00.3989355   00:00:00.4488714   256034
Spell                            00:00:00.2458318         00:00:00.2188421   00:00:00.2792958   171012
SpellInterrupts                  00:00:00.0219506         00:00:00.0127948   00:00:00.0357718   50246
SpellItemEnchantmentCondition    00:00:00.0004443         00:00:00.0003922   00:00:00.0006907   0
SpellMisc                        00:00:00.1156524         00:00:00.1025456   00:00:00.1197430   171805
SpellXSpellVisual                00:00:00.0549506         00:00:00.0424872   00:00:00.0714835   104795
TaxiPathNode                     00:00:00.0287585         00:00:00.0230101   00:00:00.0396076   71763
WMOAreaTable                     00:00:00.0181253         00:00:00.0113962   00:00:00.0277448   39087
```

#### WDB5

> Speed measurements as of commit f3d097706200a8f50b2236d3f68877e310e1f141
> 
> Measurements were made on an i3-2310M with 8 GB of DDR3 RAM (HDD).

```
File name                        Average time to load     Minimum time       Maximum time       Record count
------------------------------------------------------------------------------------------------------------
CreatureDisplayInfo              00:00:00.0254500         00:00:00.0187491   00:00:00.0368231   62608
CriteriaTree                     00:00:00.0203885         00:00:00.0119250   00:00:00.0289922   43746
Item                             00:00:00.0155547         00:00:00.0121462   00:00:00.0249945   118770
ItemModifiedAppearance           00:00:00.0152652         00:00:00.0072048   00:00:00.0218214   79494
ItemSearchName                   00:00:00.0470748         00:00:00.0370949   00:00:00.0605512   77309
Item-sparse                      00:00:00.1563485         00:00:00.1467743   00:00:00.1667554   94843
ItemSpecOverride                 00:00:00.0038236         00:00:00.0022122   00:00:00.0122970   36656
SoundKit                         00:00:00.0422319         00:00:00.0309875   00:00:00.0479300   69770
SpellCastTimes                   00:00:00.0000941         00:00:00.0000809   00:00:00.0001874   131
SpellCategories                  00:00:00.0101107         00:00:00.0040866   00:00:00.0213984   39917
SpellEffect                      00:00:00.2042076         00:00:00.1916380   00:00:00.2202517   241443
Spell                            00:00:00.1686459         00:00:00.1529934   00:00:00.2087546   161626
SpellInterrupts                  00:00:00.0139325         00:00:00.0071885   00:00:00.0253924   47680
SpellItemEnchantmentCondition    00:00:00.0000229         00:00:00.0000157   00:00:00.0000785   0
SpellMisc                        00:00:00.0602413         00:00:00.0479208   00:00:00.0774596   162447
SpellXSpellVisual                00:00:00.0181881         00:00:00.0128776   00:00:00.0328005   99374
TaxiPathNode                     00:00:00.0215235         00:00:00.0157172   00:00:00.0290410   70052
WMOAreaTable                     00:00:00.0129732         00:00:00.0076368   00:00:00.0204752   38163
```

## Reference

File specs for the various flavors of DB2 and DBC files can be found [here](http://wowdev.wiki/DBC).

## Thanks

In no particuliar order:
- #modcraft on QuakeNet.
- Kevin Montrose for [Sigil](https://github.com/kevin-montrose/Sigil) (up to version 1.0.0.2).
