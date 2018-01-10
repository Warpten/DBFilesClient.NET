using DBFilesClient2.NET;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient.NET.UnitTests
{
    [TestClass]
    public class BinaryReaderTests
    {
        [TestMethod]
        public void TestBinaryReader()
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // 00001111 11110000 0011001100 11001100
                writer.Write(new byte[] {
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 1, 8, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 16, 0,
                });
                ms.Position = 0;

                var tmpReader = new BitReader(ms, StorageOptions.Default);
                for (var j = 0; j < 3; ++j)
                {
                    for (var i = 0; i < 10; ++i)
                        Console.Write("{0}", Convert.ToString(tmpReader.ReadByte(), 2).PadLeft(8, '0'));
                    Console.WriteLine();
                }

                Console.WriteLine("Testing");
                Console.WriteLine();

                var bitSizes = new int[] { 8, 14, 6, 2, 10, 16, 11, 10 };

                var values = new List<List<int>>();
                values.Add(new List<int> { 0, 0, 0, 0, 0, 0, 0, 0 });
                values.Add(new List<int> { 0, 0, 0, 0, 0, 0, 1, 1 });
                values.Add(new List<int> { 0, 0, 0, 0, 0, 0, 0, 2 });

                ms.Position = 0;
                using (var reader = new BitReader(ms, StorageOptions.Default))
                {
                    for (var i = 0; i < values.Count; ++i)
                    {
                        var markerBuffer = new StringBuilder();
                        for (var j = 0; j < bitSizes.Length; ++j)
                        {
                            var bitSize = bitSizes[j];
                            var expectedValue = values[i][j];

                            Console.Write("{0} ", reader.BitPosition);

                            var value = reader.ReadBits(bitSize);
                            var valueAsBits = Convert.ToString(value, 2).PadLeft(bitSize, '0');

                            var expectedValueAsBits = Convert.ToString(expectedValue, 2).PadLeft(bitSize, '0');
                            
                            //Console.Write(Convert.ToString(value, 2).PadLeft(bitSize, '0'));

                            Assert.IsTrue(value == expectedValue, $"Record #{i}: Field {j} failed. Read {value} ({valueAsBits}), expected {expectedValue} ({expectedValueAsBits})");
                            markerBuffer.Append("^".PadLeft(bitSize));
                        }
                        
                        //Console.WriteLine();
                        //Console.WriteLine(markerBuffer.ToString());
                    }
                }
            }
        }
    }
}
