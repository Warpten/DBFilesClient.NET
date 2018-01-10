using DBFilesClient2.NET.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DBFilesClient2.NET.Internals
{
    [DebuggerDisplay("{ToString()}")]
    internal sealed class FieldMetadata
    {
        /// <summary>
        /// The size of this field, in bytes. This is specific to older variations: WDB(C|1|2|5|6).
        /// </summary>
        public int ByteSize { get; set; }

        public int BitSize { get; set; }

        /// <summary>
        /// Offset of this member in the record in bytes.
        /// </summary>
        public uint OffsetInRecord { get; set; }

        /// <summary>
        /// Offset of this member in the record in bytes.
        /// </summary>
        public uint BitOffsetInRecord { get; set; }

        /// <summary>
        /// The compression type for this field. For most file formats but WDC1, this is (and should be) <see cref="MemberCompression.None"/>.
        /// </summary>
        public MemberCompression Compression { get; set; } = MemberCompression.None;

        /// <summary>
        /// The size of the corresponding field, if it is an array.
        /// </summary>
        public int GuessedArraySize
        {
            set => _guessedArraySize = value;
        }

        public int GetArraySize()
        {
            // Array size is explicit here
            if (Compression == MemberCompression.BitpackedIndexedArray)
                return CompressionData.BitpackedIndexedArray.ArraySize;

            // It is also explicit here, as meta and extendedmeta work differently
            // meta gives the size of an individual field in this array, extendedmeta gives the size of the entire array in bits
            if (BitSize != 0 && ByteSize != 0)
                return (BitSize / 8) / ByteSize;

            // use the user's array size if available
            if (MemberInfo != null)
            {
                var storageAttr = MemberInfo.GetCustomAttribute<StoragePresenceAttribute>();
                if (storageAttr != null)
                    return storageAttr.SizeConst;
            }

            if (!_guessedArraySize.HasValue)
            {
                if (!MemberInfo.GetMemberType().IsArray)
                    return 1;

                throw new InvalidOperationException();
            }
            return _guessedArraySize.Value;
        }

        /// <summary>
        /// Member info of the field.
        /// </summary>
        public MemberInfo MemberInfo { get; set; }

        public CompressionData CompressionData { get; set; }

        /// <summary>
        /// The base type of this field (the type itself if flat, the element type if an array otherwise).
        /// This is a shortcut that avoid casting to PropertyInfo or MemberInfo.
        /// </summary>
        public Type BaseType
        {
            get
            {
                if (_baseType != null)
                    return _baseType;

                _baseType = MemberInfo.GetMemberType();
                if (_baseType.IsArray)
                    _baseType = _baseType.GetElementType();

                return _baseType;
            }
        }

        /// <summary>
        /// The actual type of this field (as declared in MemberInfo.DeclaringType)
        /// </summary>
        public Type Type
        {
            get
            {
                if (_regularType != null)
                    return _regularType;

                return _regularType = MemberInfo.GetMemberType();
            }
        }

        public long AdditionalDataOffset { get; set; }
        public int AdditionalDataSize { get; set; }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            switch (Compression)
            {
                case MemberCompression.None:
                    stringBuilder.Append($"/* 0x{OffsetInRecord:X3} - 0x{OffsetInRecord + ByteSize * GetArraySize():X3} */ ");
                    break;
                case MemberCompression.CommonData:
                    stringBuilder.Append($"/* CommonData ({AdditionalDataOffset}-{AdditionalDataOffset + AdditionalDataSize}) */ ");
                    break;
                case MemberCompression.Bitpacked:
                    stringBuilder.Append($"/* Bitpacked ({CompressionData.Bitpacked.Width} bits) */ ");
                    break;
                case MemberCompression.BitpackedIndexed:
                    stringBuilder.Append($"/* Pallet ({CompressionData.BitpackedIndexed.Width} bits, {AdditionalDataOffset}-{AdditionalDataOffset + AdditionalDataSize}) */ ");
                    break;
                case MemberCompression.BitpackedIndexedArray:
                    stringBuilder.Append($"/* Pallet ({CompressionData.BitpackedIndexedArray.Width} bits, {AdditionalDataOffset}-{AdditionalDataOffset + AdditionalDataSize}) */ ");
                    break;
            }

            if (BaseType != null)
                stringBuilder.Append($" {BaseType.Name}");

            if (GetArraySize() != 1 && GetArraySize() != 0)
                stringBuilder.Append($"[{GetArraySize()}]");

            if (MemberInfo != null)
                stringBuilder.Append($" {MemberInfo.Name}");
            else
                stringBuilder.Append(" UnknownField");

            return stringBuilder.ToString();
        }

        private Type _baseType, _regularType;
        private int? _guessedArraySize;
    }

    [StructLayout(LayoutKind.Explicit, Size = 12)]
    internal struct CompressionData
    {
        [StructLayout(LayoutKind.Explicit, Size = 12)]
        public struct BitpackedCompression
        {
            [FieldOffset(0)] public uint Offset;
            [FieldOffset(4)] public uint Width;
            [FieldOffset(8)] public uint Flags;

            public bool Signed => (Flags & 0x01) != 0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 12)]
        public struct BitpackedIndexedCompression
        {
            [FieldOffset(0)] public uint Offset;
            [FieldOffset(4)] public int Width;
            // [FieldOffset(8)] public uint ;
        }

        [StructLayout(LayoutKind.Explicit, Size = 12)]
        public struct BitpackedIndexedArrayCompression
        {
            [FieldOffset(0)] public uint Offset;
            [FieldOffset(4)] public uint Width;
            [FieldOffset(8)] public int ArraySize;
        }

        [StructLayout(LayoutKind.Explicit, Size = 4)]
        public struct CommonDataCompression
        {
            [FieldOffset(0)] public float Float;

            [FieldOffset(0)] public ulong UInt64;
            [FieldOffset(0)] public long Int64;

            [FieldOffset(0)] public uint UInt32;
            [FieldOffset(0)] public int Int32;

            [FieldOffset(0)] public ushort UInt16;
            [FieldOffset(0)] public short Int16;

            [FieldOffset(0)] public byte UInt8;
            [FieldOffset(0)] public sbyte Int8;
        }

        [FieldOffset(0), MarshalAs(UnmanagedType.Struct)] public BitpackedCompression Bitpacked;
        [FieldOffset(0), MarshalAs(UnmanagedType.Struct)] public CommonDataCompression CommonData;
        [FieldOffset(0), MarshalAs(UnmanagedType.Struct)] public BitpackedIndexedCompression BitpackedIndexed;
        [FieldOffset(0), MarshalAs(UnmanagedType.Struct)] public BitpackedIndexedArrayCompression BitpackedIndexedArray;
    }
}
