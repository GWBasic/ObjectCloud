// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.DataAccess.Directory
{
    public partial class File_Table
    {
        /// <summary>
        /// Returns the newest files that the user has access to
        /// </summary>
        /// <param name="userOrGroupIds"></param>
        /// <param name="maxToReturn"></param>
        /// <returns></returns>
        public abstract IEnumerable<IFile_Readable> GetNewestFiles(
            ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid> userId,
            IEnumerable<ObjectCloud.Common.ID<ObjectCloud.Interfaces.Security.IUserOrGroup, Guid>> userOrGroupIds,
            long maxToReturn);
    }
}
