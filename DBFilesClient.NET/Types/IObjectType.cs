namespace DBFilesClient.NET.Types
{
    /// <summary>
    /// An object type used as a wrapper for more complex types.
    /// </summary>
    /// <example>
    /// This example shows you how to implement foreign keys in your application.
    /// <code>
    ///     public class ForeignKey<T, U> : IObjectType<U>
    ///     {
    ///         public ForeignKey(T underlyingValue) : base(underlyingValue) { }
    /// 
    ///         public T Value => ...;
    ///     }
    /// </code>
    /// </example>
    /// <typeparam name="T">The underlying type of this object.</typeparam>
    public abstract class IObjectType<T> where T : struct
    {
        protected IObjectType(T underlyingValue)
        {
            Key = underlyingValue;
        }

        public virtual T Key { get; protected set; }

        public override string ToString() => Key.ToString();
    }
}
