using System;

namespace DBFilesClient2.NET.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class IndexAttribute : Attribute
    {
        public IndexAttribute()
        {

        }
    }
}
