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
                new KeyValuePair<string, string>("sender", sender.Identity),
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
            {
                if (!EstablishTrustDatasByToken.TryGetValue(token, out etd))
                    throw new DiskException(token + " is invalid");

                //EstablishTrustDatasByToken.Remove(token);
            }

            /*DatabaseConnection.Recipient.Insert(delegate(IRecipient_Writable recipient)
            {
                recipient.receiveNotificationEndpoint = etd.ReceiveNotificationEndpoint;
                recipient.senderToken = senderToken;
                recipient.userID = etd.Sender.Id;
            });*/

            etd.SenderToken = senderToken;
        }

        public void EstablishTrust(
            string sender,
            string senderToken,
            string loginUrl,
            string loginUrlOpenID,
            string loginUrlWebFinger,
            string loginUrlRedirect)
        {
            if (null != DatabaseConnection.Sender.SelectSingle(Sender_Table.name == sender))
                DatabaseConnection.Sender.Update(
                    Sender_Table.name == sender,
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
                    senderEntry.name = sender;
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

        /*
            
            
            
            return GetRecipientInfo(openIdOrWebFinger, forceRefresh, Endpoints.GetEndpoints(openIdOrWebFinger));
        }

        /// <summary>
        /// Gets the senderToken for the openId
        /// </summary>
        /// <param name="openIdOrWebFinger"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        public string GetRecipientInfo(string openIdOrWebFinger, bool forceRefresh, Endpoints endpoints)
        {
            IRecipient_Readable recipient = DatabaseConnection.Recipient.SelectSingle(Recipient_Table.userID == openIdOrWebFinger);

            if (!forceRefresh)
            {
                if (null != sender)
                    if (null != sender.RecipientToken)
                        return sender.RecipientToken;
            }

            // The sender token must be loaded
            // ********************

            // First, make sure there's an entry with this openId that has a null senderToken
            if (null == sender)
                DatabaseConnection.Sender.Insert(delegate(ISender_Writable senderW)
                {
                    senderW.OpenID = openIdOrWebFinger;
                });
            else
                DatabaseConnection.Sender.Update(Sender_Table.OpenID == openIdOrWebFinger,
                    delegate(ISender_Writable senderW)
                    {
                        senderW.SenderToken = null;
                    });

            string token = null;

            // Only send a reqest if there isn't a pending request
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                int deleted = DatabaseConnection.Token.Delete(Token_Table.Created < DateTime.UtcNow.Subtract(
#if DEBUG
TimeSpan.FromSeconds(25)
#else
                    TimeSpan.FromMinutes(3)
//#endif
));
                IToken_Readable tokenR = DatabaseConnection.Token.SelectSingle(Token_Table.OpenId == openIdOrWebFinger);

                if (null == tokenR)
                {
                    // Create the token that the recipient must respond with
                    token = Convert.ToBase64String(SRandom.NextBytes(200));
                    DatabaseConnection.Token.Insert(delegate(IToken_Writable tokenW)
                    {
                        tokenW.OpenId = openIdOrWebFinger;
                        tokenW.Token = token;
                        tokenW.Created = DateTime.UtcNow;
                    });

                    transaction.Commit();
                }

                    // Else only commit if old tokens were deleted
                else if (deleted > 0)
                    transaction.Commit();
            });

            // Establishing trust is only called if a token is created.  This is performed outside the transaction for performance reasons
            if (null != token)
            {
                string establishTrustEndpoint = endpoints["establishTrust"];

                HttpWebClient httpWebClient = new HttpWebClient();
                HttpResponseHandler responseHandler = httpWebClient.Post(establishTrustEndpoint,
                    new KeyValuePair<string, string>("sender", Identity),
                    new KeyValuePair<string, string>("token", token));

                if (responseHandler.StatusCode != HttpStatusCode.Created)
                    throw new ParticleException.CouldNotEstablishTrust("EstablishTrust endpoint did not return success");
            }

            // Wait for a response
            DateTime startWaitResponse = DateTime.UtcNow;

            do
            {
                sender = DatabaseConnection.Sender.SelectSingle(Sender_Table.OpenID == openIdOrWebFinger);

                if (null != sender.RecipientToken)
                    return sender.RecipientToken;

                Thread.Sleep(1);
            } while (startWaitResponse.AddMinutes(1) > DateTime.UtcNow);

            throw new ParticleException.CouldNotEstablishTrust("Could not establish trust with " + openIdOrWebFinger);
        }*/

        /*// <summary>
        /// Returns the OpenId associated with the sender token
        /// </summary>
        /// <param name="senderToken"></param>
        /// <returns></returns>
        public string GetOpenIdFromSenderToken(string senderToken)
        {
            ISender_Readable sender = DatabaseConnection.Sender.SelectSingle(Sender_Table.SenderToken == senderToken);

            if (null != sender)
                return sender.OpenID;

            throw new ParticleException.BadToken("Unknown senderToken");
        }

        /// <summary>
        /// Assists in responding when establishing trust
        /// </summary>
        /// <param name="token"></param>
        /// <param name="senderToken"></param>
        public void RespondTrust(string token, string senderToken)
        {
            DatabaseConnection.Token.Delete(Token_Table.Created < DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(3)));
            IToken_Readable tokenR = DatabaseConnection.Token.SelectSingle(Token_Table.Token == token);

            if (null == tokenR)
                throw new ParticleException.BadToken(token + " is not a valid token");

            DatabaseConnection.Sender.Update(Sender_Table.OpenID == tokenR.OpenId,
                delegate(ISender_Writable sender)
                {
                    sender.RecipientToken = senderToken;
                });

            DatabaseConnection.Token.Delete(Token_Table.Token == token);
        }

        /// <summary>
        /// Establishes trust with the sender using the sent token
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="token"></param>
        public void EstablishTrust(string sender, string token)
        {
            Endpoints endpoints = Endpoints.GetEndpoints(sender);

            // Get the sender's endpoints
            string respondTrustEndpoint = endpoints["respondTrust"];

            // Generate the senderToken
            string senderToken = Convert.ToBase64String(SRandom.NextBytes(60));

            // Send the sender token back to the sender that wants to establish trust
            HttpWebClient httpWebClient = new HttpWebClient();
            HttpResponseHandler responseHandler = httpWebClient.Post(respondTrustEndpoint,
                new KeyValuePair<string, string>("senderToken", senderToken),
                new KeyValuePair<string, string>("token", token));

            // Make sure that the sender says its okay
            if (responseHandler.StatusCode != HttpStatusCode.Accepted)
                throw new ParticleException.CouldNotEstablishTrust("RespondTrust endpoint did not return success");

            // If it's okay, update the sender's entry
            int rowsUpdated = DatabaseConnection.Sender.Update(Sender_Table.OpenID == sender,
                delegate(ISender_Writable senderW)
                {
                    senderW.SenderToken = senderToken;
                });

            // If there were no rows updated, then create an entry on a transaction
            if (0 == rowsUpdated)
                DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
                {
                    rowsUpdated = DatabaseConnection.Sender.Update(Sender_Table.OpenID == sender,
                        delegate(ISender_Writable senderW)
                        {
                            senderW.SenderToken = senderToken;
                        });

                    if (0 == rowsUpdated)
                        DatabaseConnection.Sender.Insert(delegate(ISender_Writable senderW)
                        {
                            senderW.OpenID = sender;
                            senderW.SenderToken = senderToken;
                        });

                    transaction.Commit();
                });
        }*/
    }
}
