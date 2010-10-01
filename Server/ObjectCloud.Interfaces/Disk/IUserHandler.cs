// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Interfaces.Disk
{
    /// <summary>
    /// User handler
    /// </summary>
    public interface IUserHandler : INameValuePairsHandler
    {
        /// <summary>
        /// The user's name
        /// </summary>
        string Name { get; set; }

		//// <value>
		/// Returns the OpenID Identity 
		/// </value>
        string Identity { get; }

        /// <summary>
        /// Receives a notification
        /// </summary>
        void ReceiveNotification(
            string senderIdentity,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData,
            string linkedSenderIdentity);

        /// <summary>
        /// Gets notifications
        /// </summary>
        /// <param name="newestNotificationId"></param>
        /// <param name="oldestNotificationId"></param>
        /// <param name="maxNotifications"></param>
        /// <param name="objectUrl"></param>
        /// <param name="sender"></param>
        /// <param name="desiredValues"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        IEnumerable<Dictionary<NotificationColumn, object>> GetNotifications(
            long? newestNotificationId,
            long? oldestNotificationId,
            long? maxNotifications,
            string objectUrl,
            string sender,
            List<NotificationColumn> desiredValues);

        /// <summary>
        /// Occurs when a notification is recieved
        /// </summary>
        event EventHandler<IUserHandler, EventArgs<Dictionary<NotificationColumn, object>>> NotificationRecieved;
	}
}
