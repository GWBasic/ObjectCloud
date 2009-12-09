using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.SQLite
{
    public class CantOpenDatabaseException : Exception
    {
        public CantOpenDatabaseException(string message) : base(message) { }
    }
}
