using DBFilesClient.NET;

namespace Tests.Structures
{
    [DBFileName("SpellInterrupts")]
    public sealed class SpellInterruptsEntry
    {
        public uint SpellID;
        public uint[] AuraInterruptFlags;
        public uint[] ChannelInterruptFlags;
        public ushort InterruptFlags;
        public byte DifficultyID;
    }
}