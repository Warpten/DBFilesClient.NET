using DBFilesClient.NET.Types;

namespace Tests.DataTypes
{
    public sealed class Money : IObjectType<uint>
    {
        public Money(uint underlyingValue) : base(underlyingValue)
        {
            _gold = Key / (100 * 100);
            _silver = (Key / 100) % 100;
            _copper = Key % 100;
        }

        private uint _gold;
        private uint _silver;
        private uint _copper;

        public override string ToString()
        {
            return $"{_gold}g {_silver}s {_copper}c";
        }
    }
}
