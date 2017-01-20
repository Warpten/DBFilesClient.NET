using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DBFilesClient.NET.Types;

namespace DBFilesClient.NET
{
    public abstract class Reader<T> : BinaryReader where T : class, new()
    {
        internal class Header
        {
            public bool HasStringTable { get; set; } = false;
            public bool HasIndexTable { get; set; } = false;
            public ushort IndexField { get; set; } = 0;
        }

        internal Header FileHeader { get; } = new Header();

        protected long StringTableOffset { private get; set; }

        #region Record reader generation
        // ReSharper disable once StaticMemberInGenericType
        private Dictionary<TypeCode, MethodInfo> _binaryReaderMethods { get; } = new Dictionary<TypeCode, MethodInfo>
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

            { TypeCode.String, typeof (Reader<T>).GetMethod("ReadTableString", Type.EmptyTypes) },
        };

        protected virtual MethodInfo GetPrimitiveLoader(Type typeInfo, int fieldIndex)
        {
            return _binaryReaderMethods[Type.GetTypeCode(typeInfo)];
        }

        protected virtual MethodInfo GetPrimitiveLoader(FieldInfo fieldInfo, int fieldIndex)
        {
            var fieldType = fieldInfo.FieldType;
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();

            var typeCode = Type.GetTypeCode(fieldType);
            if (typeCode == TypeCode.Object)
                return null;

            MethodInfo methodInfo;
            _binaryReaderMethods.TryGetValue(typeCode, out methodInfo);
            return methodInfo;
        }

        protected Func<Reader<T>, T> GenerateRecordLoader()
        {
            var expressions = new List<Expression>();

            // Create a parameter expression that holds the argument type.
            var readerExpr = Expression.Parameter(typeof(Reader<T>), "reader");

            // Create a variable expression that holds the return type.
            var resultExpr = Expression.Variable(typeof(T), typeof(T).Name + "Value");

            // Instantiate the return value.
            expressions.Add(Expression.Assign(resultExpr, Expression.New(typeof(T))));

            var fieldIndex = 0;
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var fieldInfo in fields)
            {
                var fieldType = fieldInfo.FieldType;
                if (fieldType.IsArray)
                {
                    fieldType = fieldType.GetElementType();
                    Debug.Assert(!fieldType.IsArray, "Only unidimensional arrays are supported.");
                }

                var typeCode = Type.GetTypeCode(fieldType);

                ConstructorInfo fieldObjectCtor = null;
                if (typeCode == TypeCode.Object)
                {
                    var baseType = fieldType.BaseType;
                    while (baseType?.BaseType != null && baseType.BaseType.IsConstructedGenericType)
                        baseType = baseType.BaseType;

                    if (baseType?.GetGenericTypeDefinition() != typeof(IObjectType<>))
                        throw new InvalidStructureException("Only object types inheriting IObjectType<T> can be loaded.");

                    fieldObjectCtor = fieldType.GetConstructor(new[] { baseType?.GetGenericArguments()[0] });
                    if (fieldObjectCtor == null)
                        throw new InvalidStructureException($"{fieldType.Name} requires a constructor.");
                }

                Expression callExpression;
                if (typeCode != TypeCode.Object)
                {
                    var callVirt = GetPrimitiveLoader(fieldInfo, fieldIndex);

                    callExpression = Expression.Call(readerExpr, callVirt);
                }
                else
                {
                    // ReSharper disable once PossibleNullReferenceException
                    var wrappedType = fieldObjectCtor.GetParameters()[0].ParameterType;
                    var callVirt = GetPrimitiveLoader(wrappedType, fieldIndex);

                    callExpression = Expression.New(fieldObjectCtor, Expression.Convert(Expression.Call(readerExpr, callVirt), wrappedType));
                }

                if (!fieldInfo.FieldType.IsArray)
                {
                    expressions.Add(Expression.Assign(Expression.MakeMemberAccess(resultExpr, fieldInfo), Expression.Convert(callExpression, fieldType)));
                }
                else
                {
                    var arraySize = GetArraySize(fieldInfo, fieldIndex);

                    var exitLabelExpr = Expression.Label();
                    var itrExpr = Expression.Variable(typeof(int), "itr");
                    expressions.Add(Expression.Block(
                        new[] { itrExpr },
                        // ReSharper disable once AssignNullToNotNullAttribute
                        Expression.Assign(
                            Expression.MakeMemberAccess(resultExpr, fieldInfo),
                            Expression.New(fieldInfo.FieldType.GetConstructor(new[] { typeof(int) }),
                                Expression.Constant(arraySize))
                            ),

                        Expression.Assign(itrExpr, Expression.Constant(0)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.LessThan(itrExpr, Expression.Constant(arraySize)),
                                Expression.Assign(
                                    Expression.ArrayAccess(Expression.MakeMemberAccess(resultExpr, fieldInfo),
                                        Expression.PostIncrementAssign(itrExpr)),
                                    Expression.Convert(callExpression, fieldType)),
                                Expression.Break(exitLabelExpr)),
                            exitLabelExpr)
                        ));
                }

                ++fieldIndex;
            }
            expressions.Add(resultExpr);

            var expressionBlock = Expression.Block(new[] { resultExpr }, expressions);
            return Expression.Lambda<Func<Reader<T>, T>>(expressionBlock, readerExpr).Compile();
        }

        protected abstract int GetArraySize(FieldInfo fieldInfo, int fieldIndex);

        #endregion
        // ReSharper disable once MemberCanBeProtected.Global
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

        // ReSharper disable once MemberCanBeProtected.Global
        // ReSharper disable once UnusedMemberHiearchy.Global
        public virtual string ReadTableString()
        {
            // Store position of the next field in this record.
            var oldPosition = BaseStream.Position + 4;

            // Compute offset to string in table.
            BaseStream.Position = ReadInt32() + StringTableOffset;

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

        internal abstract void Load();

        // ReSharper disable once MemberCanBeProtected.Global
        public int ReadInt24()
        {
            var bytes = ReadBytes(3);
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        }

        // ReSharper disable once UnusedMember.Global
        public uint ReadUInt24() => (uint)ReadInt24();
    }
}
