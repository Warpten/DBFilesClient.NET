using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("ItemSearchName")]
    public sealed class ItemSearchNameEntry
    {
        public string Name;
        public uint[] Flags;
        public uint AllowableRace;
        public uint RequiredSpell;
        public ushort RequiredReputationFaction;
        public ushort RequiredSkill;
        public ushort RequiredSkillRank;
        public ushort ItemLevel;
        public byte Quality;
        public byte RequiredExpansion;
        public byte RequiredReputationRank;
        public byte RequiredLevel;
        public uint AllowableClass;
    }
}