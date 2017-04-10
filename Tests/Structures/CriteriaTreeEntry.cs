namespace Tests.Structures
{
    [DBFile("CriteriaTree")]
    public class CriteriaTreeEntry
    {
        public uint Amount;
        public string Description;
        public ushort Parent;
        public ushort Flags;
        public byte Operator;
        public uint CriteriaID;
        public int OrderIndex;
    }
}