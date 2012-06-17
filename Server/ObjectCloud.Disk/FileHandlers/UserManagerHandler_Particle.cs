// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

using ExtremeSwank.OpenId;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
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
        // TODO
        // A lot of the logic in this file that's not tied to the DB should move someplace else so that independent implementations can use it



        /// <summary>
        /// Gets information about recipients for sending a notification
        /// </summary>
        /// <param name="openIdOrWebFinger"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        public void GetEndpointInfos(
            IUserOrGroup sender,
            bool forceRefresh, 
            IEnumerable<string> recipientIdentitiesArg,
            ParticleEndpoint particleEndpoint,
            GenericArgument<EndpointInfo> callback,
            GenericArgument<IEnumerable<string>> errorCallback,
            GenericArgument<Exception> exceptionCallback)
        {
            HashSet<string> recipientIdentities = new HashSet<string>(recipientIdentitiesArg);

            long outstandingRequests = recipientIdentities.Count;

            LockFreeQueue<Endpoints> loadedEndpoints = new LockFreeQueue<Endpoints>();

            GenericArgument<Endpoints> endpointLoaded = delegate(Endpoints endpoints)
            {
                loadedEndpoints.Enqueue(endpoints);

                if (0 == Interlocked.Decrement(ref outstandingRequests))
                    GetRecipientInfos(sender, forceRefresh, recipientIdentities, loadedEndpoints, particleEndpoint, callback, errorCallback, exceptionCallback);
            };

            GenericArgument<Exception> endpointException = delegate(Exception e)
            {
                if (0 == Interlocked.Decrement(ref outstandingRequests))
                    GetRecipientInfos(sender, forceRefresh, recipientIdentities, loadedEndpoints, particleEndpoint, callback, errorCallback, exceptionCallback);
            };

            foreach (string openIdOrWebFinger in recipientIdentities)
                Endpoints.GetEndpoints(openIdOrWebFinger, forceRefresh, endpointLoaded, endpointException);
        }

        /// <summary>
        /// Continues to get more information about recipients after all information about endpoints is loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="recipientIdentities"></param>
        /// <param name="loadedEndpoints"></param>
        /// <param name="callback"></param>
        private void GetRecipientInfos(
            IUserOrGroup sender,
            bool forceRefresh,
            HashSet<string> recipientIdentities, 
            LockFreeQueue<Endpoints> loadedEndpoints,
            ParticleEndpoint particleEndpoint,
            GenericArgument<EndpointInfo> callback,
            GenericArgument<IEnumerable<string>> errorCallback,
            GenericArgument<Exception> exceptionCallback)
        {
            try
            {
                // All of the unique particle endpoints, with the recipients at each
                Dictionary<string, List<string>> recipientsAtEndpoints = new Dictionary<string, List<string>>();
                Dictionary<string, string> establishTrustEndpoints = new Dictionary<string, string>();
                Dictionary<string, string> requestedEndpoints = new Dictionary<string, string>();

                Endpoints particleEndpoints;
                while (loadedEndpoints.Dequeue(out particleEndpoints))
                {
                    string endpoint;
                    if (particleEndpoints.TryGetEndpoint(particleEndpoint, out endpoint))
                    {
                        List<string> users;
                        if (recipientsAtEndpoints.TryGetValue(particleEndpoints[ParticleEndpoint.ReceiveNotification], out users))
                            users.Add(particleEndpoints.OpenIdOrWebFinger);
                        else
                        {
                            users = new List<string>();
                            users.Add(particleEndpoints.OpenIdOrWebFinger);

                            recipientsAtEndpoints[particleEndpoints[ParticleEndpoint.ReceiveNotification]] = users;
                            establishTrustEndpoints[particleEndpoints[ParticleEndpoint.ReceiveNotification]] = particleEndpoints[ParticleEndpoint.EstablishTrust];
                            requestedEndpoints[particleEndpoints[ParticleEndpoint.ReceiveNotification]] = particleEndpoints[particleEndpoint];
                        }
                    }
                }

                if (!forceRefresh)
                {
                    // Load for situations where trust is already established
                    // copy is to avoid locked the database
					this.persistedUserManagerData.Read(userManagerData =>
					{
						var recipientUser = userManagerData.GetUser(sender.Id);
						
						foreach (var recipientAndToken in recipientUser.receiveNotificationEndpointsBySenderToken.Where(
							r => recipientsAtEndpoints.ContainsKey(r.Value)))
						{
							var receiveNotificationEndpoint = recipientAndToken.Value;
							var senderToken = recipientAndToken.Key;
							
	                        string endpoint;
	                        if (requestedEndpoints.TryGetValue(receiveNotificationEndpoint, out endpoint))
	                        {
                        		var recipientInfo = new EndpointInfo()
								{
									RecipientIdentities = recipientsAtEndpoints[receiveNotificationEndpoint],
	                            	Endpoint = endpoint,
	                            	SenderToken = senderToken
								};
								
	                            recipientsAtEndpoints.Remove(receiveNotificationEndpoint);
	
	                            callback(recipientInfo);
	                        }
						}
					});
                }

                // For situations where trust isn't established, establish trust and then use the callback
                foreach (KeyValuePair<string, List<string>> endpointAndRecipients in recipientsAtEndpoints)
                    GetRecipientInfos(
                        sender,
                        endpointAndRecipients.Key,
                        establishTrustEndpoints[endpointAndRecipients.Key],
                        endpointAndRecipients.Value,
                        requestedEndpoints[endpointAndRecipients.Key],
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
            string requestedEndpoint,
            GenericArgument<EndpointInfo> callback,
            GenericArgument<IEnumerable<string>> errorCallback)
        {
            this.BeginEstablishTrust(sender, receiveNotificationEndpoint, establishTrustEndpoint, delegate(string senderToken)
            {
                var recipientInfo = new EndpointInfo()
				{
                	RecipientIdentities = recipients,
                	Endpoint = requestedEndpoint,
                	SenderToken = senderToken
				};
				
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
            if (null == EstablishTrustDataTimer)
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

						this.persistedUserManagerData.Write(userManagerData =>
						{
							var user = userManagerData.GetUser(sender.Id);
						
							string oldSenderToken;
							if (user.receiveNotificationSenderTokensByEndpoint.TryGetValue(receiveNotificationEndpoint, out oldSenderToken))
								user.receiveNotificationEndpointsBySenderToken.Remove(oldSenderToken);
						
							user.receiveNotificationEndpointsBySenderToken[senderToken] = receiveNotificationEndpoint;
							user.receiveNotificationSenderTokensByEndpoint[receiveNotificationEndpoint] = senderToken;
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
                new KeyValuePair<string, string>("loginURLRedirect", "redirect"),
                GenerateSecurityTimestamp());
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
			this.persistedUserManagerData.Write(userManagerData =>
			{
				Sender sender;
				if (userManagerData.sendersByIdentity.TryGetValue(senderIdentity, out sender))
					userManagerData.sendersByToken.Remove(sender.token);
				
				sender = new Sender()
				{
                    loginURL = loginUrl,
                    loginURLOpenID = loginUrlOpenID,
                    loginURLRedirect = loginUrlRedirect,
                    loginURLWebFinger = loginUrlWebFinger,
                    token = senderToken,
					identity = senderIdentity
				};
				
				userManagerData.sendersByToken[senderToken] = sender;
				userManagerData.sendersByIdentity[senderIdentity] = sender;
			});

            /*if (null != DatabaseConnection.Sender.SelectSingle(Sender_Table.identity == senderIdentity))
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
                });*/
        }

        /// <summary>
        /// Responds with the endpoints
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="callback"></param>
        public void GetEndpoints(string identity, GenericArgument<IEndpoints> callback, GenericArgument<Exception> errorCallback)
        {
            Endpoints.GetEndpoints(
                identity,
                false,
                delegate(Endpoints endpoints)
                {
                    callback(endpoints);
                },
                delegate(Exception e)
                {
                    errorCallback(e);
                });
        }

        public bool TryGetSenderIdentity(string senderToken, out string senderIdendity)
        {
			bool found = false;
			
			senderIdendity = this.persistedUserManagerData.Read(userManagerData =>
			{
				Sender sender;
				if (userManagerData.sendersByToken.TryGetValue(senderToken, out sender))
				{
					found = true;
					return sender.identity;
				}
				else
					return null;
			});
			
			return found;
        }

        public void SendNotification(
            IUser sender,
            bool forceRefresh,
            IEnumerable<IUser> recipients,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData,
            int maxRetries,
            TimeSpan transportErrorDelay)
        {
            List<string> recipientIdentities = new List<string>();
            List<IUser> localRecipients = new List<IUser>();

            // Seperate local and remote users

            foreach (IUser user in recipients)
                if (user.Local)
                    localRecipients.Add(user);
                else
                    recipientIdentities.Add(user.Identity);

            // Start sending notifications asyncronously to remote users
            if (recipientIdentities.Count > 0)
                SendNotification(sender, forceRefresh, recipientIdentities, objectUrl, summaryView, documentType, verb, changeData, maxRetries, transportErrorDelay);

            // Send local notifications on the threadpool
            if (localRecipients.Count > 0)
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    string linkedSenderIdentity = null;

                    if ("link" == verb)
                        try
                        {
                            Dictionary<string, object> parsedChangeData = JsonReader.Deserialize<Dictionary<string, object>>(changeData);
                            linkedSenderIdentity = parsedChangeData["ownerIdentity"].ToString();
                        }
                        catch (Exception e)
                        {
                            log.Error("Exception getting linked sender identity from change data while sending a local notification", e);
                        }

                    foreach (IUser user in localRecipients)
                        try
                        {
                            user.UserHandler.ReceiveNotification(sender.Identity, objectUrl, summaryView, documentType, verb, changeData, linkedSenderIdentity);
                        }
                        catch (Exception e)
                        {
                            log.Error("Error sending notification to " + user.Name, e);
                        }
                });
        }

        /// <summary>
        /// Sends notifications to recipients on other servers
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="forceRefresh"></param>
        /// <param name="recipientIdentities"></param>
        /// <param name="objectUrl"></param>
        /// <param name="summaryView"></param>
        /// <param name="documentType"></param>
        /// <param name="verb"></param>
        /// <param name="changeData"></param>
        /// <param name="maxRetries"></param>
        /// <param name="transportErrorDelay"></param>
        private void SendNotification(
            IUser sender,
            bool forceRefresh,
            IEnumerable<string> recipientIdentities,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData,
            int maxRetries,
            TimeSpan transportErrorDelay)
        {
            GetEndpointInfos(
                sender,
                forceRefresh,
                recipientIdentities,
                ParticleEndpoint.ReceiveNotification,
                delegate(EndpointInfo recipientInfo)
                {
                    SendNotification(
                        sender,
                        recipientInfo,
                        objectUrl,
                        summaryView,
                        documentType,
                        verb,
                        changeData,
                        maxRetries,
                        transportErrorDelay);
                },
                delegate(IEnumerable<string> erroniousRecipientIdentities)
                {
                    if (maxRetries > 0)
                    {
                        log.Warn(
                            "Could not get recipient information for " + StringGenerator.GenerateCommaSeperatedList(erroniousRecipientIdentities) + ", retrying");

                        ThreadPool.QueueUserWorkItem(delegate(object state)
                        {
                            Thread.Sleep(transportErrorDelay);

                            SendNotification(
                                sender,
                                true,
                                erroniousRecipientIdentities,
                                objectUrl,
                                summaryView,
                                documentType,
                                verb,
                                changeData,
                                maxRetries - 1,
                                transportErrorDelay);
                        });
                    }
                    else
                        log.Warn(
                            "Could not get recipient information for " + StringGenerator.GenerateCommaSeperatedList(erroniousRecipientIdentities) + ", no more retries left");
                },
                delegate(Exception e)
                {
                    log.Warn("Unhandled exception when sending a notification for " + objectUrl + ", no more information is known", e);
                });
		}

        public void SendNotification(
            IUser sender,
            EndpointInfo recipientInfo,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData,
            int maxRetries,
            TimeSpan transportErrorDelay)
        {
            HttpWebClient webClient = new HttpWebClient();

            GenericVoid retry = delegate()
            {
                if (maxRetries > 0)
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        Thread.Sleep(transportErrorDelay);

                        SendNotification(
                            sender,
                            recipientInfo,
                            objectUrl,
                            summaryView,
                            documentType,
                            verb,
                            changeData,
                            maxRetries - 1,
                            transportErrorDelay);
                    });
            };

            webClient.BeginPost(
                recipientInfo.Endpoint,
                delegate(HttpResponseHandler response)
                {
                    string responseString = response.AsString();

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    // success!
                    { }

                    else if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                        // errors
                        log.Warn("Errors occured when sending a notification: " + responseString);

                    else if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed && "senderToken" == responseString)
                    {
                        SendNotification(
                            sender,
                            true,
                            recipientInfo.RecipientIdentities,
                            objectUrl,
                            summaryView,
                            documentType,
                            verb,
                            changeData,
                            maxRetries,
                            transportErrorDelay);
                    }
                    else
                        retry();
                },
                delegate(Exception e)
                {
                },
                new KeyValuePair<string, string>("senderToken", recipientInfo.SenderToken),
                new KeyValuePair<string, string>("recipients", JsonWriter.Serialize(recipientInfo.RecipientIdentities)),
                new KeyValuePair<string, string>("objectUrl", objectUrl),
                new KeyValuePair<string, string>("summaryView", summaryView),
                new KeyValuePair<string, string>("documentType", documentType),
                new KeyValuePair<string, string>("verb", verb),
                new KeyValuePair<string, string>("changeData", changeData),
                GenerateSecurityTimestamp());
        }

        public RapidLoginInfo GetRapidLoginInfo(string senderIdentity)
        {
			return this.persistedUserManagerData.Read(userManagerData =>
			{
				Sender sender;
				if (!userManagerData.sendersByIdentity.TryGetValue(senderIdentity, out sender))
                	throw new ParticleException("No information is known about " + senderIdentity);
	
    	        return new RapidLoginInfo()
				{
            		LoginUrl = sender.loginURL,
            		LoginUrlOpenID = sender.loginURLOpenID,
            		LoginUrlRedirect = sender.loginURLRedirect,
            		LoginUrlWebFinger = sender.loginURLWebFinger
				};
			});
        }

        public void DeleteAllEstablishedTrust(IUserOrGroup userOrGroup)
        {
			this.persistedUserManagerData.Read(userManagerData =>
			{
				var user = userManagerData.GetUser(userOrGroup.Id);
				user.receiveNotificationEndpointsBySenderToken.Clear();
				user.receiveNotificationSenderTokensByEndpoint.Clear();
			});
        }

        /// <summary>
        /// The unix epoch
        /// </summary>
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Returns a security timestamp
        /// </summary>
        /// <returns></returns>
        public static KeyValuePair<string, string> GenerateSecurityTimestamp()
        {
            string securityTimestamp = (DateTime.UtcNow - UnixEpoch).TotalDays.ToString("R");
            return new KeyValuePair<string, string>("securityTimestamp", securityTimestamp);
        }
    }
}
