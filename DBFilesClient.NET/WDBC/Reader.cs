using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using DBFilesClient.NET.Types;

namespace DBFilesClient.NET.WDBC
{
    internal sealed class Reader<T> : NET.Reader<T> where T : class, new()
    {
        internal Reader(Stream fileStream) : base(fileStream)
        {
        }

        internal override void Load()
        {
            // We get to this through the Factory, meaning we already read the signature...
            var recordCount = ReadInt32();
            if (recordCount == 0)
                return;
            BaseStream.Position += 4; // Counts arrays
            var recordSize = ReadInt32();
            var stringBlockSize = ReadInt32();

            FileHeader.HasStringTable = stringBlockSize != 0;

            StringTableOffset = BaseStream.Length - stringBlockSize;

            // Generate the record loader function now.
            _loader = GenerateRecordLoader();

            for (var i = 0; i < recordCount; ++i)
            {
                LoadRecord();
                BaseStream.Position += recordSize;
            }
        }

        private void LoadRecord()
        {
            var key = ReadInt32();
            BaseStream.Position -= 4;
            TriggerRecordLoaded(key, _loader(this));
        }

        private Func<Reader<T>, T> _loader;

        protected override int GetArraySize(FieldInfo fieldInfo, int fieldIndex)
        {
            var marshalAttr = fieldInfo.GetCustomAttribute<MarshalAsAttribute>();
            if (marshalAttr == null)
                throw new InvalidOperationException($"Field '{typeof(T).Name}.{fieldInfo.Name} is an array and needs to be decorated with MarshalAsAttribute!");

            return marshalAttr.SizeConst;
        }
    }
}
