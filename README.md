# DBFilesClient.NET [![NuGet version](https://badge.fury.io/nu/DBFilesClient.NET.svg)](https://badge.fury.io/nu/DBFilesClient.NET)
A blazing-fast "new" DBC/DB2 reader for World of Warcraft.
Name is totally not stolen from [LordJZ's library](http://github.com/LordJZ/DBFilesClient.NET).

## Planned features

1. Support more flavors of DB2 (WDB3, WDB4)

## Usage

As well as exposing its standard `Storage<T>` type, the library exposes a `DBFileNameAttribute`. This attribute binds a file name to a structure. Use cases include loading a lot of files through reflection. The code below, snipped off the tests library, shows how to use this attribute.

```csharp
foreach (var type in Assembly.GetAssembly(typeof (ReaderTest)).GetTypes())
{
    if (!type.IsClass)
        continue;

    var attr = type.GetCustomAttribute<DBFileNameAttribute>();
    if (attr == null)
        continue;

    var instanceType = typeof (Storage<>).MakeGenericType(type);
    var instance = Activator.CreateInstance(instanceType, $@"{attr.FileName}.db2");
    ...
}
```

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


## Performance

This section only refers to WDB5 DB2 files. 

Here are a few examples (randomly selected) of loading speeds for various files.
My work laptop has an i3-2310M CPU and 8Gb of DDR3 RAM.

Expected record count in the table below is calculated depending on field meta:
* If the file has an offset map, we count there.
* Otherwise, we use the record count in header.

We then add the amount of entries in the copy table.

> This data dates from v1.0.0.2. Later versions do not introduce any kind of speed deterioration.

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
