using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Sigil;

namespace DBFilesClient.NET.WDB5
{
    internal sealed class Reader<T> : Reader where T : class, new()
    {
        private class FileHeader
        {
            public int RecordSize { get; set; }
            public int RecordCount { get; set; }
            public ushort Flags { private get; set; }
            public ushort IndexField { get; set; }

            public FieldEntry[] FieldMeta { get; set; }

            public bool HasIndexTable => (Flags & 0x04) != 0;
            public bool HasStringTable => (Flags & 0x01) == 0;

            private const int HeaderSize = 0x30;

            public int RecordOffset => HeaderSize + FieldMeta.Length * (2 + 2);
        }

        private FileHeader Header { get; } = new FileHeader();

        // ReSharper disable once StaticMemberInGenericType
        private static MethodInfo _readInt24 = typeof (Reader).GetMethod("ReadInt24", Type.EmptyTypes);

        private MethodInfo GetPrimitiveLoader(FieldInfo fieldInfo, int fieldIndex)
        {
            var fieldData = Header.FieldMeta[fieldIndex];

            var fieldType = fieldInfo.FieldType;
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
                        return GetPrimitiveLoader(fieldType);
                    case 3:
                        return _readInt24;
                    case 2:
                        return GetPrimitiveLoader<short>();
                    case 1:
                        return GetPrimitiveLoader<byte>();
                    default:
                        throw new ArgumentOutOfRangeException($"Field {fieldInfo.Name} has its metadata expose as an unsupported {fieldData.ByteSize}-bytes field!");
                }
            }

