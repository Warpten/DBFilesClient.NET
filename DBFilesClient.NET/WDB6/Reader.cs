using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBFilesClient.NET.WDB6
{
    internal class Reader<T> : WDB5.Reader<T> where T : class, new()
    {
        private CommonData[] _nonZeroValues;
 
        internal Reader(Stream fileData) : base(fileData)
        {
        }

        protected override void LoadHeader()
        {
            // Read header
            FileHeader.RecordCount = ReadInt32();
            FileHeader.FieldCount = ReadInt32();
            FileHeader.RecordSize = ReadInt32();
            // if flags & 0x01 != 0, this field takes on a new meaning - it becomes
            // an absolute offset to the beginning of the offset_map
            FileHeader.StringTableSize = ReadInt32();
            BaseStream.Position += 4 + 4; // TableHash, LayoutHash (but also timestamp!)
            FileHeader.MinIndex = ReadInt32();
            FileHeader.MaxIndex = ReadInt32();
            BaseStream.Position += 4; // Locale
            FileHeader.CopyTableSize = ReadInt32();
            var flags = ReadUInt16();
            FileHeader.IndexField = ReadUInt16();
            FileHeader.TotalFieldCount = ReadUInt32();
            FileHeader.CommonDataTableSize = ReadUInt32();

            FileHeader.HasIndexTable = (flags & 0x04) != 0;
            FileHeader.HasStringTable = (flags & 0x01) == 0;

            _nonZeroValues = new CommonData[FileHeader.TotalFieldCount];

            FieldMeta = new FieldEntry[FileHeader.FieldCount];

            for (var i = 0; i < FieldMeta.Length; ++i)
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                FieldMeta[i] = new FieldEntry();
                FieldMeta[i].UnusedBits = ReadInt16();
                FieldMeta[i].Position = ReadUInt16();
            }

            if (FileHeader.IndexField >= FieldMeta.Length)
                throw new InvalidOperationException("The index column is contained outside of the regular data stream!");

            FileHeader.StringTableOffset = BaseStream.Position;

            // Add missing entries
            FileHeader.RecordCount += FileHeader.CopyTableSize / 8;
        }

        private Action<Reader<T>, T, int> NonZeroDataLoader { get; set; }
        protected override void GenerateRecordLoader()
        {
            // Generate the regular record loader
            base.GenerateRecordLoader();

            if (FileHeader.CommonDataTableSize == 0)
                return;

            var oldPosition = BaseStream.Position;

            BaseStream.Position = BaseStream.Length - FileHeader.CommonDataTableSize;

            var columnCount = ReadInt32();
            var fields = typeof (T).GetFields(BindingFlags.Public | BindingFlags.Instance).ToArray();
            for (var i = 0; i < columnCount; ++i)
                _nonZeroValues[i] = new CommonData(this);

            // Generate an Action<Reader<T>, T, int>
            var expressionList = new List<Expression>();
            var readerExpr     = Expression.Parameter(typeof (Reader<T>));
            var recordKeyExpr  = Expression.Parameter(typeof (int));
            var structureExpr  = Expression.Parameter(typeof (T));
            var localExpr      = Expression.Variable(typeof (object));
            for (var i = FileHeader.FieldCount; i < FileHeader.TotalFieldCount; ++i)
            {
                var fieldInfo = fields[i];
                if (fieldInfo.FieldType.IsArray)
                    throw new InvalidOperationException();

                expressionList.Add(Expression.Assign(localExpr,
                    Expression.Call(readerExpr,
                        typeof (Reader<T>).GetMethod("GetCommonDataForRecord", new[] { typeof (int), typeof (int) }),
                        Expression.Constant(i), recordKeyExpr)));
                expressionList.Add(Expression.IfThen(
                    Expression.NotEqual(localExpr, Expression.Constant(null)),
                    Expression.Assign(
                        Expression.MakeMemberAccess(structureExpr, fieldInfo),
                        Expression.Convert(localExpr, fieldInfo.FieldType))));
            }

            var lambda = Expression.Lambda<Action<Reader<T>, T, int>>(
                Expression.Block(new[] { localExpr}, expressionList),
                readerExpr, structureExpr, recordKeyExpr);
            NonZeroDataLoader = lambda.Compile();

            BaseStream.Position = oldPosition;
        }

        // Just to ease on all the Expression stuff.
        public object GetCommonDataForRecord(int columnIndex, int key) => _nonZeroValues[columnIndex].TryGetValue(key);

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
        protected override void LoadRecord(long recordPosition, int key, bool forceKey = false)
        {
            var record = RecordReader(this);

            if (!(FileHeader.HasIndexTable || forceKey))
            {
                BaseStream.Position = recordPosition + FieldMeta[FileHeader.IndexField].Position;
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (FieldMeta[FileHeader.IndexField].ByteSize)
                {
                    case 4: key = ReadInt32(); break;
                    case 3: key = ReadInt24(); break;
                    case 2: key = ReadInt16(); break;
                    case 1: key = ReadSByte(); break;
                }
            }

            NonZeroDataLoader(this, record, key);
            TriggerRecordLoaded(key, record);
        }

        protected override int GetArraySize(FieldInfo fieldInfo, int fieldIndex)
        {
            var currentField = FieldMeta[fieldIndex];

            var arraySize = 1;
            if (fieldIndex + 1 < FieldMeta.Length)
                arraySize = (FieldMeta[fieldIndex + 1].Position - currentField.Position) / currentField.ByteSize;
            else if (fieldInfo.FieldType.IsArray)
            {
                var largestFieldSize = FieldMeta.Max(k => k.ByteSize);
                var smallestFieldSize = FieldMeta.Min(k => k.ByteSize);

                if (smallestFieldSize != largestFieldSize)
                {
                    var marshalAttr = fieldInfo.GetCustomAttribute<MarshalAsAttribute>();
                    if (marshalAttr == null)
                        throw new InvalidStructureException($"{typeof(T).Name}.{fieldInfo.Name}'s size can't be guessed!");

                    if (marshalAttr.SizeConst != 0)
                        arraySize = marshalAttr.SizeConst;
                }
                else // No padding in this case. Guessing array size is okay.
                    arraySize = (FileHeader.RecordSize - currentField.Position) / currentField.ByteSize;
            }

            return arraySize;
        }

        internal class CommonData
        {
            private Dictionary<int, object> Values { get; }

            public CommonData(Reader<T> owner)
            {
                var count = owner.ReadInt32();
                Values = new Dictionary<int, object>(count);
                var expectedType = owner.ReadByte();

                for (var i = 0; i < count; ++i)
                {
                    switch (expectedType)
                    {
                        case 1:
                            Values[owner.ReadInt32()] = owner.ReadInt16();
                            break;
                        case 2:
                            Values[owner.ReadInt32()] = owner.ReadByte();
                            break;
                        case 3:
                            Values[owner.ReadInt32()] = owner.ReadSingle();
                            break;
                        case 4:
                            Values[owner.ReadInt32()] = owner.ReadInt32();
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }

            public object TryGetValue(int key)
            {
                object value;
                if (Values.TryGetValue(key, out value))
                    return value;
                return null;
            }
        }
    }
}
