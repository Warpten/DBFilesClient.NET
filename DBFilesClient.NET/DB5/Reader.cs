using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Sigil;

namespace DBFilesClient.NET.DB5
{
    internal sealed class Reader<T> : Reader where T : class, new()
    {
        public class FileHeader
        {
            public int RecordSize { get; set; }
            public int RecordCount { get; set; }
            public ushort Flags { get; set; }
            public ushort IndexField { get; set; }

            public FieldEntry[] FieldMeta { get; set; }

            public bool HasIndexTable => (Flags & 0x04) != 0;
            public bool HasStringTable => (Flags & 0x01) == 0;

            private const int HeaderSize = 0x30;

            public int RecordOffset => HeaderSize + FieldMeta.Length * (2 + 2);
        }

        private FileHeader Header { get; } = new FileHeader();

        #region Reflection Emit helpers
        // ReSharper disable StaticMemberInGenericType
        private static Dictionary<TypeCode, MethodInfo> _binaryReaderMethods = new Dictionary<TypeCode, MethodInfo>()
        {
            { TypeCode.Int64, typeof (BinaryReader).GetMethod("ReadInt64", Type.EmptyTypes) },
            { TypeCode.Int32, typeof (BinaryReader).GetMethod("ReadInt32", Type.EmptyTypes) },
            { TypeCode.Int16, typeof (BinaryReader).GetMethod("ReadInt16", Type.EmptyTypes) },
            { TypeCode.SByte, typeof (BinaryReader).GetMethod("ReadSByte", Type.EmptyTypes) },

            { TypeCode.UInt64, typeof (BinaryReader).GetMethod("ReadUInt64", Type.EmptyTypes) },
            { TypeCode.UInt32, typeof (BinaryReader).GetMethod("ReadUInt32", Type.EmptyTypes) },
            { TypeCode.UInt16, typeof (BinaryReader).GetMethod("ReadUInt16", Type.EmptyTypes) },
            { TypeCode.Byte, typeof (BinaryReader).GetMethod("ReadByte", Type.EmptyTypes) },

            { TypeCode.Char, typeof (BinaryReader).GetMethod("ReadChar", Type.EmptyTypes) },
            { TypeCode.Single, typeof (BinaryReader).GetMethod("ReadSingle", Type.EmptyTypes) }
        };

        private MethodInfo _readInt24 = typeof (Reader<T>).GetMethod("ReadInt24", Type.EmptyTypes);
        private MethodInfo _readUInt24 = typeof (Reader<T>).GetMethod("ReadUInt24", Type.EmptyTypes);

