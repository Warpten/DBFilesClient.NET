using DBFilesClient.NET.UnitTests.WDBC.Structures;
using DBFilesClient2.NET;
using DBFilesClient2.NET.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace DBFilesClient.NET.UnitTests.WDBC
{
    [TestClass]
    public class WDBC
    {
        [TestMethod]
        public void WDBCs() => TestHelper.TestNamespaceMembers<WDBC>("WDBC");

        [TestMethod]
        public void Achievement() => TestHelper.TestStructure<int, AchievementEntry>("WDBC", 100, true);

        [TestMethod]
        public void FactionTemplate() => TestHelper.TestStructure<int, FactionTemplateEntry>("WDBC", 100, true);

        [TestMethod]
        public void WorldSafeLocs() => TestHelper.TestStructure<int, WorldSafeLocsEntry>("WDBC", 100, true);
    }
}
