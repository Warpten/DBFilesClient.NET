using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient.NET.Expressions
{
    public static class Expressions
    {
        public static ParameterExpression<T> Parameter<T>(string name)
        {
            return new ParameterExpression<T>(name);
        }

        public static ConstantExpression<T> Constant<T>(T value) =>
            return new ConstantExpression<T>(ValueType);
    }

    public abstract class Expression<T>
    {
        internal System.Linq.Expressions.Expression Node { get; set; }

        internal List<System.Linq.Expressions.Expression> Children { get; set; } = new List<System.Linq.Expressions.Expression>();

        public abstract Expression<T> Add<U>(Expression<U> node);
        public abstract Expression<T> Substract<U>(Expression<U> node);
        public abstract Expression<T> Divide<U>(Expression<U> node);
    }
}
