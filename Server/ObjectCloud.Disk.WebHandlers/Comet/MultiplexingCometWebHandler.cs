// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Threading;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers.Comet
{
    /// <summary>
    /// Web handler that multiplexes layer 1 (transport) to layer 2 (multiplexed) comet sessions
    /// </summary>
    public class MultiplexingCometWebHandler : WebHandler<IFileHandler>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="getArguments"></param>
        /// <param name="transportId"></param>
        /// <returns></returns>
        public override ICometTransport ConstructCometTransport(ISession session, IDictionary<string, string> getArguments, long transportId)
        {
            return new MultiplexedCometTransport(FileHandlerFactoryLocator.FileSystemResolver, session);
        }

        /// <summary>
        /// Provides unreliable multiplexed comet transport
        /// </summary>
        private class MultiplexedCometTransport : ICometTransport
        {
            private static ILog log = LogManager.GetLogger<MultiplexedCometTransport>();

            public MultiplexedCometTransport(IFileSystemResolver fileSystemResolver, ISession session)
            {
                FileSystemResolver = fileSystemResolver;
                Session = session;
                _StartSend = new MulticastEventWithTimeout(this);

                // When another channel is ready to send, then this channel will send
                Listener = new MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>.Listener(
                    TimeSpan.MaxValue,
                    delegate(ICometTransport transport, EventArgs<TimeSpan> e)
                    {
                        _StartSend.Send(e);
                    },
                    delegate(ICometTransport transport)
                    {
                    });
            }

            /// <summary>
            /// The file system resolver
            /// </summary>
            IFileSystemResolver FileSystemResolver;

            /// <summary>
            /// The session
            /// </summary>
            ISession Session;

            /// <summary>
            /// The listener used for every channel
            /// </summary>
            MulticastEventWithTimeout.Listener Listener;

            /// <summary>
            /// Provides syncronization
            /// </summary>
            //private object SyncKey = new object();
            private Mutex GetDataMutex = new Mutex();

            public object GetDataToSend()
            {
                // Wait up to 50 milliseconds to get data, if the mulitplexer is still blocked, just return nothing
                if (!GetDataMutex.WaitOne(50))
                {
                    // Assume that someone is processing something and that there might be data to send later...
                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        _StartSend.Send(new EventArgs<TimeSpan>(TimeSpan.FromMilliseconds(100)));
                    });

                    return null;
                }

                try
                {
                    Dictionary<string, object> toReturn = new Dictionary<string, object>();

                    // Add control information, if needed
                    if (RequestedChannels.Count + ChannelErrors.Count > 0)
                    {
                        Dictionary<string, object> controlInformation = new Dictionary<string, object>();

                        // Add acks
                        if (RequestedChannels.Count > 0)
                        {
                            List<long> acks = new List<long>();

                            foreach (long requestedChannel in RequestedChannels)
                                acks.Add(requestedChannel);

                            controlInformation["a"] = acks.ToArray();
                            RequestedChannels.Clear();
                        }

                        // Add error codes
                        if (ChannelErrors.Count > 0)
                            foreach (KeyValuePair<long, Status> channelInError in ChannelErrors)
                                controlInformation[channelInError.Key.ToString()] = (int)channelInError.Value;

                        ChannelErrors.Clear();

                        toReturn["m"] = controlInformation;
                    }

                    foreach (KeyValuePair<long, ICometTransport> channel in Channels)
                    {
                        object dataToSend = channel.Value.GetDataToSend();

                        if (null != dataToSend)
                            toReturn[channel.Key.ToString()] = dataToSend;
                    }

                    // Only return data if it's going to contain something
                    if (toReturn.Count > 0)
                        return toReturn;
                    else
                        // If null isn't returned, then it will prevent long polling
                        return null;
                }
                finally
                {
                    GetDataMutex.ReleaseMutex();
                }
            }

            public void HandleIncomingData(object incoming)
            {
                if (!GetDataMutex.WaitOne(50))
                {
                    // If the mutex can't be grabbed, keep trying to process the data in a non-blocking way
                    ThreadPool.QueueUserWorkItem(HandleIncomingData, incoming);
                    return;
                }

                try
                {
                    IDictionary<string, object> request = (IDictionary<string, object>)incoming;

                    object mObj = null;
                    if (request.TryGetValue("m", out mObj))
                    {
                        object[] m = (object[])mObj;

                        foreach (Dictionary<string, object> addUrlRequest in m)
                        {
                            long transportId = Convert.ToInt64(addUrlRequest["tid"]);

                            // If the channel is already estbalished, just re-ack it
                            if (Channels.ContainsKey(transportId))
                                RequestedChannels.Add(transportId);
                            else
                            {
                                // If this is a new channel, open it
                                try
                                {
                                    string url = addUrlRequest["u"].ToString();

                                    log.Info("Channel requested: " + url);

                                    IDictionary<string, string> getArguments;

                                    string[] urlAndParameters = url.Split('?');
                                    if (urlAndParameters.Length > 1)
                                        getArguments = new RequestParameters(urlAndParameters[1]);
                                    else
                                        getArguments = new Dictionary<string, string>();

                                    IFileContainer fileContainer;
                                    try
                                    {
                                        fileContainer = FileSystemResolver.ResolveFile(urlAndParameters[0]);
                                    }
                                    catch (FileDoesNotExist)
                                    {
                                        log.Error("The requested channel does not exist: " + url);
                                        throw new WebResultsOverrideException(WebResults.FromStatus(Status._404_Not_Found));
                                    }

                                    ICometTransport cometTransport = fileContainer.WebHandler.ConstructCometTransport(
                                        Session, getArguments, transportId);

                                    cometTransport.StartSend.AddListener(Listener);

                                    RequestedChannels.Add(transportId);
                                    Channels[transportId] = cometTransport;
                                }
                                catch (WebResultsOverrideException wroe)
                                {
                                    log.Error("Error when establishing a multiplexted comet transport channel", wroe);
                                    ChannelErrors[transportId] = wroe.WebResults.Status;
                                }
                                catch (Exception e)
                                {
                                    log.Error("Error when establishing a multiplexted comet transport channel", e);
                                    ChannelErrors[transportId] = Status._500_Internal_Server_Error;
                                }
                            }
                        }

                        // Return acks if any new channels were added
                        if (RequestedChannels.Count + ChannelErrors.Count > 0)
                            _StartSend.Send(new EventArgs<TimeSpan>(TimeSpan.Zero));
                    }

                    // Send data to each channel that recieved it
                    foreach (KeyValuePair<string, object> channelData in request)
                        if ("m" != channelData.Key)
                        {
                            long transportId = long.Parse(channelData.Key);

                            ICometTransport cometTransport = default(ICometTransport);
                            if (!Channels.TryGetValue(transportId, out cometTransport))
                                ChannelErrors[transportId] = Status._410_Gone;
                            else
                            {
                                try
                                {
                                    cometTransport.HandleIncomingData(channelData.Value);
                                }
                                catch (WebResultsOverrideException wroe)
                                {
                                    ChannelErrors[transportId] = wroe.WebResults.Status;
                                }
                                catch (Exception)
                                {
                                    ChannelErrors[transportId] = Status._500_Internal_Server_Error;
                                }
                            }
                        }
                }
                catch (WebResultsOverrideException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new WebResultsOverrideException(
                        WebResults.FromStatus(Status._400_Bad_Request), e);
                }
                finally
                {
                    GetDataMutex.ReleaseMutex();
                }
            }

            /// <summary>
            /// Lets other channels start sending
            /// </summary>
	        public MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>> StartSend
            {
                get { return _StartSend; }
            }
            private MulticastEventWithTimeout _StartSend;

            private class MulticastEventWithTimeout : MulticastEventWithTimeout<ICometTransport, EventArgs<TimeSpan>>
            {
                public MulticastEventWithTimeout(ICometTransport cometTransport) : base(TimeSpan.FromSeconds(2.5), cometTransport) { }

                public void Send(EventArgs<TimeSpan> e)
                {
                    TriggerEvent(e);
                }
            }

            public void Dispose()
            {
                foreach (ICometTransport cometTransport in Channels.Values)
                    try
                    {
                        cometTransport.Dispose();
                    }
                    catch (Exception e)
                    {
                        log.Error("Error when disposing a channel", e);
                    }

                StartSend.Dispose();
            }

            /// <summary>
            /// All of the channels, indexed by transport ID
            /// </summary>
            private Dictionary<long, ICometTransport> Channels = new Dictionary<long, ICometTransport>();

            /// <summary>
            /// Channels that the client keeps requesting
            /// </summary>
            private Set<long> RequestedChannels = new Set<long>();

            /// <summary>
            /// All errors that occured when opening channels
            /// </summary>
            private Dictionary<long, Status> ChannelErrors = new Dictionary<long,Status>();
        }
    }
}
