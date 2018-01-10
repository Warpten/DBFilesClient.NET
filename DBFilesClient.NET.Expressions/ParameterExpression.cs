using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient.NET.Expressions
{
    public sealed class ParameterExpression<T> : Expression<T>
    {
        internal ParameterExpression()
        {
            Node = System.Linq.Expressions.Expression.Parameter(typeof(T));
        }

        internal ParameterExpression(string name)
        {
            Node = System.Linq.Expressions.Expression.Parameter(typeof(T), name);
        }
    }

    public sealed class ConstantExpression<T> : Expression<T>
    {
        private T _nodeValue;

        internal ConstantExpression(T value)
        {
            _nodeValue = value;
            Node = System.Linq.Expressions.Expression.Constant(value, typeof(T));
        }

        public override Expression<T> Add<U>(Expression<U> node)
        {
            if (node is ConstantExpression<U> constantNode)
            {
                return System.Linq.Expressions.Expression.Add(Node, node.Node);
            }
        }

        public override Expression<T> Divide<U>(Expression<U> node)
        {
            throw new NotImplementedException();
        }

        public override Expression<T> Substract<U>(Expression<U> node)
        {
            throw new NotImplementedException();
        }
    }
}
