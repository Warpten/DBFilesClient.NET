using System;

namespace DBFilesClient2.NET.Attributes
{
    /// <summary>
    /// This attribute defines wether or not a property should be parsed in your structure through its <see cref="StoragePresenceAttribute.StoragePresence"/> attribute.
    /// It also lets you define the size of a given array.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class StoragePresenceAttribute : Attribute
    {
        /// <summary>
        /// Wether or not this field should be loaded. Defaults to <see cref="StoragePresence.Include"/>.
        /// </summary>
        public StoragePresence StoragePresence { get; set; } = StoragePresence.Include;

        /// <summary>
        /// The size of the member if that member is an array.
        /// </summary>
        public int SizeConst { get; set; }

        public StoragePresenceAttribute(StoragePresence presence, int sizeConst)
        {
            StoragePresence = presence;
            SizeConst = sizeConst;
        }

        public StoragePresenceAttribute(StoragePresence presence)
        {
            StoragePresence = presence;
        }
    }

    public enum StoragePresence
    {
        Include,
        Exclude
    }
}
