using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Exceptions;
using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace DBFilesClient2.NET.Implementations.Serializers
{
    internal class WDBSerializer<TKey, TValue, THeader> : ISerializer<TKey, TValue>
        where TKey : struct
        where TValue : class, new()
        where THeader : IStorageHeader, new()
    {
        private int _size;
        private Func<BinaryReader, TValue> _deserializer;
        private Func<TValue, TKey> _keyGetter;
        private Action<TValue, TKey> _keySetter;
        
        public IStorageReader<TKey, TValue> Storage { get; set; }

        public virtual int Size
        {
            get
            {
                if (_size != 0)
                    return _size;

                var indexFound = false;
                foreach (var memberMeta in Storage.TypeMembers)
                {
                    var memberInfo = memberMeta.MemberInfo;

                    var isIndex = memberInfo.GetCustomAttribute<IndexAttribute>() != null;
                    if (indexFound && isIndex)
                        throw new InvalidStructureException<TValue>(ExceptionReason.MultipleIndex);

                    indexFound |= isIndex;
                    if (isIndex && Storage.Header.IndexTable.Exists)
                        continue;

                    var fieldType = GetMemberType(memberInfo, out var isArray);
                    var baseSize = SizeCache.GetSizeOf(fieldType);
                    if (isArray)
                    {
                        var presenceAttribute = memberInfo.GetCustomAttribute<StoragePresenceAttribute>();
                        if (presenceAttribute != null)
                            baseSize *= presenceAttribute.SizeConst;
                        else
                            throw new InvalidStructureException<TValue>(ExceptionReason.MissingStoragePresence, memberInfo.Name);
                    }

                    _size += baseSize;
                }

                return _size;
            }
        }

        public virtual Func<BinaryReader, TValue> Deserializer
        {
            get
            {
                if (_deserializer != null)
                    return _deserializer;

                var expressions = new List<Expression>();
                var readerExpression = Expression.Parameter(typeof(BinaryReader), "reader");
                var resultExpression = Expression.Variable(typeof(TValue), "value");
                expressions.Add(Expression.Assign(resultExpression, Expression.New(typeof(TValue))));

                var skipCounter = 0;

                foreach (var memberInfo in Storage.TypeMembers)
                {
                    var memberBaseType = GetMemberType(memberInfo.MemberInfo, out var isArray);

                    var isIndex = memberInfo.MemberInfo.GetCustomAttribute<IndexAttribute>() != null;
                    if (isIndex && Storage.Header.IndexTable.Exists)
                        continue;

                    var storagePresenceAttribute = memberInfo.MemberInfo.GetCustomAttribute<StoragePresenceAttribute>();
                    if (storagePresenceAttribute != null && storagePresenceAttribute.StoragePresence == StoragePresence.Exclude && !isIndex)
                    {
                        // Increment the counter
                        skipCounter += storagePresenceAttribute.SizeConst * SizeCache.GetSizeOf(memberBaseType);
                        continue;
                    }

                    if (skipCounter != 0)
                    {
                        // Skip everything at once
                        var skipExpression = Expression.Constant((long)(skipCounter));
                        var baseStreamExpression = Expression.MakeMemberAccess(readerExpression, MemberProvider.BaseStream);
                        var positionExpression = Expression.MakeMemberAccess(baseStreamExpression, MemberProvider.StreamPosition);
                        var skipAssignmentExpression = Expression.AddAssign(positionExpression, skipExpression);
                        expressions.Add(skipAssignmentExpression);

                        skipCounter = 0;
                    }

                    var memberReaderExpression = GetBaseReader(memberInfo, readerExpression);
                    var memberAccessExpression = Expression.MakeMemberAccess(resultExpression, memberInfo.MemberInfo);

                    if (!isArray)
                        expressions.Add(Expression.Assign(memberAccessExpression, Expression.Convert(memberReaderExpression, memberInfo.BaseType)));
                    else
                    {
                        var exitLabelExpr = Expression.Label();
                        var itrExpr = Expression.Variable(typeof(int));
                        expressions.Add(Expression.Block(
                            new[] { itrExpr },
                            // ReSharper disable once AssignNullToNotNullAttribute
                            Expression.Assign(
                                Expression.MakeMemberAccess(resultExpression, memberInfo.MemberInfo),
                                Expression.New(memberInfo.Type.GetConstructor(new[] { typeof(int) }),
                                    Expression.Constant(storagePresenceAttribute.SizeConst))
                                ),

                            Expression.Assign(itrExpr, Expression.Constant(0)),
                            Expression.Loop(
                                Expression.IfThenElse(
                                    Expression.LessThan(itrExpr, Expression.Constant(storagePresenceAttribute.SizeConst)),
                                    Expression.Assign(
                                        Expression.ArrayAccess(memberAccessExpression,
                                            Expression.PostIncrementAssign(itrExpr)),
                                        Expression.Convert(memberReaderExpression, memberBaseType)),
                                    Expression.Break(exitLabelExpr)),
                                exitLabelExpr)
                            ));
                    }
                }

                expressions.Add(resultExpression);

                var expressionBlock = Expression.Block(new[] { resultExpression }, expressions);
                _deserializer = Expression.Lambda<Func<BinaryReader, TValue>>(expressionBlock, readerExpression).Compile();
                return _deserializer;
            }
        }

        public virtual Action<TKey, TValue, ICommonTable<TKey, TValue>, BinaryReader> CommonTableDeserializer
        {
            get
            {
                return null; //! TODO IMPLEMENT
            }
        }

        public Func<TValue, TKey> GetKey
        {
            get
            {
                if (_keyGetter != null)
                    return _keyGetter;

                foreach (var memberInfo in Storage.ValueType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (memberInfo.MemberType != Storage.Options.MemberType || memberInfo.GetCustomAttribute<IndexAttribute>() == null)
                        continue;

                    var structExpr = Expression.Parameter(typeof(TValue), "structure");
                    var accessExpr = Expression.MakeMemberAccess(structExpr, memberInfo);

                    var lambda = Expression.Lambda<Func<TValue, TKey>>(accessExpr, new[] { structExpr }).Compile();

                    return _keyGetter = lambda;
                }

                throw new InvalidStructureException<TValue>(ExceptionReason.MissingIndex);
            }
        }

        public Action<TValue, TKey> SetKey
        {
            get
            {
                if (_keySetter != null)
                    return _keySetter;

                foreach (var memberInfo in Storage.ValueType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (memberInfo.MemberType != Storage.Options.MemberType || memberInfo.GetCustomAttribute<IndexAttribute>() == null)
                        continue;

                    var structExpr = Expression.Parameter(typeof(TValue), "structure");
                    var accessExpr = Expression.MakeMemberAccess(structExpr, memberInfo);

                    var keyValueExpr = Expression.Parameter(typeof(TKey), "keyValue");
                    var assignmentExpr = Expression.Assign(accessExpr, keyValueExpr);

                    var lambda = Expression.Lambda<Action<TValue, TKey>>(assignmentExpr, new[] { structExpr, keyValueExpr }).Compile();

                    return _keySetter = lambda;
                }

                throw new InvalidStructureException<TValue>(ExceptionReason.MissingIndex);
            }
        }

        protected static Type GetMemberType(MemberInfo memberInfo, out bool isArray)
        {
            var fieldType = (memberInfo is FieldInfo fieldInfo) ? fieldInfo.FieldType : (memberInfo as PropertyInfo).PropertyType;
            isArray = fieldType.IsArray;
            if (isArray)
                fieldType = fieldType.GetElementType();
            return fieldType;
        }

        protected static Type GetMemberType(MemberInfo memberInfo)
        {
            var fieldType = (memberInfo is FieldInfo fieldInfo) ? fieldInfo.FieldType : (memberInfo as PropertyInfo).PropertyType;
            if (fieldType.IsArray)
                fieldType = fieldType.GetElementType();
            return fieldType;
        }

        protected Expression GetBaseReader(MemberInfo fieldInfo, Expression callTarget)
        {
            var typeCode = Type.GetTypeCode(GetMemberType(fieldInfo));

            return Expression.Call(callTarget, MemberProvider.BinaryReaders[typeCode]);
        }

        protected virtual Expression GetBaseReader(FieldMetadata fieldInfo, Expression callTarget)
        {
            var typeCode = Type.GetTypeCode(GetMemberType(fieldInfo.MemberInfo));

            switch (typeCode)
            {
                case TypeCode.Int32:
                    switch (fieldInfo.ByteSize)
                    {
                        case 3: return Expression.Call(callTarget, MemberProvider.ReadInt24);
                        case 2: return Expression.Call(callTarget, MemberProvider.BinaryReaders[TypeCode.Int16]);
                        case 1: return Expression.Call(callTarget, MemberProvider.BinaryReaders[TypeCode.SByte]);
                    }
                    break;
                case TypeCode.UInt32:
                    switch (fieldInfo.ByteSize)
                    {
                        case 3: return Expression.Call(callTarget, MemberProvider.ReadUInt24);
                        case 2: return Expression.Call(callTarget, MemberProvider.BinaryReaders[TypeCode.UInt16]);
                        case 1: return Expression.Call(callTarget, MemberProvider.BinaryReaders[TypeCode.Byte]);
                    }
                    break;
            }

            return Expression.Call(callTarget, MemberProvider.BinaryReaders[typeCode]);
        }
    }
}
