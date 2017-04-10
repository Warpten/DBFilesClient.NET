namespace Tests.Structures
{
    [DBFile("SpellCategories")]
    public class SpellCategoriesEntry
    {
        public uint SpellID;
        public ushort Category;
        public ushort StartRecoveryCategory;
        public ushort ChargeCategory;
        public byte DifficultyID;
        public byte DefenseType;
        public byte DispelType;
        public byte Mechanic;
        public byte PreventionType;
    }
}