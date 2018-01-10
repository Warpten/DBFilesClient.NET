using DBFilesClient.NET.UnitTests.WDB2.Structures;
using DBFilesClient2.NET;
using DBFilesClient2.NET.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient.NET.UnitTests.WDB2
{
    [TestClass]
    public class WDB2
    {
        [TestMethod]
        public void WDB2s() => TestHelper.TestNamespaceMembers<WDB2>("WDB2");

        [TestMethod]
        public void Map() => TestHelper.TestStructure<int, MapEntry>("WDB2", 100, true);

        [TestMethod]
        public void ItemSparse() => TestHelper.TestStructure<int, ItemSparseEntry>("WDB2", 100, true);

        [TestMethod]
        public void ItemExtendedCost() => TestHelper.TestStructure<int, ItemExtendedCostEntry>("WDB2", 100, true);
        
        [TestMethod]
        public void CreatureDisplayInfo() => TestHelper.TestStructure<int, CreatureDisplayInfoEntry>("WDB2", 100, true);
    }
}
