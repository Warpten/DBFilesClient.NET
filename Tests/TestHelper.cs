using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.IO;

namespace DBFilesClient.NET.UnitTests
{
    internal static class TestHelper
    {
        public static void PrintRecord<TKey, T>(TKey key, T instance)
        {
            foreach (var field in typeof(T).GetProperties())
            {
                var value = field.GetValue(instance);
                if (field.GetCustomAttribute<StoragePresenceAttribute>()?.StoragePresence == StoragePresence.Exclude)
                    continue;

                if (field.PropertyType.IsArray)
                {
                    var enumerableValue = value as IEnumerable;
                    if (enumerableValue == null)
                        continue;

                    var valueBuilder = new List<string>();

                    var valueEnumerator = enumerableValue.GetEnumerator();
                    if (valueEnumerator == null)
                        continue;

                    while (valueEnumerator.MoveNext())
                        valueBuilder.Add(valueEnumerator.Current.ToString());

                    Console.WriteLine($"[{key}] {field.Name}: {{ {string.Join(" ; ", valueBuilder.ToArray())} }}");
                }
                else if (!IsNullOrDefault(value, field.PropertyType))
                {
                    Console.WriteLine($"[{key}] {field.Name}: {value}");
                }
            }
            Console.WriteLine();
        }

        // Handles boxed value types
        public static bool IsNullOrDefault(object @object, Type runtimeType)
        {
            if (@object == null) return true;

            if (runtimeType == null) throw new ArgumentNullException("runtimeType");

            // Handle non-null reference types.
            if (!runtimeType.IsValueType) return false;

            // Nullable, but not null
            if (Nullable.GetUnderlyingType(runtimeType) != null) return false;

            // Use CreateInstance as the most reliable way to get default value for a value type
            object defaultValue = Activator.CreateInstance(runtimeType);

            return defaultValue.Equals(@object);
        }

        public static Tuple<List<Stopwatch>, int> TestStructure<TKey, TValue>(string directory, int sampleCount, bool shouldLog)
            where TKey : struct
            where TValue : class, new()
        {
            var fileNameAttribute = typeof(TValue).GetCustomAttribute<DBFileNameAttribute>();
            var fileName = fileNameAttribute?.Filename ?? typeof(TValue).Name;

            var perfTimers = new List<Stopwatch>();
            var entryCount = 0;

            Storage<TKey, TValue> storage = null;

            var filePath = $"../../{directory}/Files/{fileName}.db2";
            if (!File.Exists(filePath))
                filePath = filePath.Replace(".db2", ".dbc");

            for (var i = 0; i < sampleCount; ++i)
            {
                var stopwatch = new Stopwatch();

                stopwatch.Start();
                storage = new Storage<TKey, TValue>(filePath, StorageOptions.Default);
                stopwatch.Stop();

                if (entryCount == 0)
                    entryCount = storage.Count;

                perfTimers.Add(stopwatch);
            }

            if (shouldLog)
            {
                var averageTime = perfTimers.Average(p => p.ElapsedMilliseconds);
                var averageSpan = TimeSpan.FromMilliseconds(averageTime);

                var bestTime = TimeSpan.FromMilliseconds(perfTimers.Min(p => p.ElapsedMilliseconds));
                var worstTime = TimeSpan.FromMilliseconds(perfTimers.Max(p => p.ElapsedMilliseconds));

                Console.WriteLine("Loaded {0} {1} entries. Minimum {2}, maximum {3}, average {4}", entryCount, typeof(TValue).Name, bestTime, worstTime, averageSpan);
                Console.WriteLine();

                PrintRecord(storage.First().Key, storage.First().Value);
                PrintRecord(storage.Last().Key, storage.Last().Value);
            }

            return Tuple.Create(perfTimers, entryCount);
        }


