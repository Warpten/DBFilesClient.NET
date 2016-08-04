using System;
using System.Diagnostics;
using System.IO;

namespace DBFilesClient.NET
{
    internal abstract class Reader : BinaryReader
    {
        // ReSharper disable UnusedMemberInSuper.Global
        public abstract string ReadInlineString();
        public abstract string ReadTableString();
        // ReSharper restore UnusedMemberInSuper.Global

        public event Action<int, object> OnRecordLoaded;

        protected void TriggerRecordLoaded(int key, object record) => 
            OnRecordLoaded?.Invoke(key, record);

        protected Reader(Stream data) : base(data)
        {
            Debug.Assert(data is MemoryStream);
        }

        internal abstract void Load();

        public int ReadInt24()
        {
            return ReadByte() | (ReadByte() << 8) | (ReadByte() << 16);
        }

        // ReSharper disable once UnusedMember.Global
        public uint ReadUInt24() => (uint)ReadInt24();
    }
}
