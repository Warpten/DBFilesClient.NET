using System;

namespace DBFilesClient.NET
{
    public class InvalidStructureException : Exception
    {
        public string Message { get; }

        public InvalidStructureException(string message)
        {
            Message = message;
        }

        public override string ToString() => Message;
    }
}
