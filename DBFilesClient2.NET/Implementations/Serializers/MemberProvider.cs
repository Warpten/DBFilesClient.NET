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
        } = typeof(BinaryReader).GetProperty("BaseStream", BindingFlags.Public | BindingFlags.Instance);

        public static PropertyInfo BinaryReaderPosition
        {
            get;
        } = typeof(BinaryReader).GetProperty("Position", BindingFlags.Public | BindingFlags.Instance);

        public static PropertyInfo BinaryReaderBitPosition
        {
            get;
        } = typeof(BinaryReader).GetProperty("BitPosition", BindingFlags.Public | BindingFlags.Instance);

        public static PropertyInfo StreamPosition
        {
            get;
        } = typeof(Stream).GetProperty("Position", BindingFlags.Public | BindingFlags.Instance);

        public static MethodInfo ReadInt24 = typeof(BinaryReader).GetMethod("ReadInt24", Type.EmptyTypes);
        public static MethodInfo ReadUInt24 = typeof(BinaryReader).GetMethod("ReadUInt24", Type.EmptyTypes);
        public static Dictionary<TypeCode, MethodInfo> BinaryReaders { get; } = new Dictionary<TypeCode, MethodInfo> {
            { TypeCode.Int64, typeof (BinaryReader).GetMethod("ReadInt64", Type.EmptyTypes) },
            { TypeCode.Int32, typeof (BinaryReader).GetMethod("ReadInt32", Type.EmptyTypes) },
            { TypeCode.Int16, typeof (BinaryReader).GetMethod("ReadInt16", Type.EmptyTypes) },
            { TypeCode.SByte, typeof (BinaryReader).GetMethod("ReadSByte", Type.EmptyTypes) },

            { TypeCode.UInt64, typeof (BinaryReader).GetMethod("ReadUInt64", Type.EmptyTypes) },
            { TypeCode.UInt32, typeof (BinaryReader).GetMethod("ReadUInt32", Type.EmptyTypes) },
            { TypeCode.UInt16, typeof (BinaryReader).GetMethod("ReadUInt16", Type.EmptyTypes) },
            { TypeCode.Byte, typeof (BinaryReader).GetMethod("ReadByte", Type.EmptyTypes) },

            { TypeCode.Char, typeof (BinaryReader).GetMethod("ReadChar", Type.EmptyTypes) },
            { TypeCode.Single, typeof (BinaryReader).GetMethod("ReadSingle", Type.EmptyTypes) },

            { TypeCode.String, typeof (BinaryReader).GetMethod("ReadString", Type.EmptyTypes) },
        };
    }
}
