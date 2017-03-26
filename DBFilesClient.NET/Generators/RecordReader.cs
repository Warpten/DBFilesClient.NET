using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient.NET.Generators
{
    internal sealed class RecordReader<T> where T : class, new()
    {
        public static Func<Reader<T>, T> Generate()
        {
            throw new NotImplementedException();
        }
    }
}
