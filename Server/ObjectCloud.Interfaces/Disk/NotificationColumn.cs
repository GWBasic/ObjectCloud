// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// The valid columns when getting a notification
    /// </summary>
    public enum NotificationColumn
    {
        notificationId,
        timeStamp,
        state,
        sender,
        objectUrl,
        title,
        documentType,
        messageSummary,
        changeData
    }
}
