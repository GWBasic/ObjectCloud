// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using ObjectCloud.Common;

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
        /// Sends a notification
        /// </summary>
        /// <param name="openId">The OpenId to send the notification to</param>
        /// <param name="objectUrl">The object that this notification applies to.  This must be the same domain as the OpenId</param>
        /// <param name="title">The document's title</param>
        /// <param name="documentType">The document type</param>
        /// <param name="messageSummary">The message summary.  This is displayed in the user's notifications viewer GUI</param>
        /// <param name="changeData">The changeData</param>
        /// <param name="forceRefreshSenderToken">true to force a refresh of the sender token</param>
        /// <param name="forceRefreshEndpoints">true to force a refresh of the endpoints</param>
        /// <param name="maxRetries">The maximum number of times to retry</param>
        /// <param name="transportErrorDelay">The amount of time to wait before a retry when there is a transport error</param>
        void SendNotification(
            string openId,
            string objectUrl,
            string title,
            string documentType,
            string messageSummary,
            string changeData);
        
        /// <summary>
        /// Sends a notification
        /// </summary>
        /// <param name="openId">The OpenId to send the notification to</param>
        /// <param name="objectUrl">The object that this notification applies to.  This must be the same domain as the OpenId</param>
        /// <param name="title">The document's title</param>
        /// <param name="documentType">The document type</param>
        /// <param name="messageSummary">The message summary.  This is displayed in the user's notifications viewer GUI</param>
        /// <param name="changeData">The changeData</param>
        /// <param name="forceRefreshSenderToken">true to force a refresh of the sender token</param>
        /// <param name="forceRefreshEndpoints">true to force a refresh of the endpoints</param>
        /// <param name="maxRetries">The maximum number of times to retry</param>
        /// <param name="transportErrorDelay">The amount of time to wait before a retry when there is a transport error</param>
        void SendNotification(
            string openId,
            string objectUrl,
            string title,
            string documentType,
            string messageSummary,
            string changeData,
            bool forceRefreshSenderToken,
            bool forceRefreshEndpoints,
            int maxRetries,
            TimeSpan transportErrorDelay);

        /// <summary>
        /// Gets the senderToken for the openId
        /// </summary>
        /// <param name="openId"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        string GetSenderToken(string openId, bool forceRefresh);

        /// <summary>
        /// Returns the OpenId associated with the sender token
        /// </summary>
        /// <param name="senderToken"></param>
        /// <returns></returns>
        /// <exception cref="ParticleException.BadToken">Thrown if the senderToken is unknown</exception>
        string GetOpenIdFromSenderToken(string senderToken);

        /// <summary>
        /// Establishes trust with the sender using the sent token
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="token"></param>
        void EstablishTrust(string sender, string token);

        /// <summary>
        /// Assists in responding when establishing trust
        /// </summary>
        /// <param name="token"></param>
        /// <param name="senderToken"></param>
        void RespondTrust(
            string token,
            string senderToken);

        /// <summary>
        /// Receives a notification
        /// </summary>
        /// <param name="senderToken"></param>
        /// <param name="objectUrl"></param>
        /// <param name="title"></param>
        /// <param name="documentType"></param>
        /// <param name="messageSummary"></param>
        /// <param name="changeData"></param>
        /// <exception cref="ParticleException.BadToken">Thrown if the senderToken is unknown</exception>
        void ReceiveNotification(
            string senderToken,
            string objectUrl,
            string title,
            string documentType,
            string messageSummary,
            string changeData);

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
