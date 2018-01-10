using System;

namespace DBFilesClient2.NET
{
    [Flags]
    public enum Locales
    {
        Korean     = 1 << 1,
        French     = 1 << 2,
        German     = 1 << 3,
        Chinese    = 1 << 4,
        Taiwanese  = 1 << 5,
        Spanish    = 1 << 6,
        Mexican    = 1 << 7,
        Russian    = 1 << 8,
        /// <summary>
        /// Also Brazilian.
        /// </summary>
        Portuguese = 1 << 9,
        Italian    = 1 << 10,
        All        = -1
    }
}
