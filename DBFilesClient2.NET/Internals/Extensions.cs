using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBFilesClient2.NET.Internals
{
    internal static unsafe class Extensions
    {
        public static T ReadStruct<T>(this BinaryReader br) where T : struct
        {
            if (SizeCache<T>.TypeRequiresMarshal)
            {
                var storage = br.ReadBytes(SizeCache<T>.Size);
                var handle = GCHandle.Alloc(storage, GCHandleType.Pinned);
                var structure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                handle.Free();

                return structure;

                // throw new ArgumentException(
                //     "Cannot read a generic structure type that requires marshaling support. Read the structure out manually.");
            }

            // OPTIMIZATION!
            var ret = new T();
            fixed (byte* b = br.ReadBytes(SizeCache<T>.Size))
            {
                var tPtr = (byte*)SizeCache<T>.GetUnsafePtr(ref ret);
                UnsafeNativeMethods.CopyMemory(tPtr, b, SizeCache<T>.Size);
            }
            return ret;
        }
        public static T[] ReadStruct<T>(this BinaryReader br, long count) where T : struct
        {
            return br.ReadStruct<T>((int)count);
        }

        public static T[] ReadStruct<T>(this BinaryReader br, int count) where T : struct
        {
            if (SizeCache<T>.TypeRequiresMarshal)
            {
                throw new ArgumentException(
                    "Cannot read a generic structure type that requires marshaling support. Read the structure out manually.");
            }

            if (count == 0)
                return new T[0];

            var ret = new T[count];
            fixed (byte* pB = br.ReadBytes(SizeCache<T>.Size * count))
            {
                var genericPtr = (byte*)SizeCache<T>.GetUnsafePtr(ref ret[0]);
                UnsafeNativeMethods.CopyMemory(genericPtr, pB, SizeCache<T>.Size * count);
            }
            return ret;
        }

        public static Type GetMemberType(this MemberInfo memberInfo)
        {
            if (memberInfo is FieldInfo field)
                return field.FieldType;
            if (memberInfo is PropertyInfo property)
                return property.PropertyType;
            if (memberInfo is EventInfo @event)
                return @event.EventHandlerType;
            return null;
        }

        public static unsafe TDest ReinterpretCast<TDest>(this object source)
        {
            var tr = __makeref(source);
            TDest w = default;
            var trw = __makeref(w);
            *((IntPtr*)&trw) = *((IntPtr*)&tr);
            return __refvalue(trw, TDest);
        }
    }
}
