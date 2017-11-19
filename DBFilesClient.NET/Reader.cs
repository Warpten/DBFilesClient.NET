using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DBFilesClient.NET.Types;

namespace DBFilesClient.NET
{
    public abstract class Reader<T> : BinaryReader where T : class, new()
    {
        protected virtual bool EnforceStructureMatch { get; } = true;

        internal class Header
        {
            // public int Magic               { get; set; }
            public int RecordCount         { get; set; }
            public int FieldCount          { get; set; }
            public int RecordSize          { get; set; }
            public int StringTableSize     { get; set; }
            // public int TableHash           { get; set; }
            // public int LayoutHash          { get; set; }
            public int MinIndex            { get; set; }
            public int MaxIndex            { get; set; }
            // public int Locale              { get; set; }
            public int CopyTableSize       { get; set; }
            // public ushort Flags            { get; set; }
            public ushort IndexField       { get; set; }
            public uint TotalFieldCount     { get; set; }
            public uint CommonDataTableSize { get; set; }

            // Not actual header data
            public long StringTableOffset  { get; set; }
            public bool HasStringTable { get; set; }
            public bool HasIndexTable { get; set; }
        }

        internal Header FileHeader { get; } = new Header();

        protected Func<Reader<T>, T> RecordReader { get; private set; } 

        #region Record reader generation
        // ReSharper disable once StaticMemberInGenericType
        private static Dictionary<TypeCode, MethodInfo> _binaryReaderMethods { get; } = new Dictionary<TypeCode, MethodInfo>
        {
            { TypeCode.Int64, typeof (BinaryReader).GetMethod("ReadInt64", Type.EmptyTypes) },
            { TypeCode.Int32, typeof (BinaryReader).GetMethod("ReadInt32", Type.EmptyTypes) },
            { TypeCode.Int16, typeof (BinaryReader).GetMethod("ReadInt16", Type.EmptyTypes) },
            { TypeCode.SByte, typeof (BinaryReader).GetMethod("ReadSByte", Type.EmptyTypes) },

            { TypeCode.UInt64, typeof (BinaryReader).GetMethod("ReadUInt64", Type.EmptyTypes) },
            { TypeCode.UInt32, typeof (BinaryReader).GetMethod("ReadUInt32", Type.EmptyTypes) },
            { TypeCode.UInt16, typeof (BinaryReader).GetMethod("ReadUInt16", Type.EmptyTypes) },
            { TypeCode.Byte, typeof (BinaryReader).GetMethod("ReadByte", Type.EmptyTypes) },

            { TypeCode.Char, typeof (BinaryReader).GetMethod("ReadChar", Type.EmptyTypes) },
            { TypeCode.Single, typeof (BinaryReader).GetMethod("ReadSingle", Type.EmptyTypes) },

            { TypeCode.String, typeof (BinaryReader).GetMethod("ReadString", Type.EmptyTypes) },
        };

        internal virtual MethodInfo GetPrimitiveLoader(Type typeInfo, int fieldIndex)
        {
            return _binaryReaderMethods[Type.GetTypeCode(typeInfo)];
        }

        internal virtual MethodInfo GetPrimitiveLoader(PropertyInfo propertyInfo, int fieldIndex)
        {
            var fieldType = propertyInfo.PropertyType;
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();

            var typeCode = Type.GetTypeCode(fieldType);
            if (typeCode == TypeCode.Object)
                return null;

            _binaryReaderMethods.TryGetValue(typeCode, out MethodInfo methodInfo);
            return methodInfo;
        }

        private Expression GetSimpleReaderExpression(PropertyInfo propertyInfo, int fieldIndex, Expression readerExpr)
        {
            var fieldType = propertyInfo.PropertyType;
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();

            var typeCode = Type.GetTypeCode(fieldType);

            ConstructorInfo fieldObjectCtor = null;
            if (typeCode == TypeCode.Object)
            {
                var baseType = fieldType.GetTypeInfo().BaseType;
                while (baseType?.GetTypeInfo().BaseType != null && baseType.GetTypeInfo().BaseType.IsConstructedGenericType)
                    baseType = baseType.GetTypeInfo().BaseType;

                if (baseType?.GetGenericTypeDefinition() != typeof(IObjectType<>))
                    throw new InvalidStructureException("Only object types inheriting IObjectType<T> can be loaded.");

                fieldObjectCtor = fieldType.GetConstructor(new[] { baseType?.GetGenericArguments()[0] });
                if (fieldObjectCtor == null)
                    throw new InvalidStructureException($"{fieldType.Name} requires a constructor.");
            }

            Expression callExpression;
            if (typeCode != TypeCode.Object)
            {
                var callVirt = GetPrimitiveLoader(propertyInfo, fieldIndex);

                callExpression = Expression.Call(readerExpr, callVirt);
            }
            else
            {
                // ReSharper disable once PossibleNullReferenceException
                var wrappedType = fieldObjectCtor.GetParameters()[0].ParameterType;
                var callVirt = GetPrimitiveLoader(wrappedType, fieldIndex);

                callExpression = Expression.New(fieldObjectCtor,
                    Expression.Convert(Expression.Call(readerExpr, callVirt), wrappedType));
            }

            return callExpression;
        }

