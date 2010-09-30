// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

using Common.Logging;
using ExtremeSwank.OpenId;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    public partial class UserManagerWebHandler
    {
        /// <summary>
        /// Establishes trust between a sender and this server
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="sender"></param>
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
            string sender,
            string avatar,
            string token,
            string loginURL,
            string loginURLOpenID,
            string loginURLWebFinger,
            string loginURLRedirect)
        {
            string senderToken = Convert.ToBase64String(SRandom.NextBytes(100));
            byte[] avatarBytes;

            try
            {
                avatarBytes = Convert.FromBase64String(avatar);
            }
            catch (Exception e)
            {
                throw new WebResultsOverrideException(WebResults.From(Status._400_Bad_Request, "Malformed avatar"), e);
            }

            GenericArgument<HttpResponseHandler> callback = delegate(HttpResponseHandler response)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                {
                    FileHandler.EstablishTrust(sender, senderToken, loginURL, loginURLOpenID, loginURLWebFinger, loginURLRedirect);

                    webConnection.SendResults(WebResults.From(Status._201_Created, "created"));

                    IUser senderUser = FileHandlerFactoryLocator.UserManagerHandler.GetOpenIdUser(sender);

                    string avatarFilename = senderUser.Id.ToString() + ".jpg";

                    IBinaryHandler avatarHandler;
                    if (ParticleAvatarsDirectory.IsFilePresent(avatarFilename))
                        avatarHandler = ParticleAvatarsDirectory.OpenFile(avatarFilename).CastFileHandler<IBinaryHandler>();
                    else
                        avatarHandler = (IBinaryHandler)ParticleAvatarsDirectory.CreateFile(avatarFilename, "image", null);

                    avatarHandler.WriteAll(avatarBytes);
                }
                else
                    webConnection.SendResults(WebResults.From(Status._400_Bad_Request, "Error from RespondTrust"));
            };

            GenericArgument<Exception> errorCallback = delegate(Exception e)
            {
                webConnection.SendResults(WebResults.From(Status._401_Unauthorized, "Could not establish trust"));
            };

            FileHandler.GetRespondTrustEnpoint(sender, delegate(string respondTrustEndpoint)
            {
                HttpWebClient httpWebClient = new HttpWebClient();
                httpWebClient.BeginPost(
                    respondTrustEndpoint,
                    callback,
                    errorCallback,
                    new KeyValuePair<string, string>("token", token),
                    new KeyValuePair<string, string>("senderToken", senderToken));
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
    }
}
