// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Text;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Wrapper for text files
    /// </summary>
    public class TextWebHandler : WebHandler<ITextHandler>
    {
        private static ILog log = LogManager.GetLogger<TextWebHandler>();

        /// <summary>
        /// Reads all of the text in the file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="EncodeFor"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults ReadAll(IWebConnection webConnection, string EncodeFor)
        {
            string contents = FileHandler.ReadAll();

            switch (EncodeFor)
            {
                case ("HTML"):
                    {
                        // The text is encoded for HTML so it can be displayed unaltered
                        contents = HTTPStringFunctions.EncodeForHTML(contents);

                        break;
                    }
                case ("JavaScript"):
                    {
                        if (FileHandlerFactoryLocator.WebServer.MinimizeJavascript)
                        {
                            // The text will be "minimized" javascript to save space

                            JavaScriptMinifier javaScriptMinifier = new JavaScriptMinifier();

                            try
                            {
                                contents = javaScriptMinifier.Minify(contents);
                            }
                            catch (Exception e)
                            {
                                log.Error("Error when minimizing JavaScript", e);

                                return WebResults.FromString(Status._500_Internal_Server_Error, "Error when minimizing JavaScript: " + e.Message);
                            }
                        }

                        break;
                    }
            }

            if (webConnection.GetParameters.ContainsKey("MaxLength"))
            {
                int maxLength = int.Parse(webConnection.GetParameters["MaxLength"]);

                if (maxLength < contents.Length)
                    contents = contents.Substring(0, maxLength);
            }

            return WebResults.FromString(Status._200_OK, contents);
        }

        /// <summary>
        /// Resolves all of the WebComponents in the file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.Naked, WebReturnConvention.Naked, FilePermissionEnum.Read)]
        public IWebResults ResolveComponents(IWebConnection webConnection)
        {
            string contents = FileHandler.ReadAll();

            contents = webConnection.ResolveWebComponents(contents);

            return WebResults.FromString(Status._200_OK, contents);
        }

        /// <summary>
        /// Writes all of the text
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults WriteAll(IWebConnection webConnection, string text)
        {
            FileHandler.WriteAll(webConnection.Session.User, text);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }

        /// <summary>
        /// Appends the text
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults Append(IWebConnection webConnection, string text)
        {
            FileHandler.Append(webConnection.Session.User, text);

            return WebResults.FromString(Status._202_Accepted, "Appended");
        }

        /// <summary>
        /// Writes all of the text, intended for use from an HTML form
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_application_x_www_form_urlencoded, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults WriteAll_HTML(IWebConnection webConnection, string text)
        {
            FileHandler.WriteAll(webConnection.Session.User, text);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }

        /// <summary>
        /// Sends the updated contents whenever the file changes
        /// </summary>
        [ChannelEndpointMinimumPermission(FilePermissionEnum.Read)]
        public IChannelEventWebAdaptor ChangingEvent
        {
            get
            {
                if (!ChangingEventWired)
                    using (TimedLock.Lock(_ChangingEvent))
                        if (!ChangingEventWired)
                        {
                            ChangingEventWired = true;
                            FileHandler.ContentsChanged += new EventHandler<ITextHandler, EventArgs>(FileHandler_ContentsChanged);
                        }

                return _ChangingEvent;
            }
        }
        private readonly ChannelEventWebAdaptor _ChangingEvent = new ChannelEventWebAdaptor();

        private bool ChangingEventWired = false;

        void FileHandler_ContentsChanged(ITextHandler sender, EventArgs e)
        {
            string newFileContents = FileHandler.ReadAll();
            _ChangingEvent.SendAll(newFileContents);
        }
    }
}
