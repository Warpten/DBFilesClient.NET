using System;

namespace DBFilesClient.NET
{
    public interface IStorage
    {
        /// <summary>
        /// Record type.
        /// </summary>
        Type RecordType { get; }

        /// <summary>
        /// Signature of the file (WDBC, WDB2, WDB5).
        /// </summary>
        int Signature { get; set; }

        /// <summary>
        /// Returns true if the index is not contained in the record.
        /// This is false by design in WDBC and WDB2.
        /// </summary>
        bool HasIndexTable { get; set; }

        /// <summary>
        /// Returns true if the file contains a string table.
        /// </summary>
        bool HasStringTable { get; set; }

        /// <summary>
        /// Position of the index field in the record.
        /// This is 0 by design in WDBC and WDB2.
        /// </summary>
        ushort IndexField { get; set; }
    }
}
