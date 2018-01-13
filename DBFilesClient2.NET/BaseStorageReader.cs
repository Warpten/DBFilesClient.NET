using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Implementations;
using DBFilesClient2.NET.Implementations.Serializers;
using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DBFilesClient2.NET
{
    internal abstract class BaseStorageReader<TKey, TValue, THeader> : BinaryReader, IStorageReader<TKey, TValue, THeader>
        where TKey : struct
        where TValue : class, new()
        where THeader : IStorageHeader, new()
    {
        #region IStorageReader implementation
        public Type ValueType { get; } = typeof(TValue);
        public Type KeyType { get; } = typeof(TKey);

        public StorageOptions Options { get; private set; }

        public THeader Header { get; } = new THeader();
        #endregion

        public ISerializer<TKey, TValue> Serializer { get; set; }

        #region Parsing utilities
        protected Dictionary<TKey, long> OffsetMap { get; } = new Dictionary<TKey, long>();
        protected List<TKey> IndexTable { get; } = new List<TKey>();
        #endregion

        public FieldMetadata[] TypeMembers { get; protected set; }

        private MemberInfo[] _members;
        public MemberInfo[] Members
        {
            get
            {
                if (_members != null)
                    return _members;

                if (Options.MemberType == MemberTypes.Field)
                    return _members = typeof(TValue).GetFields(BindingFlags.Public | BindingFlags.Instance);

                return _members = typeof(TValue).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            }
        }

        public ICommonTable<TKey, TValue> CommonTable { get; private set; }

        protected BaseStorageReader(Stream baseStream, StorageOptions options) : base(baseStream, options)
        {
            Options = options;

            var keyCode = Type.GetTypeCode(typeof(TKey));
            if (keyCode != TypeCode.Int32 && keyCode != TypeCode.UInt32)
                throw new InvalidStructureException<TValue>(ExceptionReason.KeyMustBeInteger);
        }

        public event Action<TKey, TValue> RecordLoaded;
        public event Action<long, string> StringLoaded;

        public abstract bool ParseHeader();

        protected virtual void LoadCommonDataTable()
        {
            throw new NotSupportedException("This method should not have been called!");
        }

        public virtual void LoadFile()
        {
            TryEarlyParseStringPool();
            TryParseIndexTable();
            TryParseCommonTable();

            LoadRecords();
        }

        protected abstract void LoadRecords();

        protected void TryEarlyParseStringPool()
        {
            if (Options.LoadStringPool && StringLoaded != null && Header.StringTable.Exists)
            {
                BaseStream.Position = Header.StringTable.StartOffset;

                while (BaseStream.Position <= Header.StringTable.StartOffset + Header.StringTable.Size)
                {
                    var stringOffset = BaseStream.Position;
                    var @string = ReadString();

                    StringLoaded.Invoke(stringOffset - Header.StringTable.StartOffset, @string);
                }
            }
        }

        protected void TryParseIndexTable()
        {
            if (Header.IndexTable.Exists)
            {
                BaseStream.Position = Header.IndexTable.StartOffset;

                for (var i = 0; i < Header.RecordCount; ++i)
                    IndexTable.Add(this.ReadStruct<TKey>());
            }
        }

        protected void TryParseCommonTable()
        {
            if (Header.CommonTable.Exists)
            {
                BaseStream.Position = Header.CommonTable.StartOffset;
                CommonTable = new WDCCommonTable<TKey, TValue, THeader>(this);
            }
        }

        protected void OnRecordLoaded(TKey key, TValue value) => RecordLoaded(key, value);

        protected void GenerateMemberMetadata()
        {
            // Second pass - Generate array sizes
            for (var i = 0; i < Header.FieldCount - 1; ++i)
            {
                var currentField = TypeMembers[i];
                var nextField = TypeMembers[i + 1];
                currentField.GuessedArraySize = (int)((nextField.OffsetInRecord - currentField.OffsetInRecord) / currentField.ByteSize);
            }

            // Third pass - flatten elements that were compacted by the user in a single array, warning them in the meantime
            for (int metaItr = 0, structItr = 0; metaItr < Header.FieldCount; ++metaItr, ++structItr)
            {
                var memberInfo = Members[structItr];
                if (memberInfo.GetCustomAttribute<IndexAttribute>() != null && Header.IndexTable.Exists)
                    memberInfo = Members[structItr += 1];

                TypeMembers[metaItr].MemberInfo = memberInfo;

                //! TODO: Given the following structure (in file metadata)
                // public int   A {get;set;}
                // public int[] B {get;set;}
                // It is perfectly acceptable to combine these (in the declared structure)!

                if (!((memberInfo is FieldInfo fieldInfo && fieldInfo.FieldType.IsArray)
                    || (memberInfo is PropertyInfo propInfo && propInfo.PropertyType.IsArray)))
                    continue;

                var arrayAttribute = memberInfo.GetCustomAttribute<StoragePresenceAttribute>();
                if (arrayAttribute == null)
                {
                    //! TODO No longer needed for WDC1!

                    // If we found out the last field doesn't have StoragePresenceAttribute ...
                    if (metaItr == Header.FieldCount - 1) // ... and is an array, then complain very loudly!
                        throw new InvalidStructureException<TValue>(ExceptionReason.MissingStoragePresence, memberInfo.Name);

                    continue;
                }

                // Count the number of following fields with the same byte size
                var matchingFieldCount = 0;
                var resultingArraySize = 0;
                var j = metaItr + 1;
                for (; j < Header.FieldCount; ++j)
                {
                    ++matchingFieldCount;
                    resultingArraySize += TypeMembers[j].GetArraySize();
                    if (TypeMembers[j].ByteSize != TypeMembers[metaItr].ByteSize)
                        break;
                }

                // The user intended to have an array smaller than what is possible
                if (arrayAttribute.SizeConst < resultingArraySize)
                {
                    TypeMembers[metaItr].GuessedArraySize = arrayAttribute.SizeConst;
                    metaItr += arrayAttribute.SizeConst - 1;
                }
                else // Gobble everything
                {
                    TypeMembers[metaItr].GuessedArraySize = resultingArraySize;
                    metaItr += matchingFieldCount - 1;
                }
            }

            TypeMembers = TypeMembers.Where(f => f.MemberInfo != null).ToArray();
        }

        public void CheckRecordSize()
        {
            if (Header.RecordSize != Serializer.RecordSize)
                throw new InvalidStructureException<TValue>(ExceptionReason.StructureSizeMismatch, Header.RecordSize, Serializer.RecordSize);
        }

        public override string ReadString(Encoding encoding)
        {
            if (!Header.StringTable.Exists)
                return ReadStringDirect(encoding);

            // Not used if strings are inlined, so doesn't matter.
            var oldPosition = BaseStream.Position + 4;

            BaseStream.Position = ReadInt32() + Header.StringTable.StartOffset;
            var result = ReadStringDirect(encoding);
            BaseStream.Position = oldPosition;

            return result;
        }
    }
}
