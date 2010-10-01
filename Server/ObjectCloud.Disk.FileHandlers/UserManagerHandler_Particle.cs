// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

using ExtremeSwank.OpenId;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.DataAccess.UserManager;
using ObjectCloud.Disk.Implementation;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.ORM.DataAccess.WhereConditionals;
using ObjectCloud.Disk.FileHandlers.Particle;

namespace ObjectCloud.Disk.FileHandlers
{
    public partial class UserManagerHandler
    {
        /// <summary>
        /// Gets information about recipients for sending a notification
        /// </summary>
        /// <param name="openIdOrWebFinger"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        public void GetRecipientInfos(
            IUserOrGroup sender,
            bool forceRefresh, 
            IEnumerable<string> openIdOrWebFingersEnum, 
            GenericArgument<RecipientInfo> callback,
            GenericArgument<IEnumerable<string>> errorCallback,
            GenericArgument<Exception> exceptionCallback)
        {
            Set<string> openIdOrWebFingers = new Set<string>(openIdOrWebFingersEnum);

            long outstandingRequests = openIdOrWebFingers.Count;

            LockFreeQueue<Endpoints> loadedEndpoints = new LockFreeQueue<Endpoints>();

            GenericArgument<Endpoints> endpointLoaded = delegate(Endpoints endpoints)
            {
                loadedEndpoints.Enqueue(endpoints);

                if (0 == Interlocked.Decrement(ref outstandingRequests))
                    GetRecipientInfos(sender, openIdOrWebFingers, loadedEndpoints, callback, errorCallback, exceptionCallback);
            };

            GenericArgument<Exception> endpointException = delegate(Exception e)
            {
                if (0 == Interlocked.Decrement(ref outstandingRequests))
                    GetRecipientInfos(sender, openIdOrWebFingers, loadedEndpoints, callback, errorCallback, exceptionCallback);
            };

            foreach (string openIdOrWebFinger in openIdOrWebFingers)
                Endpoints.GetEndpoints(openIdOrWebFinger, forceRefresh, endpointLoaded, endpointException);
        }

        /// <summary>
        /// Continues to get more information about recipients after all information about endpoints is loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="openIdOrWebFingers"></param>
        /// <param name="loadedEndpoints"></param>
        /// <param name="callback"></param>
        private void GetRecipientInfos(
            IUserOrGroup sender, 
            Set<string> openIdOrWebFingers, 
            LockFreeQueue<Endpoints> loadedEndpoints,
            GenericArgument<RecipientInfo> callback,
            GenericArgument<IEnumerable<string>> errorCallback,
            GenericArgument<Exception> exceptionCallback)
        {
            try
            {
                // All of the unique particle.recieveNotification endpoints, with the recipients at each
                Dictionary<string, List<string>> recipientsAtEndpoints = new Dictionary<string, List<string>>();
                Dictionary<string, string> establishTrustEndpoints = new Dictionary<string, string>();

                Endpoints endpoints;
                while (loadedEndpoints.Dequeue(out endpoints))
                {
                    string recieveNotificationEndpoint = endpoints["receiveNotification"];

                    List<string> users;
                    if (recipientsAtEndpoints.TryGetValue(recieveNotificationEndpoint, out users))
                        users.Add(endpoints.OpenIdOrWebFinger);
                    else
                    {
                        users = new List<string>();
                        users.Add(endpoints.OpenIdOrWebFinger);

                        recipientsAtEndpoints[recieveNotificationEndpoint] = users;
                        establishTrustEndpoints[recieveNotificationEndpoint] = endpoints["establishTrust"];
                    }
                }

                // Load for situations where trust is already established
                foreach (IRecipient_Readable recipient in DatabaseConnection.Recipient.Select(
                    Recipient_Table.receiveNotificationEndpoint.In(recipientsAtEndpoints.Keys) &
                    Recipient_Table.userID == sender.Id))
                {
                    RecipientInfo recipientInfo = new RecipientInfo();
                    recipientInfo.OpenIdOrWebFingers = recipientsAtEndpoints[recipient.receiveNotificationEndpoint];
                    recipientInfo.RecieveNotificationEndpoint = recipient.receiveNotificationEndpoint;
                    recipientInfo.SenderToken = recipient.senderToken;

                    recipientsAtEndpoints.Remove(recipient.receiveNotificationEndpoint);

                    callback(recipientInfo);
                }

                // For situations where trust isn't established, establish trust and then use the callback
                foreach (KeyValuePair<string, List<string>> endpointAndRecipients in recipientsAtEndpoints)
                    GetRecipientInfos(
                        sender,
                        endpointAndRecipients.Key,
                        establishTrustEndpoints[endpointAndRecipients.Key],
                        endpointAndRecipients.Value,
                        callback,
                        errorCallback);
            }
            catch (Exception e)
            {
                exceptionCallback(e);
            }
        }