        protected virtual void GenerateRecordLoader()
        {
            if (RecordReader != null)
                return;

            var expressions = new List<Expression>();

            // Create a parameter expression that holds the argument type.
            var readerExpr = Expression.Parameter(typeof(Reader<T>), "reader");

            // Create a variable expression that holds the return type.
            var resultExpr = Expression.Variable(typeof(T), typeof(T).Name + "Value");

            // Instantiate the return value.
            expressions.Add(Expression.Assign(resultExpr, Expression.New(typeof(T))));

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToArray();

            if (properties.Length < FileHeader.FieldCount && EnforceStructureMatch)
                throw new InvalidOperationException(
                    $"Structure {typeof(T).Name} is missing properties ({properties.Length} found, {FileHeader.FieldCount} expected).");

            for (var fieldIndex = 0; fieldIndex < FileHeader.FieldCount; ++fieldIndex)
            {
                var propertyInfo = properties[fieldIndex];

                var callExpression = GetSimpleReaderExpression(propertyInfo, fieldIndex, readerExpr);

                if (!propertyInfo.PropertyType.IsArray)
                {
                    expressions.Add(Expression.Assign(
                        Expression.MakeMemberAccess(resultExpr, propertyInfo),
                        Expression.Convert(callExpression, propertyInfo.PropertyType)));
                }
                else
                {
                    var arraySize = GetArraySize(propertyInfo, fieldIndex);

                    var exitLabelExpr = Expression.Label();
                    var itrExpr = Expression.Variable(typeof(int));
                    expressions.Add(Expression.Block(
                        new[] { itrExpr },
                        // ReSharper disable once AssignNullToNotNullAttribute
                        Expression.Assign(
                            Expression.MakeMemberAccess(resultExpr, propertyInfo),
                            Expression.New(propertyInfo.PropertyType.GetConstructor(new[] { typeof(int) }),
                                Expression.Constant(arraySize))
                            ),

                        Expression.Assign(itrExpr, Expression.Constant(0)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.LessThan(itrExpr, Expression.Constant(arraySize)),
                                Expression.Assign(
                                    Expression.ArrayAccess(Expression.MakeMemberAccess(resultExpr, propertyInfo),
                                        Expression.PostIncrementAssign(itrExpr)),
                                    Expression.Convert(callExpression, propertyInfo.PropertyType.GetElementType())),
                                Expression.Break(exitLabelExpr)),
                            exitLabelExpr)
                        ));
                }
            }
            expressions.Add(resultExpr);

            var expressionBlock = Expression.Block(new[] { resultExpr }, expressions);
            RecordReader = Expression.Lambda<Func<Reader<T>, T>>(expressionBlock, readerExpr).Compile();
        }

        protected virtual int GetArraySize(PropertyInfo propertyInfo, int fieldIndex)
        {
            var marshalAttr = propertyInfo.GetCustomAttribute<ArraySizeAttribute>();
            if (marshalAttr == null)
                throw new InvalidOperationException($"Property '{typeof(T).Name}.{propertyInfo.Name} is an array and needs to be decorated with ArraySizeAttribute!");

            return marshalAttr.SizeConst;
        }
        #endregion

        // Needs to be public to be visible using Type.GetMethod(string, Type[])
        public string ReadInlineString()
        {
            var stringStart = BaseStream.Position;
            var stringLength = 0;
            while (ReadByte() != '\0')
                ++stringLength;
            BaseStream.Position = stringStart;

            var stringValue = string.Empty;
            if (stringLength != 0)
                stringValue = Encoding.UTF8.GetString(ReadBytes(stringLength));
            ReadByte();

            return stringValue;
        }

        public override string ReadString()
        {
            // Store position of the next field in this record.
            var oldPosition = BaseStream.Position + 4;

            // Compute offset to string in table.
            BaseStream.Position = ReadInt32() + FileHeader.StringTableOffset;

            // Read the string inline.
            var stringValue = ReadInlineString();

            // Restore stream position.
            BaseStream.Position = oldPosition;
            return stringValue;
        }

        public event Action<int, object> OnRecordLoaded;

        protected void TriggerRecordLoaded(int key, object record) => OnRecordLoaded?.Invoke(key, record);

        protected Reader(Stream data) : base(data)
        {
            if (!data.CanSeek)
                throw new InvalidOperationException("The provided DBC/DB2 data stream must support seek operations!");
        }

        internal void Load()
        {
            LoadHeader();

            GenerateRecordLoader();
            LoadRecords();
        }

        public int ReadInt24()
        {
            var bytes = ReadBytes(3);
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        }

        public uint ReadUInt24() => (uint)ReadInt24();

        protected abstract void LoadHeader();
        protected abstract void LoadRecords();
    }
}
