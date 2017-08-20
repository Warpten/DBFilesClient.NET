using DBFilesClient.NET.Types;

namespace Tests.DataTypes
{
    public class ForeignKey<T> : IObjectType<uint> where T : class, new()
    {
        public ForeignKey(uint underlyingValue) : base(underlyingValue)
        {
        }

        // public T Value => DbcStore.Get<T>(Key);
    }
}
