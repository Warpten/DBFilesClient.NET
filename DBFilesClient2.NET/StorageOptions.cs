using System.Reflection;

namespace DBFilesClient2.NET
{
    public sealed class StorageOptions
    {
        /// <summary>
        /// Set to true to have all strings be interned. Reduces memory footprint, advised for long-lasting instance of <see cref="Storage{TKey, TValue}"/>.
        /// </summary>
        public bool InternStrings { get; set; }

        /// <summary>
        /// Set to true to save the string pool into an array accessible outside the DBC. This would mostly be useful to dataminers.
        /// </summary>
        public bool LoadStringPool { get; set; }

        /// <summary>
        /// Set to true to load records as well. This is typically set to false when all that matters to you is the string table.
        /// </summary>
        public bool LoadRecords { get; set; }

        /// <summary>
        /// This property controls wether the library will load either fields or properties. It can not and will not do both.
        /// 
        /// Should you choose <see cref="MemberType.Property"/>, only properties with a setter will be parsed.
        /// </summary>
        public MemberTypes MemberType { get; set; }

        public bool MemoryBuffer { get; set; }

        /// <summary>
        /// The default settings for <see cref="Storage{TKey, TValue}"/>.
        /// <list type="bullet">
        ///     <listheader>
        ///         <term>Member</term>
        ///         <description>Default value</description>
        ///     </listheader>
        ///     <item>
        ///         <term><see cref="InternStrings"/></term>
        ///         <description><code>true</code></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="LoadStringPool"/></term>
        ///         <description><code>true</code></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="LoadRecords"/></term>
        ///         <description><code>true</code></description>
        ///     </item>
        ///     <item>
        ///         <term><see cref="MemberType"/></term>
        ///         <description><see cref="MemberTypes.Properties"></description>
        ///     </item>
        /// </list>
        /// </summary>
        public static StorageOptions Default { get; } = new StorageOptions()
        {
            InternStrings = false,
            LoadStringPool = false,
            MemberType = MemberTypes.Property,
            LoadRecords = true,
            MemoryBuffer = true
        };
    }
}
