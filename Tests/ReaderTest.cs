using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

                var attr = type.GetCustomAttribute<DBFileAttribute>();
                if (attr == null)
                    continue;

                var times = new List<long>();
                var recordCount = 0;
                for (var i = 1; i <= 10; ++i)
                {
                    var instanceType = typeof (Storage<>).MakeGenericType(type);

                    try
                    {
                        var countGetter = instanceType.GetProperty("Count").GetGetMethod();
                        var stopwatch = Stopwatch.StartNew();
                        var instance = Activator.CreateInstance(instanceType,
                            $@"..\Debug\DBFilesClient\{attr.FileName}.db2", true);
                        stopwatch.Stop();

                        times.Add(stopwatch.ElapsedTicks);

                        if (recordCount == 0)
                            recordCount = (int)countGetter.Invoke(instance, new object[] { });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                Console.WriteLine("{0}{1}{2}{3}{4}",
                    attr.FileName.PadRight(33),
                    TimeSpan.FromTicks((long)times.Average()).ToString().PadRight(25), TimeSpan.FromTicks(times.Min()).ToString().PadRight(19), TimeSpan.FromTicks(times.Max()).ToString().PadRight(19),
                    recordCount);
            }
        }

        [TestMethod]
        public void SpellXSpellVisual()
        {
            var storage = new Storage<SpellXSpellVisualEntry>(@".\DBFilesClient\SpellXSPellVisual.db2");
            Console.WriteLine("Loaded {0} records", storage.Count);
            Assert.IsTrue(storage.Count > 0);
        }

        private static void PrintRecord<T>(int key, T instance)
        {
            foreach (var field in typeof(T).GetProperties())
            {
                var value = field.GetValue(instance);

                if (field.PropertyType.IsArray)
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
