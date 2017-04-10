namespace Tests.Structures
{
    [DBFile("SpellXSpellVisual")]
    public class SpellXSpellVisualEntry
    {
        public uint SpellID;
        public uint SpellVisualID;
        public uint ID;
        public float Chance;
        public short CasterPlayerConditionID;
        public short CasterUnitConditionID;
        public short PlayerConditionID;
        public short UnitConditionID;
        public uint IconFileDataID;
        public uint ActiveIconFileDataID;
        public byte Flags;
        public byte DifficultyID;
        public byte Priority;
    }
}