        public static Tuple<List<Stopwatch>, int> TestStructure<TKey, TValue>(string directory, int sampleCount, bool shouldLog, params TKey[] keys)
            where TKey : struct
            where TValue : class, new()
        {
            var fileNameAttribute = typeof(TValue).GetCustomAttribute<DBFileNameAttribute>();
            var fileName = fileNameAttribute?.Filename ?? typeof(TValue).Name;

            var perfTimers = new List<Stopwatch>();
            var entryCount = 0;

            Storage<TKey, TValue> storage = null;

            var filePath = $"../../{directory}/Files/{fileName}.db2";
            if (!File.Exists(filePath))
                filePath = filePath.Replace(".db2", ".dbc");

            for (var i = 0; i < sampleCount; ++i)
            {
                var stopwatch = new Stopwatch();

                stopwatch.Start();
                storage = new Storage<TKey, TValue>(filePath, StorageOptions.Default);
                stopwatch.Stop();

                if (entryCount == 0)
                    entryCount = storage.Count;

                perfTimers.Add(stopwatch);
            }

            if (shouldLog)
            {
                var averageTime = perfTimers.Average(p => p.ElapsedMilliseconds);
                var averageSpan = TimeSpan.FromMilliseconds(averageTime);

                var bestTime = TimeSpan.FromMilliseconds(perfTimers.Min(p => p.ElapsedMilliseconds));
                var worstTime = TimeSpan.FromMilliseconds(perfTimers.Max(p => p.ElapsedMilliseconds));

                Console.WriteLine("Loaded {0} {1} entries. Minimum {2}, maximum {3}, average {4}", entryCount, typeof(TValue).Name, bestTime, worstTime, averageSpan);
                Console.WriteLine();

                foreach (var kv in storage)
                {
                    if (keys.Contains(kv.Key))
                        PrintRecord(kv.Key, kv.Value);
                }
            }

            return Tuple.Create(perfTimers, entryCount);
        }

        public static void TestNamespaceMembers<T>(string directory)
        {
            var methodInfo = typeof(TestHelper).GetMethod("TestStructure", new[] { typeof(string), typeof(int), typeof(bool) });
            Assert.IsNotNull(methodInfo);

            Console.WriteLine("File name                        Average time to load     Time per record     Maximum time       Minimum time       Standard deviation");
            Console.WriteLine("--------------------------------------------------------------------------------------------------------------------------------------");

            var structureList = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == typeof(T).Namespace + ".Structures");
            foreach (var structure in structureList)
            {
                if (!structure.IsClass)
                    continue;

                var indexMember = structure.GetProperties(BindingFlags.Public | BindingFlags.Instance).First(p => p.GetCustomAttribute<IndexAttribute>() != null);

                var genericMethodInfo = methodInfo.MakeGenericMethod(new[] { indexMember.PropertyType, structure });
                Assert.IsNotNull(genericMethodInfo);

                var result = (Tuple<List<Stopwatch>, int>)genericMethodInfo.Invoke(null, new object[] { directory, 75, false });
                var perfTimers = result.Item1;
                var entryCount = result.Item2;

                var averageTime = TimeSpan.FromMilliseconds(perfTimers.Average(p => p.ElapsedMilliseconds));
                var sumOfDeltaSq = perfTimers.Sum(p => Math.Pow(p.ElapsedMilliseconds - averageTime.Milliseconds, 2));

                var stddev = TimeSpan.FromMilliseconds(sumOfDeltaSq / perfTimers.Count);

                var bestTime = TimeSpan.FromMilliseconds(perfTimers.Min(p => p.ElapsedMilliseconds));
                var worstTime = TimeSpan.FromMilliseconds(perfTimers.Max(p => p.ElapsedMilliseconds));

                Console.WriteLine("{0}{1}{2}{3}{4}{5}",
                    structure.Name.PadRight(33),
                    averageTime.ToString().PadRight(25),
                    ((float)(averageTime.TotalMilliseconds / entryCount)).ToString().PadRight(20),
                    worstTime.ToString().PadRight(19),
                    bestTime.ToString().PadRight(19),
                    stddev.ToString().PadRight(18));
            }
        }
    }
}
