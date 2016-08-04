using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("SpellCategories")]
    public sealed class SpellCategoriesEntry
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