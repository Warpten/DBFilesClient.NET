using System;
using System.Diagnostics;
using System.Reflection;
using DBFilesClient.NET;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class ReaderTest
    {
        [TestMethod]
        public void TestDB5()
        {
            Console.WriteLine("File name                        Time to load        Record count");
            Console.WriteLine("-----------------------------------------------------------------");

            foreach (var type in Assembly.GetAssembly(typeof (ReaderTest)).GetTypes())
            {
                if (!type.IsClass)
                    continue;

                var attr = type.GetCustomAttribute<DBFileNameAttribute>();
                if (attr == null)
                    continue;

                var instanceType = typeof (Storage<>).MakeGenericType(type);

                var countGetter = instanceType.GetProperty("Count").GetGetMethod();
                var stopwatch = Stopwatch.StartNew();
                var instance = Activator.CreateInstance(instanceType, $@"D:\DataDir\22045\dbc\frFR\{attr.FileName}.db2");
                stopwatch.Stop();

                var recordCount = (int)countGetter.Invoke(instance, new object[] {});

                Console.WriteLine("{0}{1}{2}",
                    attr.FileName.PadRight(33), stopwatch.Elapsed.ToString().PadRight(20), recordCount);
            }
        }
    }
}
