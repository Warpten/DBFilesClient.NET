using System;

namespace DBFilesClient.NET
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class DBFileNameAttribute : Attribute
    {
        public string FileName { get; }

        public DBFileNameAttribute(string fileName)
        {
            FileName = fileName;
        }
    }
}
