// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// The states that a notification can be
    /// </summary>
    public enum NotificationState : int
    {
        /// <summary>
        /// The notification's been read
        /// </summary>
        read = 0,

        /// <summary>
        /// The notification is unread
        /// </summary>
        unread = 1
    }
}
