# DBFilesClient.NET [![NuGet](https://img.shields.io/nuget/v/Nuget.Core.svg)](https://nuget.org/packages/DBFilesClient.NET/)
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
File name                        Time to load        Record count   Expected   OK
---------------------------------------------------------------------------------
Spell                            00:00:00.8905325    158280         158280     OK
SpellEffect                      00:00:00.6427693    235970         235970     OK
Item-sparse                      00:00:00.7340361    93432          93432      OK
SoundKit                         00:00:00.2466342    66871          66871      OK
ItemSearchName                   00:00:00.2603996    76339          76339      OK
CreatureDisplayInfo              00:00:00.1517513    62121          62121      OK
SpellXSpellVisual                00:00:00.1608694    97038          97038      OK
CriteriaTree                     00:00:00.1630648    42868          42868      OK
TaxiPathNode                     00:00:00.1258343    69861          69861      OK
WMOAreaTable                     00:00:00.1784469    37874          37874      OK
ItemModifiedAppearance           00:00:00.1325420    79573          79573      OK
SpellMisc                        00:00:00.3252460    159202         159202     OK
SpellInterrupts                  00:00:00.1456599    46785          46785      OK
ItemSpecOverride                 00:00:00.0775243    55581          55581      OK
SpellCategories                  00:00:00.0764223    39087          39087      OK
Item                             00:00:00.1168999    117277         117277     OK
SpellCastTimes                   00:00:00.0548959    129            129        OK
SpellItemEnchantmentCondition    00:00:00.0564202    0              0          OK
```

## Reference

File specs for the various flavors of DB2 and DBC files can be found [here](http://wowdev.wiki/DBC).

## Thanks

In no particuliar order:
- #modcraft on QuakeNet.
- Kevin Montrose for [Sigil](https://github.com/kevin-montrose/Sigil) (up to version 1.0.0.2).
