using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBFilesClient.NET.WDB2
{
    internal class Reader<T> : NET.Reader<T> where T : class, new()
    {
        internal Reader(Stream fileStream) : base(fileStream)
        {
        }

        internal override void Load()
        {
            var recordCount = ReadInt32();
            if (recordCount == 0)
                return;

            BaseStream.Position += 4;
            var recordSize = ReadInt32();
            var stringTableSize = ReadInt32();
            BaseStream.Position += 12;
            var minIndex = ReadInt32();
            var maxIndex = ReadInt32();

            FileHeader.HasStringTable = stringTableSize != 0;

            // Generate the record loader function now.
            _loader = GenerateRecordLoader();

            BaseStream.Position += 8 + (maxIndex - minIndex + 1) * (4 + 2);

            StringTableOffset = BaseStream.Length - stringTableSize;

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
