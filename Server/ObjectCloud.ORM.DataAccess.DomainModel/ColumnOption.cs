using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.ORM.DataAccess.DomainModel
{
    [Flags]
    public enum ColumnOption : int
    {
        None = 0x00,
        Indexed = 0x01,
        Unique = 0x02
    }
}
