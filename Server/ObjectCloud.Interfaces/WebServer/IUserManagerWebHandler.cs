// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Xml;

using Common.Logging;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.WebServer
{
    public interface IUserManagerWebHandler
    {
        IWebResults UserConfirmLink(
            IWebConnection webConnection,
            string objectUrl,
            string ownerIdentity,
            string linkedSummaryView,
            string linkUrl,
            string linkDocumentType,
            string recipients,
            string redirectUrl,
            string linkID,
            string password,
            string remember);
    }
}
