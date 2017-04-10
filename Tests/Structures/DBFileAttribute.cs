using System;

namespace Tests.Structures
{
    public sealed class DBFileAttribute : Attribute
    {
        public string FileName { get;}

        public DBFileAttribute(string fileName)
        {
            FileName = fileName;
        }
    }
}
