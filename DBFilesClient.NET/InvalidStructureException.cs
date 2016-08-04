using System;

namespace DBFilesClient.NET
{
    public class InvalidStructureException : Exception
    {
        public InvalidStructureException(string message) : base(message)
        {
            
        }
    }
}
