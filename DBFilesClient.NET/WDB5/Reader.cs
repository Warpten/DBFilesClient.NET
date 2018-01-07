using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBFilesClient.NET.WDB5
{
    internal class Reader<T> : NET.Reader<T> where T : class, new()
    {
        #region Header extras
        protected FieldEntry[] FieldMeta { get; set; }
        #endregion

        // ReSharper disable StaticMemberInGenericType
        private MethodInfo _readInt24 = typeof (Reader<T>).GetMethod("ReadInt24", Type.EmptyTypes);
        private MethodInfo _readUInt24 = typeof (Reader<T>).GetMethod("ReadUInt24", Type.EmptyTypes);
        // ReSharper restore StaticMemberInGenericType

        internal override MethodInfo GetPrimitiveLoader(Type fieldType, int fieldIndex)
        {
            var fieldData = FieldMeta[fieldIndex];
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();

            var typeCode = Type.GetTypeCode(fieldType);

            if (typeCode == TypeCode.Int32 || typeCode == TypeCode.UInt32)
            {
                switch (fieldData.ByteSize)
                {
                    case 4:
                        return base.GetPrimitiveLoader(fieldType, fieldIndex);
                    case 3:
                        return typeCode == TypeCode.Int32 ? _readInt24 : _readUInt24;
                    case 2:
                        return typeCode == TypeCode.Int32
                            ? base.GetPrimitiveLoader(typeof (short), fieldIndex)
                            : base.GetPrimitiveLoader(typeof (ushort), fieldIndex);
                    case 1:
                        return typeCode == TypeCode.Int32
                            ? base.GetPrimitiveLoader(typeof (sbyte), fieldIndex)
                            : base.GetPrimitiveLoader(typeof (byte), fieldIndex);
                }
            }
            return base.GetPrimitiveLoader(fieldType, fieldIndex);
        }

        internal override MethodInfo GetPrimitiveLoader(PropertyInfo propertyInfo, int fieldIndex)
        {
            var fieldData = FieldMeta[fieldIndex];

            var fieldType = propertyInfo.PropertyType;
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();

            var typeCode = Type.GetTypeCode(fieldType);

            // Special handling for integer types due to bit sizes
            // Note that all fields actually have bit size; we just only ever see 3 as an
            // odd value (Yes, bit size can be negative for types larger than in32!)
            if (typeCode == TypeCode.Int32 || typeCode == TypeCode.UInt32)
            {
                switch (fieldData.ByteSize)
                {
                    case 4:
                        return base.GetPrimitiveLoader(fieldType, fieldIndex);
                    case 3:
                        return typeCode == TypeCode.Int32 ? _readInt24 : _readUInt24;
                    case 2:
                        return typeCode == TypeCode.Int32
                            ? base.GetPrimitiveLoader(typeof (short), fieldIndex)
                            : base.GetPrimitiveLoader(typeof (ushort), fieldIndex);
                    case 1:
                        return typeCode == TypeCode.Int32
                            ? base.GetPrimitiveLoader(typeof (sbyte), fieldIndex)
                            : base.GetPrimitiveLoader(typeof (byte), fieldIndex);
                    default:
                        throw new ArgumentOutOfRangeException(
                            $@"Field {propertyInfo.Name} has its metadata expose as an unsupported {
                                fieldData.ByteSize}-bytes field!");
                }
            }

            return base.GetPrimitiveLoader(propertyInfo, fieldIndex);
        }

        public override string ReadString()
        {
            return !FileHeader.HasStringTable ? ReadInlineString() : base.ReadString();
        }

        internal Reader(Stream fileData) : base(fileData)
        {
        }

        protected override void LoadHeader()
        {
            FileHeader.RecordCount = ReadInt32();
            if (FileHeader.RecordCount == 0)
                return;

            FileHeader.FieldCount = ReadInt32();

            FieldMeta = new FieldEntry[FileHeader.FieldCount];

            FileHeader.RecordSize = ReadInt32();
            FileHeader.StringTableSize = ReadInt32();
            BaseStream.Position += 4 + 4;
            FileHeader.MinIndex = ReadInt32();
            FileHeader.MaxIndex = ReadInt32();
            BaseStream.Position += 4;
            FileHeader.CopyTableSize = ReadInt32();
            var flags = ReadUInt16();
            FileHeader.IndexField = ReadUInt16();

            FileHeader.HasIndexTable = (flags & 0x04) != 0;
            FileHeader.HasStringTable = (flags & 0x01) == 0;

            for (var i = 0; i < FieldMeta.Length; ++i)
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                FieldMeta[i] = new FieldEntry();
                FieldMeta[i].UnusedBits = ReadInt16();
                FieldMeta[i].Position = ReadUInt16();
            }

            FileHeader.StringTableOffset = 0x30 + FieldMeta.Length * (2 + 2) + FileHeader.RecordSize * FileHeader.RecordCount;
        }

        protected override void LoadRecords()
        {
            // Generate common file offsets
            var recordPosition = BaseStream.Position;
            var copyTablePosition = BaseStream.Length - FileHeader.CopyTableSize - FileHeader.CommonDataTableSize;
            FileHeader.CopyTableSize /= 8; // Simpler for later.

            int[] idTable = null;
            if (FileHeader.HasIndexTable)
            {
                BaseStream.Position = copyTablePosition - FileHeader.RecordCount * 4;

                idTable = new int[FileHeader.RecordCount];
                for (var i = 0; i < FileHeader.RecordCount; ++i)
                    idTable[i] = ReadInt32();
            }

            var offsetMap = new Dictionary<int /* recordIndex */, long /* absoluteOffset */>();

            if (FileHeader.HasStringTable)
            {
                for (var i = 0; i < FileHeader.RecordCount; ++i)
                {
                    // <Simca_> records are padded to largest field size, 35 is padded
                    //          to 36 because 35 isn't divisible by 4
                    // <Simca_> if largest field size and smallest field size are the size,
                    //          there is of course no padding
                    BaseStream.Position = recordPosition;
                    var recordIndex = idTable?[i] ?? FileHeader.MinIndex + i;
                    LoadRecord(recordPosition, recordIndex);

                    offsetMap[recordIndex] = recordPosition;
                    recordPosition += FileHeader.RecordSize;
                }
            }
            else
            {
                var offsetCount = FileHeader.MaxIndex - FileHeader.MinIndex + 1;

                BaseStream.Position = copyTablePosition - offsetCount * (4 + 2);
                if (FileHeader.HasIndexTable) // Account for index table
                    BaseStream.Position -= FileHeader.RecordCount * 4;

                for (var i = 0; i < offsetCount; ++i)
                {
                    var offset = ReadUInt32();
                    var length = ReadUInt16();
                    if (offset == 0 || length == 0)
                        continue;

                    var index = FileHeader.MinIndex + i;
                    var nextOffsetMapPosition = BaseStream.Position;
                    BaseStream.Position = offset;
                    LoadRecord(offset, index);
                    BaseStream.Position = nextOffsetMapPosition;

                    offsetMap[index] = offset;
                }
            }

            BaseStream.Position = copyTablePosition;
            ArraySegment<byte> underlyingBuffer;
            MemoryStream memStream = BaseStream as MemoryStream;
            if (memStream!=null && memStream.TryGetBuffer(out underlyingBuffer))
            {
                for (var i = 0; i < FileHeader.CopyTableSize; ++i)
                {
                    var newIndex = ReadInt32();
                    var oldIndex = ReadInt32();

                    // Write the new index into the underlying buffer.
                    if (!FileHeader.HasIndexTable)
                    {
                        if (FieldMeta[FileHeader.IndexField].ByteSize != 4)
                            throw new InvalidOperationException();

                        var position = offsetMap[oldIndex] + FieldMeta[FileHeader.IndexField].Position;
                        for (var k = 0; k < FieldMeta[FileHeader.IndexField].ByteSize; ++k)
                            ((IList<byte>)(underlyingBuffer))[(int)(k + position)] = (byte)((newIndex >> (8 * k)) & 0xFF);
                    }

                    var nextCopyTablePosition = BaseStream.Position;
                    BaseStream.Position = offsetMap[oldIndex];
                    LoadRecord(0, newIndex, true);
                    BaseStream.Position = nextCopyTablePosition;
                }
            }

            // Add missing entries
            FileHeader.RecordCount += FileHeader.CopyTableSize;
        }

        /// <summary>
        /// Generates a new record for the provided key.
        /// 
        /// If the file has an index table, and if <see cref="forceKey"/> is <b>false</b>,
        /// the code reads the index value from the stream and uses it.
        /// </summary>
        /// <param name="recordPosition">Absolute position of this record in the file stream.
        /// 
        /// This parameter is used if the file has the ID column in its data members (<see cref="NET.Reader{T}.FileHeader.HasIndexTable"/> is <b>false</b>).</param>
        /// <param name="key">The index of this record (ID).</param>
        /// <param name="forceKey">If set to <b>true</b>, <see cref="recordPosition"/> is ignored.</param>
        protected virtual void LoadRecord(long recordPosition, int key, bool forceKey = false)
        {
            var record = RecordReader(this);

            if (FileHeader.HasIndexTable || forceKey)
                TriggerRecordLoaded(key, record);
            else
            {
                BaseStream.Position = recordPosition + FieldMeta[FileHeader.IndexField].Position;
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (FieldMeta[FileHeader.IndexField].ByteSize)
                {
                    case 4:
                        TriggerRecordLoaded(ReadInt32(), record);
                        break;
                    case 3:
                        TriggerRecordLoaded(ReadInt24(), record);
                        break;
                    case 2:
                        TriggerRecordLoaded(ReadInt16(), record);
                        break;
                    case 1:
                        TriggerRecordLoaded(ReadSByte(), record);
                        break;
                }
            }
        }

        protected override int GetArraySize(PropertyInfo propertyInfo, int fieldIndex)
        {
            var currentField = FieldMeta[fieldIndex];

            var arraySize = 1;
            if (fieldIndex + 1 < FieldMeta.Length)
                arraySize = (FieldMeta[fieldIndex + 1].Position - currentField.Position) / currentField.ByteSize;
            else if (propertyInfo.PropertyType.IsArray)
            {
                var largestFieldSize = FieldMeta.Max(k => k.ByteSize);
                var smallestFieldSize = FieldMeta.Min(k => k.ByteSize);

                if (smallestFieldSize != largestFieldSize)
                {
                    var marshalAttr = propertyInfo.GetCustomAttribute<ArraySizeAttribute>();
                    if (marshalAttr == null)
                        throw new InvalidStructureException($"{typeof(T).Name}.{propertyInfo.Name}'s size can't be guessed!");

                    if (marshalAttr.SizeConst != 0)
                        arraySize = marshalAttr.SizeConst;
                }
                else // No padding in this case. Guessing array size is okay.
                    arraySize = (FileHeader.RecordSize - currentField.Position) / currentField.ByteSize;
            }

            return arraySize;
        }

        protected class FieldEntry
        {
            public short UnusedBits { private get; set; }
            public ushort Position { get; set; }

            public int ByteSize => 4 - UnusedBits / 8;
        }
    }
}
