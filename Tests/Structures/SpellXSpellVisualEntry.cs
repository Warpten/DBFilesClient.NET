using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("SpellXSpellVisual")]
    public sealed class SpellXSpellVisualEntry
    {
        public uint SpellID;
        public float Unk620;
        public ushort SpellVisualID;
        public ushort ViolentSpellVisualID;
        public ushort PlayerConditionID;
        public byte DifficultyID;
        public byte Flags;
        public uint ID;
    }
}