using DBFilesClient2.NET.Attributes;
using DBFilesClient2.NET.Implementations.WDC1;
using DBFilesClient2.NET.Internals;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace DBFilesClient2.NET.Implementations.Serializers
{
    internal class WDCSerializer<TKey, TValue, THeader> : WDBSerializer<TKey, TValue, THeader>
        where TKey : struct
        where TValue : class, new()
        where THeader : IStorageHeader, new()
    {
        private Func<BinaryReader, TValue> _deserializer;
        private Action<TKey, TValue, ICommonTable<TKey, TValue>, BinaryReader> _commonTableDeserializer;

        public override Func<BinaryReader, TValue> Deserializer
        {
            get
            {
                if (_deserializer != null)
                    return _deserializer;

                if (!(Storage is WDC1Reader<TKey, TValue> wdcReader))
                    throw new InvalidOperationException();

                return _deserializer = GenerateWDC1(wdcReader);
            }
        }

        private Func<BinaryReader, TValue> GenerateWDC1(WDC1Reader<TKey, TValue> reader)
        {
            var expressionList = new List<Expression>();

            var readerExpr = Expression.Parameter(typeof(BinaryReader), "reader");
            var valueExpr = Expression.Variable(typeof(TValue), "value");

            var scrubberExpr = Expression.Variable(typeof(long), "position");

            // Prepare a lot of helper expressions
            var positionExpr = Expression.MakeMemberAccess(readerExpr, MemberProvider.BinaryReaderPosition);
            var bitposExpr = Expression.MakeMemberAccess(readerExpr, MemberProvider.BinaryReaderBitPosition);

            expressionList.Add(Expression.Assign(valueExpr, Expression.New(typeof(TValue))));
            for (var i = 0; i < reader.TypeMembers.Length; ++i)
            {
                var memberMeta = reader.TypeMembers[i];

                if (reader.Header.IndexTable.Exists && memberMeta.MemberInfo.GetCustomAttribute<IndexAttribute>() != null)
                    continue;

                var memberAccessExpr = Expression.MakeMemberAccess(valueExpr, memberMeta.MemberInfo);

                var readExpr = GetBaseReader(memberMeta, readerExpr);

                switch (memberMeta.Compression)
                {
                    case MemberCompression.None:
                        {
                            if (!memberMeta.Type.IsArray)
                            {
                                expressionList.Add(Expression.Assign(
                                    memberAccessExpr,
                                    readExpr));
                            }
                            else
                            {
                                var exitLabelExpr = Expression.Label();
                                var itrExpr = Expression.Variable(typeof(int));
                                var loopMax = Expression.Constant(memberMeta.GetArraySize());
                                expressionList.Add(Expression.Block(
                                    new[] { itrExpr },
                                    // ReSharper disable once AssignNullToNotNullAttribute
                                    Expression.Assign(
                                        memberAccessExpr,
                                        Expression.New(memberMeta.Type.GetConstructor(new[] { typeof(int) }), loopMax)),

                                    Expression.Assign(itrExpr, Expression.Constant(0)),
                                    Expression.Loop(
                                        Expression.IfThenElse(
                                            Expression.LessThan(itrExpr, loopMax),
                                            Expression.Assign(
                                                Expression.ArrayAccess(memberAccessExpr, Expression.PostIncrementAssign(itrExpr)),
                                                readExpr),
                                            Expression.Break(exitLabelExpr)),
                                        exitLabelExpr)
                                ));
                            }
                            break;
                        }
                    case MemberCompression.Bitpacked:
                        expressionList.Add(Expression.Assign(memberAccessExpr, Expression.Convert(readExpr, memberMeta.BaseType)));
                        break;
                    case MemberCompression.BitpackedIndexed:
                        {
                            // var bitAmount = memberMeta.BitSize % 8;
                            // var byteAmount = memberMeta.ByteSize / 8;
                            // 
                            // expressionList.Add(Expression.Assign(
                            //     scrubberExpr,
                            //     Expression.Add(
                            //         bitposExpr,
                            //         Expression.Constant((long)memberMeta.BitSize))));
                            // 
                            // //! TODO: Make the additions a single constant - this is for debugging
                            // var seekExpr = Expression.Add(
                            //     Expression.Add(
                            //         Expression.Constant(reader.Header.PalletTable.StartOffset),
                            //         Expression.Constant(memberMeta.AdditionalDataOffset)),
                            //     Expression.Multiply(Expression.Constant(4L), Expression.Convert(readExpr, typeof(long))));
                            // expressionList.Add(Expression.Assign(positionExpr, seekExpr));
                            // expressionList.Add(Expression.Assign(
                            //     memberAccessExpr,
                            //     GetBaseReader(memberMeta.MemberInfo, readerExpr)));
                            // 
                            // // expressionList.Add(Expression.Assign(positionExpr, Expression.Constant()))
                            // expressionList.Add(Expression.Assign(
                            //     bitposExpr,
                            //     scrubberExpr));
                            break;
                        }
                    case MemberCompression.BitpackedIndexedArray:
                        {
                            // expressionList.Add(Expression.Assign(
                            //     scrubberExpr,
                            //     Expression.Add(
                            //         bitposExpr,
                            //         Expression.Constant((long)memberMeta.BitSize))));
                            // 
                            // var seekExpr = Expression.Add(
                            // Expression.Add(
                            //     Expression.Constant(reader.Header.PalletTable.StartOffset),
                            //     Expression.Constant(memberMeta.AdditionalDataOffset)),
                            // Expression.Multiply(Expression.Constant(4L), Expression.Convert(readExpr, typeof(long))));
                            // expressionList.Add(Expression.Assign(positionExpr, seekExpr));
                            // 
                            // var exitLabelExpr = Expression.Label();
                            // var itrExpr = Expression.Variable(typeof(int));
                            // expressionList.Add(Expression.Block(
                            //     new[] { itrExpr },
                            //     // ReSharper disable once AssignNullToNotNullAttribute
                            //     Expression.Assign(
                            //         memberAccessExpr,
                            //         Expression.New(memberMeta.Type.GetConstructor(new[] { typeof(int) }),
                            //             Expression.Constant(memberMeta.GetArraySize()))
                            //         ),
                            // 
                            //     Expression.Assign(itrExpr, Expression.Constant(0)),
                            //     Expression.Loop(
                            //         Expression.IfThenElse(
                            //             Expression.LessThan(itrExpr, Expression.Constant(memberMeta.GetArraySize())),
                            //             Expression.Assign(
                            //                 Expression.ArrayAccess(memberAccessExpr,
                            //                     Expression.PostIncrementAssign(itrExpr)),
                            //                 Expression.Convert(GetBaseReader(memberMeta.MemberInfo, readerExpr), memberMeta.BaseType)),
                            //             Expression.Break(exitLabelExpr)),
                            //         exitLabelExpr)
                            // ));
                            // 
                            // expressionList.Add(Expression.Assign(
                            //     bitposExpr,
                            //     scrubberExpr));
                            break;
                        }
                    // Skip common data
                    case MemberCompression.CommonData:
                    {
                        Expression defaultValueExpr = null;
                        switch (Type.GetTypeCode(memberMeta.BaseType))
                        {
                            case TypeCode.UInt64: defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.UInt64); break;
                            case TypeCode.UInt32: defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.UInt32); break;
                            case TypeCode.UInt16: defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.UInt16); break;
                            case TypeCode.Byte:   defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.UInt8);  break;
                            case TypeCode.Int64:  defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.Int64);  break;
                            case TypeCode.Int32:  defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.Int32);  break;
                            case TypeCode.Int16:  defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.Int16);  break;
                            case TypeCode.SByte:  defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.Int8);   break;
                            case TypeCode.Single: defaultValueExpr = Expression.Constant(memberMeta.CompressionData.CommonData.Float);  break;
                            default:
                                throw new InvalidOperationException();
                        }
                        expressionList.Add(Expression.Assign(memberAccessExpr, defaultValueExpr));
                        break;
                    }
                    default:
                        throw new NotImplementedException();
                }
            }

            expressionList.Add(valueExpr);

            var expressionBlock = Expression.Block(new[] { valueExpr, scrubberExpr }, expressionList);
            var lambda = Expression.Lambda<Func<BinaryReader, TValue>>(expressionBlock, readerExpr);
            return lambda.Compile();
        }

        public override Action<TKey, TValue, ICommonTable<TKey, TValue>, BinaryReader> CommonTableDeserializer
        {
            get
            {
                if (_commonTableDeserializer != null)
                    return _commonTableDeserializer;

                if (Storage is WDC1Reader<TKey, TValue> wdcReader)
                    return _commonTableDeserializer = GenerateCommonBlockReader(Storage as WDC1Reader<TKey, TValue>);

                throw new InvalidOperationException();
            }
        }
        
        private Action<TKey, TValue, ICommonTable<TKey, TValue>, BinaryReader> GenerateCommonBlockReader(WDC1Reader<TKey, TValue> reader)
        {
            var expressionList = new List<Expression>();

            var readerExpr = Expression.Parameter(typeof(BinaryReader), "reader");
            var keyExpr = Expression.Parameter(typeof(TKey), "key");
            var valueExpr = Expression.Parameter(typeof(TValue), "value");
            var commonTableExpr = Expression.Parameter(typeof(ICommonTable<TKey, TValue>), "commonTable");

            var positionExpr = Expression.MakeMemberAccess(readerExpr, MemberProvider.BinaryReaderPosition);

            var valueOffsetExpr = Expression.Variable(typeof(long), "valueOffset");

            var memberIndex = 0;
            foreach (var memberMeta in reader.TypeMembers)
            {
                if (reader.Header.IndexTable.Exists && memberMeta.MemberInfo.GetCustomAttribute<IndexAttribute>() != null)
                    continue;
                
                var memberAccessExpr = Expression.MakeMemberAccess(valueExpr, memberMeta.MemberInfo);
                if (memberMeta.Compression == MemberCompression.CommonData)
                {
                    var readValueExpr = GetBaseReader(memberMeta, readerExpr);

                    var arraySize = memberMeta.GetArraySize();

                    var offsetExpression = Expression.Call(commonTableExpr,
                        typeof(ICommonTable<TKey, TValue>).GetMethod("GetMemberOffset", BindingFlags.Public | BindingFlags.Instance),
                        new Expression[] { Expression.Constant(memberIndex), keyExpr });
    
                    expressionList.Add(Expression.Assign(valueOffsetExpr, offsetExpression));
                    expressionList.Add(Expression.IfThen(Expression.GreaterThan(valueOffsetExpr, Expression.Constant(0L)),
                        Expression.Block(
                            Expression.Assign(positionExpr, valueOffsetExpr),
                            Expression.Assign(memberAccessExpr, readValueExpr))));
                }

                ++memberIndex;
            }

            if (expressionList.Count == 0)
                throw new InvalidOperationException();

            var body = Expression.Block(new[] { valueOffsetExpr }, expressionList);
            var block = Expression.Lambda<Action<TKey, TValue, ICommonTable<TKey, TValue>, BinaryReader>>(body, keyExpr, valueExpr, commonTableExpr, readerExpr);

            _commonTableDeserializer = block.Compile();
            return _commonTableDeserializer;
        }

        protected override Expression GetBaseReader(FieldMetadata fieldInfo, Expression callTarget)
        {
            if ((fieldInfo.BitSize % 8) != 0)
            {
                var bitReadCall = Expression.Call(callTarget, typeof(BinaryReader).GetMethod("ReadBits", new[] { typeof(int) }), Expression.Constant(fieldInfo.BitSize));

                if (fieldInfo.BaseType == typeof(string))
                    throw new NotImplementedException();

                return bitReadCall;
            }

            return base.GetBaseReader(fieldInfo, callTarget);
        }
    }
}
