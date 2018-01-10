using System;

namespace DBFilesClient2.NET.Exceptions
{
    public sealed class StorageLoadException : Exception
    {
        public StorageLoadException(string fieldName) : base($"{fieldName} has not been assigned!")
        {

        }
    }
}
