namespace DBFilesClient2.NET.Internals
{
    internal enum MemberCompression
    {
        /// <summary>
        /// The field is a 8-, 16-, 32-, or 64-bit integer in the record data
        /// </summary>
        None,

        /// <summary>
        /// The field is a bitpacked integer in the record data. It is <see cref="FieldDetailedInfo.BitSize"/> bits long and starts at
        /// <see cref="FieldDetailedInfo.BitOffset"/>.
        /// </summary>
        Bitpacked,

        /// <summary>
        /// The field is assumed to be a default value, and exceptions from that default value are stored in the corresponding
        /// section in CommonData as pairs of ( uint recordId; uint value; }
        /// </summary>
        CommonData,

        /// <summary>
        /// The field has a bitpacked index in the record data. This index is used as a lookup key for the pallet data. That block is an array
        /// of uints, so the index should be multipled by 4 when addressing bytes.
        /// </summary>
        /// <remarks>you just read bitpacked index and the use it as palletData[index + offset] or palletData[index + offset + arraySize * arrayIndex]</remarks>
        BitpackedIndexed,

        /// <summary>
        /// The field has a bitpacked index in the record data. Contrary to <see cref="BitpackedIndexed"/>, the corresponding index does not
        /// need to be multiplied.
        /// </summary>
        /// <remarks>you just read bitpacked index and the use it as palletData[index + offset] or palletData[index + offset + arraySize * arrayIndex]</remarks>
        BitpackedIndexedArray
    }
}