            return GetPrimitiveLoader(fieldType);
        }

        public override string ReadTableString()
        {
            return !Header.HasStringTable ? ReadInlineString() : base.ReadTableString();
        }

        internal Reader(Stream fileData) : base(fileData)
        {
            // We get to this through the Factory, meaning we already read the signature...
        }

        internal override void Load()
        {
            Header.RecordCount = ReadInt32();
            if (Header.RecordCount == 0)
                return;
            Header.FieldMeta = new FieldEntry[ReadInt32()];
            Header.RecordSize = ReadInt32();
            BaseStream.Position += 4 + 4 + 4;
            var minIndex = ReadInt32();
            var maxIndex = ReadInt32();
            BaseStream.Position += 4;
            var copyTableSize = ReadInt32();
            Header.Flags = ReadUInt16();
            Header.IndexField = ReadUInt16();

            for (var i = 0; i < Header.FieldMeta.Length; ++i)
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                Header.FieldMeta[i] = new FieldEntry();
                Header.FieldMeta[i].BitSize = ReadInt16();
                Header.FieldMeta[i].Position = ReadUInt16();
            }

            StringTableOffset = Header.RecordOffset + Header.RecordSize * Header.RecordCount;

            // Field metadata is loaded, generate the record loader function now.
            _loader = GenerateRecordLoader();

            // Generate common file offsets
            var recordPosition = BaseStream.Position;
            var copyTablePosition = BaseStream.Length - copyTableSize;
            copyTableSize /= 8; // Simpler for later.

            int[] idTable = null;
            if (Header.HasIndexTable)
            {
                BaseStream.Position = copyTablePosition - Header.RecordCount * 4;

                idTable = new int[Header.RecordCount];
                for (var i = 0; i < Header.RecordCount; ++i)
                    idTable[i] = ReadInt32();
            }

            var offsetMap = new Dictionary<int /* recordIndex */, long /* absoluteOffset */>();

            if (Header.HasStringTable)
            {
                for (var i = 0; i < Header.RecordCount; ++i)
                {
                    // <Simca_> records are padded to largest field size, 35 is padded
                    //          to 36 because 35 isn't divisible by 4
                    // <Simca_> if largest field size and smallest field size are the size, there is of course no padding
                    BaseStream.Position = recordPosition;
                    var recordIndex = idTable?[i] ?? minIndex + i;
                    LoadRecord(recordPosition, recordIndex);

                    offsetMap[recordIndex] = recordPosition;

                    recordPosition += Header.RecordSize;
                }
            }
            else
            {
                var offsetCount = maxIndex - minIndex + 1;

                BaseStream.Position = copyTablePosition - offsetCount * (4 + 2);
                if (Header.HasIndexTable) // Account for index table
                    BaseStream.Position -= Header.RecordCount * 4;

                for (var i = 0; i < maxIndex - minIndex + 1; ++i)
                {
                    var offset = ReadUInt32();
                    var length = ReadUInt16();
                    if (offset == 0 || length == 0)
                        continue;

                    var index = minIndex + i;
                    var nextOffsetMapPosition = BaseStream.Position;
                    BaseStream.Position = offset;
                    LoadRecord(offset, index);
                    BaseStream.Position = nextOffsetMapPosition;

                    offsetMap[index] = offset;
                }
            }

            BaseStream.Position = copyTablePosition;
            for (var i = 0; i < copyTableSize; ++i)
            {
                var newIndex = ReadInt32();
                var oldIndex = ReadInt32();
                
                // Write the new index into the underlying buffer.
                if (!Header.HasIndexTable)
                {
                    var baseMemoryStream = (MemoryStream)BaseStream;
                    if (Header.FieldMeta[Header.IndexField].ByteSize != 4)
                        throw new InvalidOperationException();

                    var underlyingBuffer = baseMemoryStream.GetBuffer();
                    var position = offsetMap[oldIndex] + Header.FieldMeta[Header.IndexField].Position;
                    for (var k = 0; k < Header.FieldMeta[Header.IndexField].ByteSize; ++k)
                        underlyingBuffer[k + position] = (byte)((newIndex >> (8 * k)) & 0xFF);
                }

                var nextCopyTablePosition = BaseStream.Position;
                BaseStream.Position = offsetMap[oldIndex];
                LoadRecord(0, newIndex, true);
                BaseStream.Position = nextCopyTablePosition;
            }
        }

        /// <summary>
        /// Generates a new record for the provided key.
        /// 
        /// If the file has an index table, and if <see cref="forceKey"/> is <b>false</b>,
        /// the code reads the index value from the stream and uses it.
        /// </summary>
        /// <param name="recordPosition">Absolute position of this record in the file stream.
        /// 
        /// This parameter is used if the file has the ID column in its data members (<see cref="FileHeader.HasIndexTable"/> is <b>false</b>).</param>
        /// <param name="key">The index of this record (ID).</param>
        /// <param name="forceKey">If set to <b>true</b>, <see cref="recordPosition"/> is ignored.</param>
        private void LoadRecord(long recordPosition, int key, bool forceKey = false)
        {
            var record = _loader(this);

            if (Header.HasIndexTable || forceKey)
                TriggerRecordLoaded(key, record);
            else
            {
                BaseStream.Position = recordPosition + Header.FieldMeta[Header.IndexField].Position;
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (Header.FieldMeta[Header.IndexField].ByteSize)
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

        private delegate T LoaderDelegate(Reader<T> table);
        private LoaderDelegate _loader;

        private LoaderDelegate GenerateRecordLoader()
        {
            var emitter = Emit<LoaderDelegate>.NewDynamicMethod("LoaderDelegate", null, false);
            var resultLocal = emitter.DeclareLocal<T>();
            emitter.NewObject<T>();
            emitter.StoreLocal(resultLocal);

            var fieldIndex = 0;

            var fields = typeof (T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var fieldInfo in fields)
            {
                var currentField = Header.FieldMeta[fieldIndex];
                var fieldType = fieldInfo.FieldType;
                var isArray = fieldType.IsArray;

                var callVirt = GetPrimitiveLoader(fieldInfo, fieldIndex);
                Debug.Assert(callVirt != null);

                var arraySize = 1;
                if (fieldIndex + 1 < Header.FieldMeta.Length)
                    arraySize = (Header.FieldMeta[fieldIndex + 1].Position - currentField.Position) / currentField.ByteSize;
                else if (isArray)
                {
                    var largestFieldSize = Header.FieldMeta.Max(k => k.ByteSize);
                    var smallestFieldSize = Header.FieldMeta.Min(k => k.ByteSize);

                    if (smallestFieldSize != largestFieldSize)
                    {
                        var marshalAttr = fieldInfo.GetCustomAttribute<MarshalAsAttribute>();
                        Debug.Assert(marshalAttr != null, $"{typeof(T).Name}.{fieldInfo.Name} is an array but lacks MarshalAsAttribute!");
                        if (isArray && marshalAttr.SizeConst != 0)
                            arraySize = Math.Min(arraySize, marshalAttr.SizeConst);
                    }
                    else // No padding in this case. Guessing array size is okay.
                        arraySize = (Header.RecordSize - currentField.Position) / currentField.ByteSize;
                }

                if (!Header.HasIndexTable && fieldIndex == Header.IndexField)
                    arraySize = 1;

                if (!isArray)
                {
                    if (arraySize != 1)
                        throw new InvalidStructureException(
                            $"Field {typeof(T).Name}.{fieldInfo.Name} is an array but is declared as non-array.");

                    emitter.LoadLocal(resultLocal);
                    emitter.LoadArgument(0);
                    emitter.CallVirtual(callVirt);
                    emitter.StoreField(fieldInfo);
                }
                else
                {
                    if (arraySize == 1)
                        throw new InvalidStructureException(
                            $"Field {typeof(T).Name}.{fieldInfo.Name} is not an array but is declared as an array.");

                    emitter.LoadLocal(resultLocal);
                    emitter.LoadConstant(arraySize);
                    emitter.NewArray(fieldType.GetElementType());
                    emitter.StoreField(fieldInfo);

                    var loopBodyLabel = emitter.DefineLabel();
                    var loopConditionLabel = emitter.DefineLabel();

                    using (var iterationLocal = emitter.DeclareLocal<int>())
                    {
                        emitter.LoadConstant(0);
                        emitter.StoreLocal(iterationLocal);
                        emitter.Branch(loopConditionLabel);
                        emitter.MarkLabel(loopBodyLabel);
                        emitter.LoadLocal(resultLocal);
                        emitter.LoadField(fieldInfo);
                        emitter.LoadLocal(iterationLocal);
                        emitter.LoadArgument(0);
                        emitter.CallVirtual(callVirt);
                        emitter.StoreElement(fieldType.GetElementType());
                        emitter.LoadLocal(iterationLocal);
                        emitter.LoadConstant(1);
                        emitter.Add();
                        emitter.StoreLocal(iterationLocal);
                        emitter.MarkLabel(loopConditionLabel);
                        emitter.LoadLocal(iterationLocal);
                        emitter.LoadConstant(arraySize);
                        emitter.CompareLessThan();
                        emitter.BranchIfTrue(loopBodyLabel);
                    }
                }

                ++fieldIndex;
            }

            emitter.LoadLocal(resultLocal);
            emitter.Return();

            return emitter.CreateDelegate(OptimizationOptions.None);
        }

        private class FieldEntry
        {
            public short BitSize { private get; set; }
            public ushort Position { get; set; }

            public int ByteSize => 4 - BitSize / 8;
        }
    }
}
