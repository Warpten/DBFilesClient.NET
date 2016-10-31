using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBFilesClient.NET.WDBC
{
    internal sealed class Reader<T> : Reader where T : class, new()
    {
        internal Reader(Stream fileStream) : base(fileStream)
        {
        }

        internal override void Load()
        {
            // We get to this through the Factory, meaning we already read the signature...
            var recordCount = ReadInt32();
            if (recordCount == 0)
                return;
            BaseStream.Position += 4; // Counts arrays
            var recordSize = ReadInt32();
            var stringBlockSize = ReadInt32();

            FileHeader.HasStringTable = stringBlockSize != 0;

            StringTableOffset = BaseStream.Length - stringBlockSize;

            // Generate the record loader function now.
            _loader = GenerateRecordLoader();

            for (var i = 0; i < recordCount; ++i)
            {
                LoadRecord();
                BaseStream.Position += recordSize;
            }
        }

        private void LoadRecord()
        {
            var key = ReadInt32();
            BaseStream.Position -= 4;
            TriggerRecordLoaded(key, _loader(this));
        }

        private Func<Reader<T>, T> _loader;

        private static Func<Reader<T>, T> GenerateRecordLoader()
        {
            var expressions = new List<Expression>();

            // Create a parameter expression that holds the argument type.
            var readerExpr = Expression.Parameter(typeof(Reader<T>), "reader");

            // Create a variable expression that holds the return type.
            var resultExpr = Expression.Variable(typeof(T), typeof(T).Name + "Value");

            // Instantiate the return value.
            expressions.Add(Expression.Assign(resultExpr, Expression.New(typeof(T))));

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
                    fieldObjectCtor = fieldType.GetConstructor(new[] { fieldType.BaseType?.GetGenericArguments()[0] });
                    if (fieldObjectCtor == null)
                        throw new InvalidStructureException($"{fieldType.Name} requires a constructor.");
                }

                Expression callExpression;
                if (typeCode != TypeCode.Object)
                {
                    var callVirt = GetPrimitiveLoader(fieldInfo);

                    callExpression = Expression.Call(readerExpr, callVirt);
                }
                else
                {
                    // ReSharper disable once PossibleNullReferenceException
                    var wrappedType = fieldObjectCtor.GetParameters()[0].ParameterType;
                    var callVirt = GetPrimitiveLoader(wrappedType);

                    callExpression = Expression.New(fieldObjectCtor, Expression.Convert(Expression.Call(readerExpr, callVirt), wrappedType));
                }

                if (!fieldInfo.FieldType.IsArray)
                {
                    expressions.Add(Expression.Assign(Expression.MakeMemberAccess(resultExpr, fieldInfo), Expression.Convert(callExpression, fieldType)));
                }
                else
                {
                    var marshalAttribute = fieldInfo.GetCustomAttribute<MarshalAsAttribute>();
                    if (marshalAttribute == null)
                        throw new InvalidStructureException($"Field {fieldInfo.Name} is an array but misses MarshalAsAttribute!");

                    var exitLabelExpr = Expression.Label();
                    var itrExpr = Expression.Variable(typeof(int), "itr");
                    expressions.Add(Expression.Block(
                        new[] { itrExpr },
                        // ReSharper disable once AssignNullToNotNullAttribute
                        Expression.Assign(
                            Expression.MakeMemberAccess(resultExpr, fieldInfo),
                            Expression.New(fieldInfo.FieldType.GetConstructor(new[] { typeof(int) }),
                                Expression.Constant(marshalAttribute.SizeConst))
                            ),

                        Expression.Assign(itrExpr, Expression.Constant(0)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                Expression.LessThan(itrExpr, Expression.Constant(marshalAttribute.SizeConst)),
                                Expression.Assign(
                                    Expression.ArrayAccess(Expression.MakeMemberAccess(resultExpr, fieldInfo),
                                        Expression.PostIncrementAssign(itrExpr)),
                                    Expression.Convert(callExpression, fieldType)),
                                Expression.Break(exitLabelExpr)),
                            exitLabelExpr)
                        ));
                }
            }
            expressions.Add(resultExpr);

            var expressionBlock = Expression.Block(new[] { resultExpr }, expressions);
            return Expression.Lambda<Func<Reader<T>, T>>(expressionBlock, readerExpr).Compile();
        }
    }
}