        /// <summary>
        /// Establishes trust as part of GetRecipientInfos when trust isn't established
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="receiveNotificationEndpoint"></param>
        /// <param name="recipients"></param>
        /// <param name="callback"></param>
        private void GetRecipientInfos(
            IUserOrGroup sender, 
            string receiveNotificationEndpoint,
            string establishTrustEndpoint,
            List<string> recipients,
            GenericArgument<RecipientInfo> callback,
            GenericArgument<IEnumerable<string>> errorCallback)
        {
            BeginEstablishTrust(sender, receiveNotificationEndpoint, establishTrustEndpoint, delegate(string senderToken)
            {
                RecipientInfo recipientInfo = new RecipientInfo();
                recipientInfo.OpenIdOrWebFingers = recipients;
                recipientInfo.RecieveNotificationEndpoint = receiveNotificationEndpoint;
                recipientInfo.SenderToken = senderToken;

                callback(recipientInfo);
            },
            delegate(Exception e)
            {
                log.Error(
                    string.Format("Could not establish trust between {0} and {1}", sender.Name, StringGenerator.GenerateCommaSeperatedList(recipients)),
                    e);

                errorCallback(recipients);
            });
        }

        /// <summary>
        /// Data used during the establish trust handshake
        /// </summary>
        private class EstablishTrustData
        {
            //public IUserOrGroup Sender;
            //public string ReceiveNotificationEndpoint;
            //public GenericArgument<string> Callback;
            public string SenderToken;
            public DateTime Created = DateTime.UtcNow;
        }

        /// <summary>
        /// Represents ongoing establish trust requests
        /// </summary>
        private Dictionary<string, EstablishTrustData> EstablishTrustDatasByToken = new Dictionary<string, EstablishTrustData>();

        /// <summary>
        /// Periodically cleans up outstanding establish trust request data
        /// </summary>
        private Timer EstablishTrustDataTimer = null;

        private void EstablishTrustDataCleanup(object state)
        {
            using (TimedLock.Lock(EstablishTrustDatasByToken))
                foreach (KeyValuePair<string, EstablishTrustData> tokenAndEstablishTrustData in Enumerable<KeyValuePair<string, EstablishTrustData>>.FastCopy(EstablishTrustDatasByToken))
                    if (tokenAndEstablishTrustData.Value.Created.AddMinutes(10) < DateTime.UtcNow)
                        EstablishTrustDatasByToken.Remove(tokenAndEstablishTrustData.Key);
        }

