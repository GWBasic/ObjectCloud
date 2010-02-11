// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Collections.Generic;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Handles any kind of binary data
    /// </summary>
    public class BinaryWebHandler : WebHandler<IBinaryHandler>
    {
        /// <summary>
        /// Reads all of the data in the file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Naked, FilePermissionEnum.Read)]
        public IWebResults ReadAll(IWebConnection webConnection)
        {
            byte[] contents = FileHandler.ReadAll();

            MemoryStream stream = new MemoryStream(contents, false);

            return WebResults.FromStream(Status._200_OK, stream);
        }

        /// <summary>
        /// Reads all of the data in the file and returns a Base64 encoded string
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults ReadAllBase64(IWebConnection webConnection)
        {
            byte[] contents = FileHandler.ReadAll();

            string toReturn = Convert.ToBase64String(contents);

            return WebResults.FromString(Status._200_OK, toReturn);
        }

        /// <summary>
        /// Writes all of the data to the file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="contents">Any kind of data</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_bytes, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults WriteAll(IWebConnection webConnection, byte[] contents)
        {
            FileHandler.WriteAll(contents);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }

        /// <summary>
        /// Writes all of the data to the file
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="base64">Any kind of data, represented as a base64-encoded string</param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.POST_string, WebReturnConvention.Status, FilePermissionEnum.Write)]
        public IWebResults WriteAllBase64(IWebConnection webConnection, string base64)
        {
            byte[] contents = Convert.FromBase64String(base64);
            FileHandler.WriteAll(contents);

            return WebResults.FromString(Status._202_Accepted, "Saved");
        }

        /// <summary>
        /// Sends the updated contents whenever the file changes.  The contents are base64 encoded
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
                            FileHandler.ContentsChanged += new EventHandler<IBinaryHandler, EventArgs>(FileHandler_ContentsChanged);
                        }

                return _ChangingEvent;
            }
        }
        private readonly ChannelEventWebAdaptor _ChangingEvent = new ChannelEventWebAdaptor();

        private bool ChangingEventWired = false;

        void FileHandler_ContentsChanged(IBinaryHandler sender, EventArgs e)
        {
            byte[] newFileContents = FileHandler.ReadAll();
            _ChangingEvent.SendAll(Convert.ToBase64String(newFileContents));
        }
    }
}