        private MethodInfo _stringReaderMethod;
        // ReSharper restore StaticMemberInGenericType

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
                        return _binaryReaderMethods[typeCode];
                    case 3:
                        return typeCode == TypeCode.Int32 ? _readInt24 : _readUInt24;
                    case 2:
                        return typeCode == TypeCode.Int32 ? _binaryReaderMethods[TypeCode.Int16] : _binaryReaderMethods[TypeCode.UInt16];
                    case 1:
                        return typeCode == TypeCode.Int32 ? _binaryReaderMethods[TypeCode.SByte] : _binaryReaderMethods[TypeCode.Byte];
                    default:
                        throw new ArgumentOutOfRangeException($"Field {fieldInfo.Name} has its metadata expose as an unsupported {fieldData.ByteSize}-bytes field!");
                }
            }

            MethodInfo methodInfo;
            return _binaryReaderMethods.TryGetValue(typeCode, out methodInfo) ? methodInfo : null;
        }

        private MethodInfo GetStringLoader(FieldInfo fieldInfo)
        {
            var fieldType = fieldInfo.FieldType;
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();

            var typeCode = Type.GetTypeCode(fieldType);
            if (typeCode != TypeCode.String)
                return null;

            if (_stringReaderMethod != null)
                return _stringReaderMethod;

            _stringReaderMethod = Header.HasStringTable ?
                typeof (Reader<T>).GetMethod("ReadTableString", Type.EmptyTypes) :
                typeof (Reader<T>).GetMethod("ReadInlineString", Type.EmptyTypes);
            return _stringReaderMethod;
        }

        // ReSharper disable once UnusedMember.Global
        public override string ReadTableString()
        {
            // Store position of the next field in this record.
            var oldPosition = BaseStream.Position + 4;

            // Compute offset to string in table.
            BaseStream.Position = ReadInt32() + Header.RecordOffset + Header.RecordSize * Header.RecordCount;

            // Read the string inline.
            var stringValue = ReadInlineString();

            // Restore stream position.
            BaseStream.Position = oldPosition;
            return stringValue;
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public override string ReadInlineString()
        {
            var stringStart = BaseStream.Position;
            var stringLength = 0;
            while (ReadByte() != '\0')
                ++stringLength;
            BaseStream.Position = stringStart;

            if (stringLength == 0)
                return string.Empty;

            var stringValue = Encoding.UTF8.GetString(ReadBytes(stringLength));
            ReadByte();

            return stringValue;
        }
        #endregion

        internal Reader(Stream fileStream) : base(fileStream)
        {
            // We get to this through the Factory, meaning we already read the signature...
        }

        internal override void Load()
        {
            Header.RecordCount = ReadInt32();
            Header.FieldMeta = new FieldEntry[ReadInt32()];
            Header.RecordSize = ReadInt32();
            ReadInt32(); // String table size or garbage (supposedly absolute address of offsetMap ...)
            ReadInt32(); // Table hash
            ReadInt32(); // Layout hash
            var minIndex = ReadInt32();
            var maxIndex = ReadInt32();
            ReadInt32(); // Locale mask
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
            // This is here strictly for debugging (saves to an assembly that can be loaded in ilspy et al)
            /*var asmName = new AssemblyName("DynamicCreateAssembly");
            var asm = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
            var mod = asm.DefineDynamicModule(asmName.Name, asmName.Name + ".dll");
            var typeBuilder = mod.DefineType("MyType", TypeAttributes.Public | TypeAttributes.Class);
            //var method = typeBuilder.DefineMethod("DynamicCreate_T", MethodAttributes.Static | MethodAttributes.Public,
            //   typeof (T), new[] { typeof(DBFileBinaryReader), typeof(DB2<T>) });
            var emitter = Emit<LoaderDelegate>.BuildMethod(typeBuilder, "DynamicCreate_T",
                MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard);*/

            var emitter = Emit<LoaderDelegate>.NewDynamicMethod("LoaderDelegate", null, false);
            var resultLocal = emitter.DeclareLocal<T>();
            emitter.NewObject<T>();
            emitter.StoreLocal(resultLocal);

            var fieldIndex = 0;

            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var fieldInfo in fields)
            {
                var fieldType = fieldInfo.FieldType;
                var isArray = fieldInfo.FieldType.IsArray;

                var callVirt = GetPrimitiveLoader(fieldInfo, fieldIndex) ??
                               GetStringLoader(fieldInfo);

                var delimiter = fieldIndex + 1 == Header.FieldMeta.Length
                    ? Header.RecordSize
                    : Header.FieldMeta[fieldIndex + 1].Position;
                delimiter -= Header.FieldMeta[fieldIndex].Position;
                var arraySize = delimiter / Header.FieldMeta[fieldIndex].ByteSize;

                if (!isArray)
                {
                    if (arraySize > 1)
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

            /*emiter.CreateMethod();
            typeBuilder.CreateType();
            asm.Save(asmName.Name + ".dll");*/

            return emitter.CreateDelegate();
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public int ReadInt24()
        {
            return ReadByte() | (ReadByte() << 8) | (ReadByte() << 16);
        }

        // ReSharper disable once UnusedMember.Global
        public uint ReadUInt24() => (uint) ReadInt24();

        public class FieldEntry
        {
            public short BitSize { private get; set; }
            public ushort Position { get; set; }

            public int ByteSize => 4 - BitSize / 8;
        }
    }
}
