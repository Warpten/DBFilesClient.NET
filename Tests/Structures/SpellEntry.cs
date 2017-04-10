namespace Tests.Structures
{
    [DBFile("Spell")]
    public class SpellEntry
    {
        public string Name;
        public string NameSubtext;
        public string Description;
        public string AuraDescription;
        public uint MiscID;
        public uint ID;
        public uint DescriptionVariablesID;
    }
}