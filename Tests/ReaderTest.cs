using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using DBFilesClient.NET;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tests.Structures;

namespace Tests
{
    [TestClass]
    public class ReaderTest
    {
        [TestMethod]
        public void Load()
        {
            Console.WriteLine("File name                        Average time to load     Minimum time       Maximum time       Record count");
            Console.WriteLine("------------------------------------------------------------------------------------------------------------");

            foreach (var type in Assembly.GetAssembly(typeof(ReaderTest)).GetTypes())
            {
                if (!type.IsClass)
                    continue;

                var attr = type.GetCustomAttribute<DBFileNameAttribute>();
                if (attr == null)
                    continue;

                if (attr.FileName != "CreatureDisplayInfo")
                    continue;

                var times = new List<long>();
                var recordCount = 0;
                for (var i = 1; i <= 10; ++i)
                {
                    var instanceType = typeof (Storage<>).MakeGenericType(type);

                    var countGetter = instanceType.GetProperty("Count").GetGetMethod();
                    var stopwatch = Stopwatch.StartNew();
                    var instance = Activator.CreateInstance(instanceType,
                        $@"C:\Users\verto\Desktop\{attr.FileName}.db2", true);
                    stopwatch.Stop();

                    times.Add(stopwatch.ElapsedTicks);

                    if (recordCount == 0)
                        recordCount = (int)countGetter.Invoke(instance, new object[] { });
                }

                Console.WriteLine("{0}{1}{2}{3}{4}",
                    attr.FileName.PadRight(33),
                    TimeSpan.FromTicks((long)times.Average()).ToString().PadRight(25), TimeSpan.FromTicks(times.Min()).ToString().PadRight(19), TimeSpan.FromTicks(times.Max()).ToString().PadRight(19),
                    recordCount);
            }
        }

        [TestMethod]
        public void CreatureDisplayInfo()
        {
            var storage = new Storage<CreatureDisplayInfoEntry>(@"C:\Users\verto\Desktop\CreatureDisplayInfo.db2");
            var enumerator = storage.GetEnumerator();

            /*for (var i = 0; i < 10; ++i)
            {
                if (!enumerator.MoveNext())
                    break;

                PrintRecord(enumerator.Current.Key, enumerator.Current.Value);
            }*/

            PrintRecord(58248, storage[58248]);
            Console.WriteLine("Fuck you, mate");
        }

        [TestMethod]
        public void Spell()
        {
            var storage = new Storage<SpellEntry>(@"D:\DataDir\22566\dbc\frFR\Spell.db2");
            var enumerator = storage.GetEnumerator();

            for (var i = 0; i < 10; ++i)
            {
                if (!enumerator.MoveNext())
                    break;

                PrintRecord(enumerator.Current.Key, enumerator.Current.Value);
            }
        }

        [TestMethod]
        public void Map()
        {
            var storage = new Storage<MapEntry>(@"C:\Users\verto\Desktop\dbfilesclient\map.db2");
            var enumerator = storage.GetEnumerator();

            for (var i = 0; i < 10; ++i)
            {
                if (!enumerator.MoveNext())
                    break;

                PrintRecord(enumerator.Current.Key, enumerator.Current.Value);
            }
        }

        [TestMethod]
        public void ItemSparse()
        {
            var storage = new Storage<ItemSparseEntry>(@"D:\DataDir\22566\dbc\frFR\Item-sparse.db2");
            var enumerator = storage.GetEnumerator();

            for (var i = 0; i < 10; ++i)
            {
                if (!enumerator.MoveNext())
                    break;

                PrintRecord(enumerator.Current.Key, enumerator.Current.Value);
            }
        }

        private static void PrintRecord<T>(int key, T instance)
        {
            foreach (var field in typeof(T).GetFields())
            {
                var value = field.GetValue(instance);

                if (field.FieldType.IsArray)
                {
                    var enumerableValue = value as IEnumerable;
                    var valueBuilder = new List<string>();

                    var valueEnumerator = enumerableValue.GetEnumerator();
                    while (valueEnumerator.MoveNext())
                        valueBuilder.Add(valueEnumerator.Current.ToString());

                    Console.WriteLine(
                        $"[{key}] {field.Name}: {{ {string.Join(" ; ", valueBuilder.ToArray())} }}");
                }
                else
                {
                    Console.WriteLine($"[{key}] {field.Name}: {value}");
                }
            }
            Console.WriteLine();
        }
    }
}