        /// <summary>
        /// Establishes trust between a user and another server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="receiveNotificationEndpoint"></param>
        /// <param name="callback"></param>
        private void BeginEstablishTrust(
            IUserOrGroup sender,
            string receiveNotificationEndpoint,
            string establishTrustEndpoint,
            GenericArgument<string> callback,
            GenericArgument<Exception> errorCallback)
        {
            // Make sure the timer is created
            if (null == EstablishTrustDatasByToken)
            {
                Timer timer = new Timer(EstablishTrustDataCleanup, null, 600000, 600000);
                if (null != Interlocked.CompareExchange<Timer>(ref EstablishTrustDataTimer, timer, null))
                    timer.Dispose();
            }

            // Get the avatar
            byte[] avatar;
            ISession session = FileHandlerFactoryLocator.SessionManagerHandler.CreateSession();

            try
            {
                IWebConnection webConnection = new BlockingShellWebConnection(
                    FileHandlerFactoryLocator.WebServer,
                    session,
                    "/Users/" + sender.Name + ".user?Method=GetAvatar",
                    null,
                    null,
                    null,
                    CallingFrom.Web,
                    WebMethod.GET);

                IWebResults webResults = webConnection.ShellTo("/Users/" + sender.Name + ".user?Method=GetAvatar");

                using (Stream stream = webResults.ResultsAsStream)
                {
                    avatar = new byte[stream.Length];
                    stream.Read(avatar, 0, avatar.Length);
                }
            }
            finally
            {
                FileHandlerFactoryLocator.SessionManagerHandler.EndSession(session.SessionId);
            }


            // Hold on to callback information for use after trust is established
            string token;
            using (TimedLock.Lock(EstablishTrustDatasByToken))
            {
                do
                    token = Convert.ToBase64String(SRandom.NextBytes(100));
                while (EstablishTrustDatasByToken.ContainsKey(token));

                EstablishTrustDatasByToken[token] = new EstablishTrustData();
            }

            HttpWebClient httpWebClient = new HttpWebClient();
            httpWebClient.BeginPost(
                establishTrustEndpoint,
                delegate(HttpResponseHandler httpResponseHandler) 
                {
                    if (httpResponseHandler.StatusCode == System.Net.HttpStatusCode.Created)
                    {
                        string senderToken;
                        
                        using (TimedLock.Lock(EstablishTrustDatasByToken))
                        {
                            senderToken = EstablishTrustDatasByToken[token].SenderToken;
                            EstablishTrustDatasByToken.Remove(token);
                        }

                        DatabaseConnection.Recipient.Insert(delegate(IRecipient_Writable recipient)
                        {
                            recipient.receiveNotificationEndpoint = receiveNotificationEndpoint;
                            recipient.senderToken = senderToken;
                            recipient.userID = sender.Id;
                        });

                        callback(senderToken);
                    }
                    else
                        errorCallback(new ParticleException.CouldNotEstablishTrust("Couldn't establish trust: " + httpResponseHandler.AsString()));
                },
                errorCallback,
                new KeyValuePair<string, string>("senderIdentity", sender.Identity),
                new KeyValuePair<string, string>("token", token),
                new KeyValuePair<string, string>("avatar", Convert.ToBase64String(avatar)),
                new KeyValuePair<string, string>("loginURL", string.Format("http://{0}/Users/UserDB?Method=OpenIDLogin", FileHandlerFactoryLocator.HostnameAndPort)),
                new KeyValuePair<string, string>("loginURLOpenID", "openid_url"),
                new KeyValuePair<string, string>("loginURLWebFinger", "openid_url"),
                new KeyValuePair<string, string>("loginURLRedirect", "redirect"));
        }

        /// <summary>
        /// Used when responding to a request to establish trust
        /// </summary>
        /// <param name="token"></param>
        /// <param name="senderToken"></param>
        public void RespondTrust(string token, string senderToken)
        {
            EstablishTrustData etd;

            using (TimedLock.Lock(EstablishTrustDatasByToken))
                if (!EstablishTrustDatasByToken.TryGetValue(token, out etd))
                    throw new DiskException(token + " is invalid");

            etd.SenderToken = senderToken;
        }

        public void EstablishTrust(
            string senderIdentity,
            string senderToken,
            string loginUrl,
            string loginUrlOpenID,
            string loginUrlWebFinger,
            string loginUrlRedirect)
        {
            if (null != DatabaseConnection.Sender.SelectSingle(Sender_Table.identity == senderIdentity))
                DatabaseConnection.Sender.Update(
                    Sender_Table.identity == senderIdentity,
                    delegate(ISender_Writable senderEntry)
                    {
                        senderEntry.loginURL = loginUrl;
                        senderEntry.loginURLOpenID = loginUrlOpenID;
                        senderEntry.loginURLRedirect = loginUrlRedirect;
                        senderEntry.loginURLWebFinger = loginUrlWebFinger;
                        senderEntry.senderToken = senderToken;
                    });
            else
                DatabaseConnection.Sender.Insert(delegate(ISender_Writable senderEntry)
                {
                    senderEntry.loginURL = loginUrl;
                    senderEntry.loginURLOpenID = loginUrlOpenID;
                    senderEntry.loginURLRedirect = loginUrlRedirect;
                    senderEntry.loginURLWebFinger = loginUrlWebFinger;
                    senderEntry.senderToken = senderToken;
                    senderEntry.identity = senderIdentity;
                });
        }

        /// <summary>
        /// Responds with the endpoint needed to respond trust
        /// </summary>
        /// <param name="openIdOrWebFinger"></param>
        /// <param name="callback"></param>
        public void GetRespondTrustEnpoint(string openIdOrWebFinger, GenericArgument<string> callback)
        {
            Endpoints.GetEndpoints(
                openIdOrWebFinger,
                false,
                delegate(Endpoints endpoints)
                {
                    callback(endpoints["respondTrust"]);
                },
                delegate(Exception e)
                {
                    callback("???");
                });
        }

        public bool TryGetSenderIdentity(string senderToken, out string senderIdendity)
        {
            ISender_Readable sender = DatabaseConnection.Sender.SelectSingle(Sender_Table.senderToken == senderToken);

            if (null == sender)
            {
                senderIdendity = null;
                return false;
            }
            else
            {
                senderIdendity = sender.identity;
                return true;
            }
        }
    }
}
