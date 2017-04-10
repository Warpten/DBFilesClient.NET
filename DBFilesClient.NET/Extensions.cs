using System;
using System.IO;

namespace DBFilesClient.NET
{
    public static class Extensions
    {
        /// <summary>
        /// Polyfill for compatibility with .NET Core targets.
        /// </summary>
#if !(NETCOREAPP1_1 || NETCOREAPP1_0)
        public static bool TryGetBuffer(this MemoryStream ms, out ArraySegment<byte> segment)
        {
            try
            {
                segment = new ArraySegment<byte>(ms.GetBuffer());
                return true;
            }
            catch (UnauthorizedAccessException /* uae */)
            {
                segment = default(ArraySegment<byte>);
                return false;
            }
        }
#endif
    }
}
