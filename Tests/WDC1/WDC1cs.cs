using DBFilesClient.NET.UnitTests.WDC1.Structures;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DBFilesClient.NET.UnitTests.WDC1
{
    [TestClass]
    public class WDC1
    {
        [TestMethod]
        public void WDC1s() => TestHelper.TestNamespaceMembers<WDC1>("WDC1");

        [TestMethod]
        public void SpellEffect() => TestHelper.TestStructure<int, SpellEffectEntry>("WDC1", 1, true, 1, 2, 3, 8);

        [TestMethod]
        public void Map() => TestHelper.TestStructure<int, MapEntry>("WDC1", 1, true);
    }
}
