using System;

namespace DBFilesClient.NET
{
    public sealed class ArraySizeAttribute : Attribute
    {
        public int SizeConst { get; set; }
    }
}
