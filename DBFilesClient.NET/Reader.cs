using System;
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

        protected Reader(Stream input) : base(input)
        {
        }

        internal abstract void Load();
    }
}
