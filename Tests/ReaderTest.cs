using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DBFilesClient.NET;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class ReaderTest
    {
        [TestMethod]
        public void TestLoad()
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

                // if (attr.FileName != "SpellEffect")
                //     continue;

                var times = new List<long>();
                var recordCount = 0;
                for (var i = 1; i <= 10; ++i)
                {
                    var instanceType = typeof(Storage<>).MakeGenericType(type);

                    var countGetter = instanceType.GetProperty("Count").GetGetMethod();
                    var stopwatch = Stopwatch.StartNew();
                    var instance = Activator.CreateInstance(instanceType,
                        $@"D:\DataDir\22566\dbc\frFR\{attr.FileName}.db2");
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
    }
}
