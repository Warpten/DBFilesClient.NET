using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBFilesClient.NET.UnitTests
{
    public class DBFileNameAttribute : Attribute
    {
        public string Filename { get; }

        public DBFileNameAttribute(string fileName)
        {
            Filename = fileName;
        }
    }
}
