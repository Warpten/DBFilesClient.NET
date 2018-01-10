using System;

namespace DBFilesClient2.NET.Exceptions
{
    public class InvalidStructureException<T> : Exception
    {
        public ExceptionReason Reason { get; }

        private string _message;

        public InvalidStructureException(ExceptionReason reason, params object[] extraParameters)
        {
            Reason = reason;

            _message = $"Parsing of type {typeof(T).Name} failed with reason {reason}";
            switch (reason)
            {
                case ExceptionReason.StructureSizeMismatch:
                    _message += $" (Expected size {extraParameters[0]}, calculated {extraParameters[1]} instead).";
                    break;
                case ExceptionReason.MissingStoragePresence:
                    _message += $" (Missing StoragePresenceAttribute on array field or property {typeof(T).Name}.{extraParameters[0]}).";
                    break;
                case ExceptionReason.InvalidMetaByteSize:
                    _message += $" (The metadata generated for field or property {typeof(T).Name}.{extraParameters[0]} contains an invalid byte size ({extraParameters[1]}) - 4, 3, 2 or 1 expected.";
                    break;
                case ExceptionReason.KeyMustBeInteger:
                    _message += $" (The key provided to Storage<TKey, TValue> must be either Int32 or UInt32).";
                    break;
                case ExceptionReason.MultipleIndex:
                    _message += $" (Type {typeof(T).Name} contains multiple fields or properties declared as keys through IndexAttribute.";
                    break;
                case ExceptionReason.MissingIndex:
                    _message += $" (No member was marked as index in {typeof(T).Name}).";
                    break;
                case ExceptionReason.UnknownCommonIdentifier:
                    _message += $" (Unknown common identifier type {extraParameters[0]}).";
                    break;
                case ExceptionReason.InvalidArraySize:
                    _message += $" (Field {extraParameters[0]} has invalid array size {extraParameters[1]}, should be {extraParameters[2]}).";
                    break;
                case ExceptionReason.MemberShouldBeSigned:
                    _message += $" (Field {extraParameters[0]} should be signed).";
                    break;
                default:
                    _message += '.';
                    break;
            }
        }

        public InvalidStructureException(ExceptionReason reason)
        {
            Reason = reason;
            _message = $"Parsing of type {typeof(T).Name} failed";
        }

        public override string Message => _message;
    }

    public enum ExceptionReason
    {
        StructureSizeMismatch,
        MissingStoragePresence,
        InvalidMetaByteSize,
        KeyMustBeInteger,
        MultipleIndex,
        MissingIndex,
        OutOfRecordBounds,
        UnknownCommonIdentifier,
        IncorrectCommonType,
        OutOfCommonBounds,
        InvalidArraySize,
        MemberShouldBeSigned
    }
}
