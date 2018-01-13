using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient2.NET.Implementations.Serializers
{
    /// <summary>
    /// A quick and dirty helper "class" (should rather be a namespace, but eh)
    /// that avoids multiple repetitive reflection calls in <see cref="ISerializer{TKey, TValue}"/>.
    /// </summary>
    internal static class MemberProvider
    {
        public static PropertyInfo BaseStream
        {
            get;
        } = typeof(IBinaryReader).GetProperty("BaseStream", BindingFlags.Public | BindingFlags.Instance);

        public static PropertyInfo BinaryReaderPosition
        {
            get;
        } = typeof(IBinaryReader).GetProperty("Position", BindingFlags.Public | BindingFlags.Instance);

        public static PropertyInfo BinaryReaderBitPosition
        {
            get;
        } = typeof(IBinaryReader).GetProperty("BitPosition", BindingFlags.Public | BindingFlags.Instance);

        public static PropertyInfo StreamPosition
        {
            get;
        } = typeof(Stream).GetProperty("Position", BindingFlags.Public | BindingFlags.Instance);

        public static MethodInfo ReadInt24 = typeof(IBinaryReader).GetMethod("ReadInt24", Type.EmptyTypes);
        public static MethodInfo ReadUInt24 = typeof(IBinaryReader).GetMethod("ReadUInt24", Type.EmptyTypes);
        public static Dictionary<TypeCode, MethodInfo> BinaryReaders { get; } = new Dictionary<TypeCode, MethodInfo> {
            { TypeCode.Int64, typeof (IBinaryReader).GetMethod("ReadInt64", new[] { typeof(int) }) },
            { TypeCode.Int32, typeof (IBinaryReader).GetMethod("ReadInt32", new[] { typeof(int) }) },
            { TypeCode.Int16, typeof (IBinaryReader).GetMethod("ReadInt16", new[] { typeof(int) }) },
            { TypeCode.SByte, typeof (IBinaryReader).GetMethod("ReadSByte", new[] { typeof(int) }) },

            { TypeCode.UInt64, typeof (IBinaryReader).GetMethod("ReadUInt64", new[] { typeof(int) }) },
            { TypeCode.UInt32, typeof (IBinaryReader).GetMethod("ReadUInt32", new[] { typeof(int) }) },
            { TypeCode.UInt16, typeof (IBinaryReader).GetMethod("ReadUInt16", new[] { typeof(int) }) },
            { TypeCode.Byte, typeof (IBinaryReader).GetMethod("ReadByte", new[] { typeof(int) }) },

            // { TypeCode.Char, typeof (IBinaryReader).GetMethod("ReadChar", new[] { typeof(int) }) },
            { TypeCode.Single, typeof (IBinaryReader).GetMethod("ReadSingle", new[] { typeof(int) }) },

            { TypeCode.String, typeof (IBinaryReader).GetMethod("ReadString", Type.EmptyTypes) },
        };
    }
}
