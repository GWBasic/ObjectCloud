// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Xml;

using Common.Logging;
using ExtremeSwank.OpenId;
using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    public partial class UserManagerWebHandler
    {
        /// <summary>
        /// In-memory sender tokens that are awaiting storage in the database because of a pending respondTrust call
        /// </summary>
        private Set<string> PendingSenderTokens = new Set<string>();

        /// <summary>
        /// Establishes trust between a sender and this server
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="senderIdentity"></param>
        /// <param name="avatar"></param>
        /// <param name="token"></param>
        /// <param name="loginURL"></param>
        /// <param name="loginURLOpenID"></param>
        /// <param name="loginURLWebFinger"></param>
        /// <param name="loginURLRedirect"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults EstablishTrust(
            IWebConnection webConnection,
            string senderIdentity,
            string avatar,
            string token,
            string loginURL,
            string loginURLOpenID,
            string loginURLWebFinger,
            string loginURLRedirect)
        {
            // Decode the avatar, verifying that it's legitimate base64 encoded
            byte[] avatarBytes;
            try
            {
                avatarBytes = Convert.FromBase64String(avatar);
            }
            catch (Exception e)
            {
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "Malformed avatar"), e);
            }

            // Note:  The avatar should be a valid JPEG under 250kb.
            // For the sake of keeping the network running, this is not verified

            // Generate a sender token, with a sanity check for duplicates
            // TODO for the OCD:  It's statisically difficult, but this sender token isn't inserted in the database, thus
            // there's no check against duplicate in-RAM sender tokens
            string senderToken;
            bool senderTokenInMemory;
            string conflictingSenderIdentity;

            do
            {
                // First check the database for a duplicate
                do
                    senderToken = Convert.ToBase64String(SRandom.NextBytes(100));
                while (FileHandler.TryGetSenderIdentity(senderToken, out conflictingSenderIdentity));

                // Next check for a duplicate sender token that hasn't yet been written to the DB
                lock (PendingSenderTokens)
                {
                    senderTokenInMemory = PendingSenderTokens.Contains(senderToken);

                    if (!senderTokenInMemory)
                        PendingSenderTokens.Add(senderToken);
                }

            } while (senderTokenInMemory);

            GenericArgument<HttpResponseHandler> callback = delegate(HttpResponseHandler response)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    FileHandler.EstablishTrust(senderIdentity, senderToken, loginURL, loginURLOpenID, loginURLWebFinger, loginURLRedirect);

                    webConnection.SendResults(WebResults.From(Status._201_Created, "created"));

                    IUser senderUser = FileHandlerFactoryLocator.UserManagerHandler.GetOpenIdUser(senderIdentity);

                    string avatarFilename = senderUser.Id.ToString() + ".jpg";

                    IBinaryHandler avatarHandler;
                    if (ParticleAvatarsDirectory.IsFilePresent(avatarFilename))
                        avatarHandler = ParticleAvatarsDirectory.OpenFile(avatarFilename).CastFileHandler<IBinaryHandler>();
                    else
                        avatarHandler = (IBinaryHandler)ParticleAvatarsDirectory.CreateFile(avatarFilename, "image", null);

                    avatarHandler.WriteAll(avatarBytes);

                    // Remove the sender token from the list of pending sender tokens
                    lock (PendingSenderTokens)
                        PendingSenderTokens.Remove(senderToken);
                }
                else
                    webConnection.SendResults(WebResults.From(Status._400_Bad_Request, "Error from RespondTrust"));
            };

            GenericArgument<Exception> errorCallback = delegate(Exception e)
            {
                webConnection.SendResults(WebResults.From(Status._401_Unauthorized, "Could not establish trust"));
            };

            FileHandler.GetEndpoints(
                senderIdentity,
                delegate(IEndpoints endpoints)
                {
                    HttpWebClient httpWebClient = new HttpWebClient();
                    httpWebClient.BeginPost(
                        endpoints[ParticleEndpoint.RespondTrust],
                        callback,
                        errorCallback,
                        new KeyValuePair<string, string>("token", token),
                        new KeyValuePair<string, string>("senderToken", senderToken));
                },
                delegate(Exception e)
                {
                    log.Error("Can not get the endpoint to respond establishing trust from within a call to EstablishTrust", e);
                });

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        public IDirectoryHandler ParticleAvatarsDirectory
        {
            get 
            {
                if (null == _ParticleAvatarsDirectory)
                    _ParticleAvatarsDirectory = FileContainer.ParentDirectoryHandler.OpenFile("ParticleAvatars").CastFileHandler<IDirectoryHandler>();

                return _ParticleAvatarsDirectory; 
            }
        }
        private IDirectoryHandler _ParticleAvatarsDirectory = null;

        /// <summary>
        /// Handles a server's response to EstablishTrust
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="token"></param>
        /// <param name="senderToken"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults RespondTrust(IWebConnection webConnection, string token, string senderToken)
        {
            try
            {
                FileHandler.RespondTrust(token, senderToken);
                return WebResults.From(Status._202_Accepted);
            }
            catch (Exception e)
            {
                log.Error("Exception in RespondTrust", e);
                return WebResults.From(Status._400_Bad_Request);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="senderToken"></param>
        /// <param name="recipients"></param>
        /// <param name="objectUrl"></param>
        /// <param name="summaryView"></param>
        /// <param name="documentType"></param>
        /// <param name="verb"></param>
        /// <param name="changeData"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults ReceiveNotification(
            IWebConnection webConnection,
            string senderToken,
            string[] recipients,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData)
        {
            string senderIdentity;
            if (!FileHandler.TryGetSenderIdentity(senderToken, out senderIdentity))
                return WebResults.From(Status._412_Precondition_Failed, "senderToken");

            Dictionary<string, string> errors = new Dictionary<string, string>();

            // Validate summary view
            string summaryViewError = null;
            ValidateSummaryView(ref summaryView, ref summaryViewError);

            if (null != summaryViewError)
                errors["summaryView"] = summaryViewError;

            // validate change data
            string changeDataError = null;
            string linkedSenderIdentity = null;
            ValidateChangeData(verb, ref changeData, ref changeDataError, ref linkedSenderIdentity);

            if (null != changeDataError)
                errors["changeData"] = changeDataError;

            if ("link" == verb)
            {
                // First, link notifications are held in RAM
                Dictionary<string, object> linkChangeData = JsonReader.Deserialize<Dictionary<string, object>>(changeData);

                object linkID;
                if (!linkChangeData.TryGetValue("linkID", out linkID))
                    throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "linkID missing from change data"));

                NotificationLinkConfirmation nlc = GetNotificationLinkConfirmation(objectUrl, linkID.ToString());

                nlc.ChangeData = changeData;
                nlc.DocumentType = documentType;
                nlc.SenderIdentity = senderIdentity;
                nlc.SummaryView = summaryView;

                nlc.NotificationLinkInfo = new LinkInfo();
                nlc.NotificationLinkInfo.RecipientIdentities = new Set<string>(recipients);
                ReadLinkInfo(linkChangeData, ref nlc.NotificationLinkInfo.LinkDocumentType, "linkDocumentType");
                ReadLinkInfo(linkChangeData, ref nlc.NotificationLinkInfo.LinkSummaryView, "linkSummaryView");
                ReadLinkInfo(linkChangeData, ref nlc.NotificationLinkInfo.LinkUrl, "linkUrl");
                ReadLinkInfo(linkChangeData, ref nlc.NotificationLinkInfo.OwnerIdentity, "ownerIdentity");

                ProcessNotificationLinkConfirmation(objectUrl, linkID.ToString(), nlc);

                // then make sure users are valid
                for (int ctr = 0; ctr < recipients.Length; ctr++)
                {
                    try
                    {
                        GetUserFromNotificationRecipient(recipients[ctr]);
                    }
                    catch (UnknownUser)
                    {
                        errors[recipients[ctr]] = "notFound";
                    }
                    catch
                    {
                        errors[recipients[ctr]] = "error";
                    }
                }
            }
            else
            {
                // All other notifications are handled per the spec

                // Start handling the result for each recipient
                IAsyncResult[] receiveNotificationDelegateResults = new IAsyncResult[recipients.Length];
                for (int ctr = 0; ctr < recipients.Length; ctr++)
                    receiveNotificationDelegateResults[ctr] = ReceiveNotificationDelegate.BeginInvoke(
                        senderIdentity,
                        recipients[ctr],
                        objectUrl,
                        summaryView,
                        documentType,
                        verb,
                        changeData,
                        linkedSenderIdentity,
                        null,
                        null);

                // Wait for all notifications to be written to the database and aggregate errors
                for (int ctr = 0; ctr < recipients.Length; ctr++)
                {
                    try
                    {
                        ReceiveNotificationDelegate.EndInvoke(receiveNotificationDelegateResults[ctr]);
                    }
                    catch (UnknownUser)
                    {
                        errors[recipients[ctr]] = "notFound";
                    }
                    catch
                    {
                        errors[recipients[ctr]] = "error";
                    }
                }
            }
            // If there are no errors, return a 200 ok, else, return an accepted with the errors
            if (errors.Count == 0)
                return WebResults.From(Status._200_OK);
            else
                return WebResults.From(Status._202_Accepted, JsonWriter.Serialize(errors));
        }

        private void ReadLinkInfo(Dictionary<string, object> linkChangeData, ref string property, string name)
        {
            object propertyFromObject;
            if (!linkChangeData.TryGetValue(name, out propertyFromObject))
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, name + " missing from change data"));

            property = propertyFromObject.ToString();
        }

        /// <summary>
        /// The valid summary view tags and attributes.  Each tag is a key, and then there is a set of valid attributes
        /// </summary>
        public static Dictionary<string, Set<string>> ValidSummaryViewTagsAndAttributes
        {
            get 
            {
                if (null == _ValidSummaryViewTagsAndAttributes)
                {
                    Dictionary<string, Set<string>> validSummaryViewTagsAndAttributes = new Dictionary<string, Set<string>>();

                    validSummaryViewTagsAndAttributes["p"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["div"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["span"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["a"] = new Set<string>("href", "src", "target");
                    validSummaryViewTagsAndAttributes["br"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["img"] = new Set<string>("src");
                    validSummaryViewTagsAndAttributes["b"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["em"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["i"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["li"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["ol"] = new Set<string>();
                    validSummaryViewTagsAndAttributes["ul"] = new Set<string>();

                    // Assignment is performed atomically to avoid threading issues
                    _ValidSummaryViewTagsAndAttributes = validSummaryViewTagsAndAttributes;
                }

                return _ValidSummaryViewTagsAndAttributes; 
            }
        }
        private static Dictionary<string, Set<string>> _ValidSummaryViewTagsAndAttributes = null;

        /// <summary>
        /// The valid summary view classes
        /// </summary>
        private static Set<string> ValidSummaryViewClasses = new Set<string>(
            "particle_large", "particle_small", "particle_emphasis", "particle_right", "particle_left", "particle_clear");

        /// <summary>
        /// Validates a summary view, removing invalid tags, attributes, and css classes
        /// </summary>
        /// <param name="summaryView"></param>
        /// <param name="summaryViewError"></param>
        public void ValidateSummaryView(ref string summaryView, ref string summaryViewError)
        {
            XmlNode summaryViewNode;

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(string.Format("<summaryView>{0}</summaryView>", summaryView));

                summaryViewNode = xmlDocument.FirstChild;
            }
            catch (Exception e)
            {
                summaryView = string.Format("<pre>{0}</pre>", e.Message);
                summaryViewError = e.Message;

                return;
            }

            StringBuilder errorBuilder = new StringBuilder();

            foreach (XmlNode node in Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(summaryViewNode.ChildNodes)))
                ValidateSummaryView(node, errorBuilder);

            summaryView = summaryViewNode.InnerXml;

            if (errorBuilder.Length > 0)
                summaryViewError = errorBuilder.ToString();
        }

        private void ValidateSummaryView(XmlNode node, StringBuilder errorBuilder)
        {
            // Allow text to pass through
            if (node is XmlText)
                return;

            // Outright remove the node if it's not supported
            Set<string> supportedAttributes;
            if (!ValidSummaryViewTagsAndAttributes.TryGetValue(node.LocalName, out supportedAttributes))
            {
                node.ParentNode.RemoveChild(node);
                errorBuilder.AppendFormat("<{0}> is not a valid tag: {1} ", node.LocalName, node.OuterXml);

                return;
            }

            // Verify attributes and classes
            foreach (XmlAttribute attribute in Enumerable<XmlAttribute>.FastCopy(Enumerable<XmlAttribute>.Cast(node.Attributes)))
                if (attribute.LocalName == "class")
                {
                    // verify classes
                    Set<string> verifiedClasses = new Set<string>();

                    foreach (string classString in StringParser.Parse(attribute.Value, new string[] { " " }))
                        if (ValidSummaryViewClasses.Contains(classString))
                            verifiedClasses.Add(classString);
                        else
                            errorBuilder.AppendFormat(
                                "{0} is not a valid class in summary views: {1} ",
                                classString,
                                node.OuterXml);

                    attribute.Value = StringGenerator.GenerateSeperatedList(verifiedClasses, " ");
                }
                else if (!supportedAttributes.Contains(attribute.LocalName))
                {
                    node.Attributes.Remove(attribute);

                    if (attribute.LocalName != "xmlns")
                        errorBuilder.AppendFormat(
                            "{0} is not a valid attribute in <{1}>: {2} ",
                            attribute.LocalName,
                            node.LocalName,
                            node.OuterXml);
                }
                else if (node.LocalName == "a" && attribute.LocalName == "target")
                    if (!(attribute.Value == "_top" || attribute.Value == "_blank"))
                    {
                        node.Attributes.Remove(attribute);

                        errorBuilder.AppendFormat(
                                "{0} is not a valid value for target: {1} ",
                                attribute.Value,
                                node.OuterXml);
                    }

            foreach (XmlNode subNode in Enumerable<XmlNode>.FastCopy(Enumerable<XmlNode>.Cast(node.ChildNodes)))
                ValidateSummaryView(subNode, errorBuilder);
        }

        /// <summary>
        /// Validates change data
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="changeData"></param>
        /// <param name="changeDataError"></param>
        /// <param name="linkedSenderIdentity"></param>
        private void ValidateChangeData(string verb, ref string changeData, ref string changeDataError, ref string linkedSenderIdentity)
        {
            switch (verb)
            {
                case ("share"):
                    {
                        try
                        {
                            JsonReader.Deserialize<object[]>(changeData);
                        }
                        catch (Exception e)
                        {
                            changeData = "[]";
                            changeDataError = "For verb share, changeData must be a JSON array of recipients: " + e.Message;
                        }

                        break;
                    }

                case ("link"):
                    {
                        try
                        {
                            Dictionary<string, object> parsedChangeData =
                                JsonReader.Deserialize<Dictionary<string, object>>(changeData);

                            StringBuilder errorBuilder = new StringBuilder();

                            if (!parsedChangeData.ContainsKey("URL"))
                            {
                                errorBuilder.Append("Verb link requires a URL in the changeData ");
                                parsedChangeData["URL"] = "";
                            }

                            object summaryViewObj;
                            if (!parsedChangeData.TryGetValue("summaryView", out summaryViewObj))
                            {
                                errorBuilder.Append("Verb link requires a summaryView in the changeData ");
                                parsedChangeData["summaryView"] = "???";
                            }
                            else
                            {
                                string summaryView = summaryViewObj.ToString();
                                string summaryViewError = null;

                                ValidateSummaryView(ref summaryView, ref summaryViewError);

                                if (null != summaryViewError)
                                    errorBuilder.Append(summaryViewError);
                            }

                            // TODO:  Validate summary view

                            object linkedSenderIdentityObject;
                            if (!parsedChangeData.TryGetValue("owner", out linkedSenderIdentityObject))
                            {
                                errorBuilder.Append("Verb link requires a owner in the changeData ");
                                parsedChangeData["owner"] = "";
                            }
                            else
                                linkedSenderIdentity = linkedSenderIdentityObject.ToString();

                            changeData = JsonWriter.Serialize(parsedChangeData);

                            if (errorBuilder.Length > 0)
                                changeDataError = errorBuilder.ToString();
                        }
                        catch (Exception e)
                        {
                            Dictionary<string, object> parsedChangeData = new Dictionary<string, object>();
                            parsedChangeData["URL"] = "";
                            parsedChangeData["summaryView"] = string.Format("<pre>{0}</pre>", e.Message);
                            parsedChangeData["owner"] = "unknown";

                            changeData = JsonWriter.Serialize(parsedChangeData);
                            changeDataError = "For verb link, changeData must be a JSON object with URL, summaryView, and owner: " + e.Message;
                        }

                        break;
                    }

                default:
                    {
                        if (null != changeData)
                        {
                            if (changeData.Length > 0)
                                changeDataError = string.Format("No change data is allowed for verb {0}", verb);

                            changeData = null;
                        }

                        break;
                    }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public UserManagerWebHandler()
        {
            ReceiveNotificationDelegate = ReceiveNotification;
        }

        /// <summary>
        /// Delegate for RecieveNotification
        /// </summary>
        private readonly ReceiveNotificationDelegateType ReceiveNotificationDelegate;

        private void ReceiveNotification(
            string senderIdentity, 
            string recipient, 
            string objectUrl, 
            string summaryView, 
            string documentType, 
            string verb, 
            string changeData,
            string linkedSenderIdentity)
        {
            try
            {
                IUser user = GetUserFromNotificationRecipient(recipient);
                
                user.UserHandler.ReceiveNotification(
                    senderIdentity,
                    objectUrl,
                    summaryView,
                    documentType,
                    verb,
                    changeData,
                    linkedSenderIdentity);
            }
            catch (Exception e)
            {
                log.Error("Error in ReceiveNotification for " + recipient, e);
                throw;
            }
        }

        private IUser GetUserFromNotificationRecipient(string recipient)
        {
            string name = GetLocalUserNameFromOpenID(recipient);

            IUser user = FileHandler.GetUser(name);

            if (user == FileHandlerFactoryLocator.UserFactory.AnonymousUser.UserHandler)
                throw new SecurityException("The anonymous user can not recieve notifications");

            if (user == FileHandlerFactoryLocator.UserFactory.RootUser.UserHandler)
                throw new SecurityException("The root user can not recieve notifications from the public internet");
            return user;
        }

        /// <summary>
        /// Assists in getting information needed to perform a rapid login for an openID
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="senderIdentity"></param>
        /// <returns></returns>
        [WebCallable(Interfaces.WebServer.WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JSON, FilePermissionEnum.Read)]
        public IWebResults GetRapidLoginInfo(IWebConnection webConnection, string senderIdentity)
        {
            if ((!webConnection.Session.User.Local )|| (webConnection.Session.User.Id == FileHandlerFactoryLocator.UserFactory.AnonymousUser.Id))
                return WebResults.From(Status._403_Forbidden, "You must be a local user to see login information");

            try
            {
                return WebResults.ToJson(FileHandler.GetRapidLoginInfo(senderIdentity));
            }
            catch (ParticleException pe)
            {
                log.Error(senderIdentity + " is not known", pe);
                return WebResults.From(Status._404_Not_Found, senderIdentity + " unknown");
            }
        }

        /// <summary>
        /// Handles when a user confirms linking on another server, including starting to call particle.confirmLink on remote servers
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="objectUrl"></param>
        /// <param name="ownerIdentity"></param>
        /// <param name="linkSummaryView"></param>
        /// <param name="linkUrl"></param>
        /// <param name="linkDocumentType"></param>
        /// <param name="recipients"></param>
        /// <param name="redirectUrl"></param>
        /// <param name="linkID"></param>
        /// <param name="password"></param>
        /// <param name="remember"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults UserConfirmLink(
            IWebConnection webConnection,
            string objectUrl,
            string ownerIdentity,
            string linkSummaryView,
            string linkUrl,
            string linkDocumentType,
            string[] recipients,
            string redirectUrl,
            string linkID,
            string password,
            string remember)
        {
            IUser user;
            if (ownerIdentity != webConnection.Session.User.Identity)
            {
                string name = GetLocalUserNameFromOpenID(ownerIdentity);

                // Load the user and verify the password
                user = LoadUserAndVerifyPassword(webConnection, name, password);
            }
            else
                user = webConnection.Session.User;

            Uri domainUri = new Uri(objectUrl);

            webConnection.Session.User.UserHandler.SetRememberOpenIDLink(domainUri.Host, remember != null);

            FileHandler.GetEndpointInfos(
                user,
                false,
                recipients,
                ParticleEndpoint.ConfirmLink,
                delegate(EndpointInfo endpointInfo)
                {
                    HttpWebClient httpWebClient = new HttpWebClient();
                        httpWebClient.BeginPost(
                        endpointInfo.Endpoint,
                        delegate(HttpResponseHandler httpResponseHandler) 
                        {
                        },
                        delegate(Exception e)
                        {
                            log.Warn("Exception calling particle.confirmLink for " + StringGenerator.GenerateCommaSeperatedList(endpointInfo.RecipientIdentities), e);
                        },
                        new KeyValuePair<string, string>("objectUrl", objectUrl),
                        new KeyValuePair<string, string>("senderToken", endpointInfo.SenderToken),
                        new KeyValuePair<string, string>("linkSummaryView", linkSummaryView),
                        new KeyValuePair<string, string>("linkUrl", linkUrl),
                        new KeyValuePair<string, string>("linkDocumentType", linkDocumentType),
                        new KeyValuePair<string, string>("linkID", linkID),
                        new KeyValuePair<string, string>("recipients", JsonWriter.Serialize(endpointInfo.RecipientIdentities)));
                },
                delegate(IEnumerable<string> recipientsInError)
                {
                    log.Warn("Could not get particle.confirmLink for the following recipients: " + StringGenerator.GenerateCommaSeperatedList(recipientsInError));
                },
                delegate(Exception e)
                {
                    log.Error("Exception getting recipient information for particle.confirmLink", e);
                });

            return WebResults.Redirect(redirectUrl);
        }

        
        /// <summary>
        /// Handles when an identity server confirms that a user made a link
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="objectUrl"></param>
        /// <param name="senderToken"></param>
        /// <param name="linkSummaryView"></param>
        /// <param name="linkUrl"></param>
        /// <param name="linkDocumentType"></param>
        /// <param name="recipients"></param>
        /// <param name="linkID"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Read)]
        public IWebResults ConfirmLink(
            IWebConnection webConnection,
            string objectUrl,
            string senderToken,
            string linkSummaryView,
            string linkUrl,
            string linkDocumentType,
            string[] recipients,
            string linkID)
        {
            string senderIdentity;
            if (!FileHandler.TryGetSenderIdentity(senderToken, out senderIdentity))
                return WebResults.From(Status._412_Precondition_Failed, "senderToken");

            NotificationLinkConfirmation notificationLinkConfirmation = GetNotificationLinkConfirmation(objectUrl, linkID);

            notificationLinkConfirmation.ConfirmationLinkInfo = new LinkInfo();
            notificationLinkConfirmation.ConfirmationLinkInfo.LinkDocumentType = linkDocumentType;
            notificationLinkConfirmation.ConfirmationLinkInfo.LinkSummaryView = linkSummaryView;
            notificationLinkConfirmation.ConfirmationLinkInfo.LinkUrl = linkUrl;
            notificationLinkConfirmation.ConfirmationLinkInfo.OwnerIdentity = senderIdentity;
            notificationLinkConfirmation.ConfirmationLinkInfo.RecipientIdentities = new Set<string>(recipients);

            ProcessNotificationLinkConfirmation(objectUrl, linkID, notificationLinkConfirmation);

            return WebResults.From(Status._200_OK);
        }

        private ReaderWriterLockSlim NotificationLinkConfirmationLock = new ReaderWriterLockSlim();

        /// <summary>
        /// NotificationLinkConfirmations, first by ObjectUrl, then by LinkID
        /// </summary>
        private Dictionary<string, Dictionary<string, NotificationLinkConfirmation>> NotificationLinkConfirmationsByObjectUrlByLinkID
            = new Dictionary<string, Dictionary<string, NotificationLinkConfirmation>>();

        private NotificationLinkConfirmation GetNotificationLinkConfirmation(string objectUrl, string linkID)
        {
            // Make sure the timer is created
            if (null == NotificationLinkConfirmationTimer)
            {
                Timer timer = new Timer(NotificationLinkConfirmationCleanup, null, 600000, 600000);
                if (null != Interlocked.CompareExchange<Timer>(ref NotificationLinkConfirmationTimer, timer, null))
                    timer.Dispose();
            }

            // Get the dictionary for the the object url
            Dictionary<string, NotificationLinkConfirmation> forObjectUrl;

            NotificationLinkConfirmationLock.EnterUpgradeableReadLock();

            try
            {
                // If there's no dictionary, aquire a write lock, re-check, and then create it
                if (!NotificationLinkConfirmationsByObjectUrlByLinkID.TryGetValue(objectUrl, out forObjectUrl))
                {
                    NotificationLinkConfirmationLock.EnterWriteLock();

                    try
                    {
                        if (!NotificationLinkConfirmationsByObjectUrlByLinkID.TryGetValue(objectUrl, out forObjectUrl))
                        {
                            forObjectUrl = new Dictionary<string, NotificationLinkConfirmation>();
                            NotificationLinkConfirmationsByObjectUrlByLinkID[objectUrl] = forObjectUrl;
                        }
                    }
                    finally
                    {
                        NotificationLinkConfirmationLock.ExitWriteLock();
                    }
                }

                // Manipulation of the dictionary needs to happen within the read lock because NotificationLinkConfirmationsByObjectUrlByLinkID
                // is also cleaned within a lock
                lock (forObjectUrl)
                {
                    NotificationLinkConfirmation toReturn;

                    if (!forObjectUrl.TryGetValue(linkID, out toReturn))
                    {
                        toReturn = new NotificationLinkConfirmation();
                        forObjectUrl[linkID] = toReturn;
                    }

                    return toReturn;
                }
            }
            finally
            {
                NotificationLinkConfirmationLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Periodically cleans up outstanding establish trust request data
        /// </summary>
        private Timer NotificationLinkConfirmationTimer = null;

        private void NotificationLinkConfirmationCleanup(object state)
        {
            try
            {
                NotificationLinkConfirmationLock.EnterWriteLock();

                try
                {
                    foreach (KeyValuePair<string, Dictionary<string, NotificationLinkConfirmation>> forObjectUrlKVP
                        in Enumerable<KeyValuePair<string, Dictionary<string, NotificationLinkConfirmation>>>.FastCopy(NotificationLinkConfirmationsByObjectUrlByLinkID))
                    {
                        lock (forObjectUrlKVP.Value)
                        {
                            foreach (KeyValuePair<string, NotificationLinkConfirmation> nlcKVP in
                                Enumerable<KeyValuePair<string, NotificationLinkConfirmation>>.FastCopy(forObjectUrlKVP.Value))
                            {
                                if (nlcKVP.Value.Created.AddHours(1) < DateTime.UtcNow)
                                    forObjectUrlKVP.Value.Remove(nlcKVP.Key);
                            }

                            if (0 == forObjectUrlKVP.Value.Count)
                                NotificationLinkConfirmationsByObjectUrlByLinkID.Remove(forObjectUrlKVP.Key);
                        }
                    }
                }
                finally
                {
                    NotificationLinkConfirmationLock.ExitWriteLock();
                }
            }
            catch (Exception e)
            {
                log.Error("Exception cleaning up unconfirmed link notifications", e);
            }
        }

        private void ProcessNotificationLinkConfirmation(
            string objectUrl,
            string linkID,
            NotificationLinkConfirmation notificationLinkConfirmation)
        {
            if (2 != Interlocked.Increment(ref notificationLinkConfirmation.NumItemsRecieved))
                // Not ready for processing
                return;

            // Remove old NotificationLinkConfirmation from RAM
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    NotificationLinkConfirmationLock.EnterUpgradeableReadLock();

                    try
                    {
                        Dictionary<string, NotificationLinkConfirmation> objectUrlDictionary;
                        if (NotificationLinkConfirmationsByObjectUrlByLinkID.TryGetValue(objectUrl, out objectUrlDictionary))
                            lock (objectUrlDictionary)
                            {
                                objectUrlDictionary.Remove(linkID);

                                if (0 == objectUrlDictionary.Count)
                                {
                                    NotificationLinkConfirmationLock.EnterWriteLock();

                                    try
                                    {
                                        NotificationLinkConfirmationsByObjectUrlByLinkID.Remove(objectUrl);
                                    }
                                    finally
                                    {
                                        NotificationLinkConfirmationLock.ExitWriteLock();
                                    }
                                }
                            }
                    }
                    finally
                    {
                        NotificationLinkConfirmationLock.ExitUpgradeableReadLock();
                    }
                }
                catch (Exception e)
                {
                    log.Error("Exception cleaning " + objectUrl + " from the cached link notifications", e);
                }
            });

            if (!notificationLinkConfirmation.NotificationLinkInfo.Equals(
                notificationLinkConfirmation.ConfirmationLinkInfo))
            {
                log.Warn("Notification confirmation mismatch on a link: " + JsonWriter.Serialize(notificationLinkConfirmation));
                return;
            }

            IEnumerable<string> recipients = 
                notificationLinkConfirmation.ConfirmationLinkInfo.RecipientIdentities.Intersection(
                    notificationLinkConfirmation.ConfirmationLinkInfo.RecipientIdentities);

            // Start handling the result for each recipient
            foreach (string recipient in recipients)
                ThreadPool.QueueUserWorkItem(delegate(object recipientObj)
                {
                    try
                    {
                        ReceiveNotification(
                            notificationLinkConfirmation.SenderIdentity,
                            recipientObj.ToString(),
                            objectUrl,
                            notificationLinkConfirmation.SummaryView,
                            notificationLinkConfirmation.DocumentType,
                            "link",
                            notificationLinkConfirmation.ChangeData,
                            notificationLinkConfirmation.ConfirmationLinkInfo.OwnerIdentity);
                    }
                    catch (Exception e)
                    {
                        log.Warn("Exception processing a confirmed link notification", e);
                    }
                }, recipient);
        }

        /// <summary>
        /// Encapsulates data required to confirm a link
        /// </summary>
        private class NotificationLinkConfirmation
        {
            public string SenderIdentity;
            public string SummaryView;
            public string DocumentType;
            public string ChangeData;

            /// <summary>
            /// The link info that came from the host's notification
            /// </summary>
            public LinkInfo NotificationLinkInfo;

            /// <summary>
            /// The link info that came from the identity's confirmation
            /// </summary>
            public LinkInfo ConfirmationLinkInfo;

            /// <summary>
            /// The algorithm to know when both a notification and confirmation have come in is to increment this using interlocked, when 2 is returned, then it's time to process the notification
            /// </summary>
            public int NumItemsRecieved = 0;

            public DateTime Created = DateTime.UtcNow;
        }

        /// <summary>
        /// Encapsulates information about the link that comes from both the notification and the confirmation
        /// </summary>
        private class LinkInfo
        {
            public Set<string> RecipientIdentities;
            public string OwnerIdentity;
            public string LinkUrl;
            public string LinkSummaryView;
            public string LinkDocumentType;

            public override bool Equals(object obj)
            {
                if (obj is LinkInfo)
                {
                    LinkInfo other = (LinkInfo)obj;

                    if (OwnerIdentity == other.OwnerIdentity)
                        if (LinkUrl == other.LinkUrl)
                            if (LinkSummaryView == other.LinkSummaryView)
                                if (LinkDocumentType == other.LinkDocumentType)
                                    return true;
                }

                return false;
            }

            public override int GetHashCode()
            {
                int[] toHash = new int[]
                {
                    RecipientIdentities.GetHashCode(),
                    OwnerIdentity.GetHashCode(),
                    LinkUrl.GetHashCode(),
                    LinkSummaryView.GetHashCode(),
                    LinkDocumentType.GetHashCode()
                };

                return toHash.GetHashCode();
            }
        }
    }

    delegate void ReceiveNotificationDelegateType(
            string senderIdentity,
            string recipient,
            string objectUrl,
            string summaryView,
            string documentType,
            string verb,
            string changeData,
            string linkedSenderIdentity);
}
