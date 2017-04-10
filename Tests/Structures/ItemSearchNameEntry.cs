using System.Runtime.InteropServices;

namespace Tests.Structures
{
    [DBFile("ItemSearchName")]
    public class ItemSearchNameEntry
    {
        public string Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
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
        public int AllowableClass;
    }
}