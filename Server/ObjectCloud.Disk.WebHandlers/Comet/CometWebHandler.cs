// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Comet
{
    /// <summary>
    /// Handles Comet IO once a session is established
    /// </summary>
    public class CometWebHandler : BaseCometWebHandler
    {
        /// <summary>
        /// Performs a comet handshake
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.Naked, WebReturnConvention.Naked)]
        public IWebResults DoComet(IWebConnection webConnection)
        {
            IDictionary<string, string> variables;

            switch (webConnection.Method)
            {
                case WebMethod.GET:
                    variables = webConnection.GetParameters;
                    break;

                case WebMethod.POST:
                    variables = webConnection.PostParameters;
                    break;

                default:
                    return WebResults.FromString(Status._405_Method_Not_Allowed, "Only GET and POST is allowed");
            }

            /*
Per-request variables
---------------------

Spec Name       var name    default value

SESSION_KEY     "s"         *Required, no default value.
ACK_ID          "a"         "-1"
DATA            "d"         ""
NO_CACHE        "n"         *ignored
             */

            string sessionKeyString = variables["s"];
            variables.Remove("s");

            ushort sessionIdValue = default(ushort);

            if (!ushort.TryParse(
                sessionKeyString,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out sessionIdValue))
            {
                return WebResults.FromString(Status._417_Expectation_Failed, "Invalid session ID");
            }

            ID<ICometSession, ushort> sessionId = new ID<ICometSession, ushort>(sessionIdValue);

            ICometSession cometSession;
            try
            {
                cometSession = CometHandler[sessionId];
            }
            catch (BadCometSessionId)
            {
                return WebResults.FromString(Status._400_Bad_Request, "Bad SESSION_KEY");
            }

            if (ResponseNotYetSent.ContainsKey(cometSession))
                ResponseNotYetSent[cometSession].Discard();

            AsyncDataSender asyncDataSender = new AsyncDataSender(webConnection, cometSession);
            asyncDataSender.CompletedResponse += new EventHandler<AsyncDataSender, EventArgs<ICometSession>>(AsyncDataSender_CompletedResponse);
            ResponseNotYetSent[cometSession] = asyncDataSender;

            return asyncDataSender.DoComet(CometHandler, variables);
        }

        /// <summary>
        /// Removes dead requests
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AsyncDataSender_CompletedResponse(AsyncDataSender sender, EventArgs<ICometSession> e)
        {
            sender.CompletedResponse -= new EventHandler<AsyncDataSender, EventArgs<ICometSession>>(AsyncDataSender_CompletedResponse);
            ResponseNotYetSent.Remove(sender.CometSession);
        }

        /// <summary>
        /// All of the incomplete requests
        /// </summary>
        private Dictionary<ICometSession, AsyncDataSender> ResponseNotYetSent = new Dictionary<ICometSession, AsyncDataSender>();

        /// <summary>
        /// Maintains state while waiting for outgoing data
        /// </summary>
        private class AsyncDataSender
        {
            private IWebConnection WebConnection;

            public AsyncDataSender(IWebConnection webConnection, ICometSession cometSession)
            {
                WebConnection = webConnection;
                CometSession = cometSession;
            }

            public ICometSession CometSession;
            string Preamble = default(string);
            string BatchPrefix = default(string);
            string BatchSuffix = default(string);
            string ContentType = default(string);
            //string RequestPrefix = default(string);
            //string RequestSuffix = default(string);
            MulticastEventWithTimeout<ICometSession, EventArgs>.Listener Listener = default(MulticastEventWithTimeout<ICometSession, EventArgs>.Listener);

            public IWebResults DoComet(ICometHandler cometHandler, IDictionary<string, string> variables)
            {
                string ack_id = null;
                if (!WebConnection.Headers.TryGetValue("LAST-EVENT-ID", out ack_id))
                    variables.TryGetValue("a", out ack_id);

                if (null != ack_id)
                    if (("-1" != ack_id) && (ack_id.Length > 0))
                    {
                        ulong highestAckedSentPacketId = default(ulong);

                        if (!ulong.TryParse(ack_id, out highestAckedSentPacketId))
                            return WebResults.FromString(Status._400_Bad_Request, "Bad ACK_ID");

                        variables.Remove("a");

                        CometSession.AckSentPackets(highestAckedSentPacketId);
                    }

                string data;
                if (variables.ContainsKey("d"))
                {
                    data = variables["d"];
                    variables.Remove("d");
                }
                else
                    data = "";

                // TODO, do something with the data
                Console.WriteLine(data);

                // Remove NO_CACHE
                variables.Remove("n");

                CometSession.MergeVariables(variables);

                /*
    Persistent Variables
    --------------------

    Spec Name       var name    default value

    REQUEST_PREFIX  "rp"        ""
    REQUEST_SUFFIX  "rs"        ""
    DURATION        "du"         "30"
    IS_STREAMING    "is"        "0"
    INTERVAL        "i"         "0"
    PREBUFFER_SIZE  "ps"        "0"
    PREAMBLE        "p"         ""
    BATCH_PREFIX    "bp"        ""
    BATCH_SUFFIX    "bs"        ""
    GZIP_OK         "g"         ""
    SSE             "se"        ""
    CONTENT_TYPE    "ct"        "text/html"
    PREBUFFER       *see PREBUFFER_SIZE
    SSE_ID          *see SSE
                 */

                TimeSpan duration;
                if (CometSession.Variables.ContainsKey("du"))
                {
                    double durationD = default(double);

                    if (!double.TryParse(CometSession.Variables["du"], out durationD))
                        return WebResults.FromString(Status._400_Bad_Request, "Bad DURATION");

                    duration = TimeSpan.FromSeconds(durationD);

                }
                else
                    duration = TimeSpan.FromSeconds(30);

                // TODO: IS_STREAMING, INTERVAL, PREBUFFER_SIZE

                if (!CometSession.Variables.TryGetValue("p", out Preamble))
                    Preamble = "";

                if (!CometSession.Variables.TryGetValue("bp", out BatchPrefix))
                    BatchPrefix = "";

                if (!CometSession.Variables.TryGetValue("bs", out BatchSuffix))
                    BatchSuffix = "";

                if (!CometSession.Variables.TryGetValue("ct", out ContentType))
                    ContentType = "text/html";

                /*if (!CometSession.Variables.TryGetValue("rp", out RequestPrefix))
                    RequestPrefix = "";

                if (!CometSession.Variables.TryGetValue("rs", out RequestSuffix))
                    RequestSuffix = "";*/

                lock (CometSession)
                    if (CometSession.HasUnAckedSentPackets)
                        SendData(CometSession.UnAckedSentPackets);
                    else
                    {
                        Listener = new MulticastEventWithTimeout<ICometSession, EventArgs>.Listener(
                            duration,
                            delegate(ICometSession cometSessionS, EventArgs e)
                            {
                                CometSession.DataSent.RemoveListener(Listener);
                                SendData(CometSession.UnAckedSentPackets);
                            },
                            delegate(ICometSession cometSessionS)
                            {
                                SendData(CometSession.UnAckedSentPackets);
                            });

                        CometSession.DataSent.AddListener(Listener);
                    }
                // Don't block while waiting for packets!
                return null;
            }

            /// <summary>
            /// Sends the comet data to the browser
            /// </summary>
            /// <param name="packetsToSend"></param>
            private void SendData(IEnumerable<KeyValuePair<ulong, string>> packetsToSend)
            {
                if (ResponseSent)
                    return;

                lock (Locker)
                {
                    if (ResponseSent)
                        return;

                    //The body of each Comet response will contain the PREBUFFER immediately at the start, followed immediately by the PREAMBLE
                    StringBuilder resultsBuilder = new StringBuilder();
                    resultsBuilder.Append(Preamble);
                    resultsBuilder.Append(BatchPrefix);
                    resultsBuilder.Append("(");

                    // The results
                    List<object[]> results = new List<object[]>();

                    List<KeyValuePair<ulong, string>> sortedPacketsToSend = new List<KeyValuePair<ulong, string>>(packetsToSend);
                    sortedPacketsToSend.Sort(delegate(KeyValuePair<ulong, string> a, KeyValuePair<ulong, string> b)
                    {
                        return a.Key.CompareTo(b.Key);
                    });

                    ulong lastIdSent = 0;

                    foreach (KeyValuePair<ulong, string> unackedSentPacket in sortedPacketsToSend)
                    {
                        results.Add(new object[] { unackedSentPacket.Key, 0, unackedSentPacket.Value });
                        lastIdSent = unackedSentPacket.Key;
                    }

                    JsonWriter jsonWriter = new JsonWriter(resultsBuilder);
                    jsonWriter.Write(results.ToArray());

                    //[ PACKET_ID, PACKET_ENCODING, PACKET_DATA ]

                    resultsBuilder.Append(")");
                    resultsBuilder.Append(BatchSuffix);

                    // This spot makes no sense.  It's for HTML5, but it appears that this should be a header instead of being appended to the response body
                    // Perhaps the Comet Session Protocol spec will be updated?
                    string sse = default(string);
                    if (CometSession.Variables.TryGetValue("se", out sse))
                        if ("1" == sse)
                            resultsBuilder.AppendFormat("id: {0}\r\n", lastIdSent);

                    WebResults toReturn = WebResults.FromString(Status._200_OK, resultsBuilder.ToString());
                    toReturn.ContentType = ContentType;
                    toReturn.Headers["Cache-Control"] = "no-cache, must-revalidate";

                    WebConnection.SendResults(toReturn);

                    ResponseSent = true;
                }

                if (null != CompletedResponse)
                    CompletedResponse(this, new EventArgs<ICometSession>(CometSession));
            }

            /// <summary>
            /// Forces the open connection to return immediately
            /// </summary>
            public void Discard()
            {
                if (ResponseSent)
                    return;

                lock (Locker)
                {
                    if (ResponseSent)
                        return;
                    
                    CometSession.DataSent.RemoveListener(Listener);

                    // Not sure what status to use here
                    WebConnection.SendResults(WebResults.FromStatus(Status._200_OK));

                    ResponseSent = true;
                }

                if (null != CompletedResponse)
                    CompletedResponse(this, new EventArgs<ICometSession>(CometSession));
            }

            /// <summary>
            /// Used to make sure that there isn't two responses sent
            /// </summary>
            private object Locker = new object();

            private bool ResponseSent = false;

            /// <summary>
            /// Called when the response is completed
            /// </summary>
            public event EventHandler<AsyncDataSender, EventArgs<ICometSession>> CompletedResponse;
        }
    }
}
