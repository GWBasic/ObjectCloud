// Scripts:  /API/Prototype.js, /API/Comet/CometTransport.js

var CP_Multiplex =
{
   multiplexerTransport: null,

   unackedChannels: {},
   channels: {},

   reset: function()
   {
      this.multiplexerTransport = null;
   },

   create: function(url, callbacks)
   {
      if (!callbacks.getDataToSend)
         callbacks.getDataToSend = function(){};

      if (!callbacks.handleIncomingData)
         callbacks.handleIncomingData = function(){};

      if (!callbacks.handleError)
         callbacks.handleError = function(){};

      if (!callbacks.flashSuccess)
         callbacks.flashSuccess = function(){};

      if (!callbacks.flashError)
         callbacks.flashError = function(){};

      var me = this;

      if (null == this.multiplexerTransport)
         this.multiplexerTransport = CP_Transport.create(
         "/System/Comet/Multiplexer",
         {
            getDataToSend : function()
            {
               // Generate control data
               var control = [];

               for (var transportId in me.unackedChannels)
               {
                  var unackedChannel = me.unackedChannels[transportId];

                  control.push(
                  {
                     u: unackedChannel.url,
                     tid: unackedChannel.transportId
                  });
               }

               var data = {};
               if (control.length > 0)
                  data.m = control;

               for (var transportId in me.channels)
               {
                  var channel = me.channels[transportId];

                  var dataFromChannel = channel.callbacks.getDataToSend();
                  if (null != dataFromChannel)
                     data[channel.transportId] = dataFromChannel;
               }

               return data;
            },
            handleIncomingData : function(data)
            {
               for (var transportId in data)
               {
                  var dataFromChannel = data[transportId];

                  // Control information
                  if ("m" == transportId)
                  {
                     for (var subTransportId in dataFromChannel)
                     {
                        var controlInfo = dataFromChannel[subTransportId];

                        // acks
                        if ("a" == subTransportId)
                           for (var ctr = 0; ctr < controlInfo.length; ctr++)
                           {
                              delete me.unackedChannels[controlInfo[ctr]];
                           }
                        else
                        {
                           var channel = me.channels[subTransportId];

                           if (null != channel)
                           {
                              channel.callbacks.handleError(controlInfo);
                              channel.isDisconnected = true;

                              delete me.channels[subTransportId];
                              delete me.unackedChannels[subTransportId];
                           }
                        }
                     }
                  }
                  else
                     me.channels[transportId].callbacks.handleIncomingData(dataFromChannel);
               }
            },
            handleError : function(error)
            {
               for (var transportId in me.channels)
                  me.channels[transportId].callbacks.handleError(error);
            },
            flashSuccess : function(transport, sendId)
            {
               for (var transportId in me.channels)
                  me.channels[transportId].callbacks.flashSuccess(transport, sendId);
            },
            flashError : function(transport, sendId)
            {
               for (var transportId in me.channels)
                  me.channels[transportId].callbacks.flashError(transport, sendId);
            }
         });

      var toReturn =
      {
         url: url,

         callbacks: callbacks,

         isDisconnected: false,

         transportId: Math.floor(Math.random()*99999),

         startSend: function(delay)
         {
            if (this.isDisconnected)
               throw "Channel is disconnected";

            me.multiplexerTransport.startSend(delay);
         }
      };

      this.unackedChannels[toReturn.transportId] = toReturn;
      this.channels[toReturn.transportId] = toReturn;

      return toReturn;

   }
};

var CP_QualityReliable =
{
   connect: function(url, callbacks)
   {
      if (!callbacks.handleIncomingData)
         callbacks.handleIncomingData = function(){};

      if (!callbacks.handleCloseRequested)
         callbacks.handleCloseRequested = function(){};

      if (!callbacks.handleClosed)
         callbacks.handleClosed = function(){};

      if (!callbacks.handleError)
         callbacks.handleError = function(){};

      if (!callbacks.flashSuccess)
         callbacks.flashSuccess = function(){};

      if (!callbacks.flashError)
         callbacks.flashError = function(){};

      var nextSentPacketId = 0;

      var nextExpectedPacketId = 0;

      var ack = null;

      var unsentPackets = {};

      var recievedPackets = {};

      var transport = null;

      var waiting = {};

      var state = "connected";

      var toReturn =
      {
         callbacks: callbacks,

         send: function(data, maxDelay)
         {
            if ("connected" != state)
               throw "Can not send data when the connection is disconnecting or disconnected";

            unsentPackets[nextSentPacketId] = data;
            nextSentPacketId++;

            transport.startSend(maxDelay);
         },

         close: function()
         {
            state = "disconnecting";

            transport.startSend(0);
         }
      }

      transport = CP_Multiplex.create(
         url,
         {
            getDataToSend: function(sendId) 
            {
               var count = 0;

               // Check to see if there's no data to return
               if (null == ack)
               {
                  for (var packetId in unsentPackets)
                     if (unsentPackets.hasOwnProperty(packetId))
                        ++count;

                  // If there's no data to send, return null
                  if (0 == count)
                     if ("connected" == state)
                        return null;
               }

               // Track which packets were sent in this request
               waiting[sendId] = nextSentPacketId - 1;

               var toReturn = {};

               if (count > 0)
                  toReturn.d = unsentPackets;

               if (null != ack)
               {
                  toReturn.a = ack;

                  // if the ack gets lost, then the server will re-send
                  ack = null;
               }

               if ("connected" != state)
                  toReturn.end = true;

               return toReturn;
            },

            handleIncomingData: function(incoming)
            {
               for (var packetId in incoming)
                  if ("end" == packetId)
                     if ("disconnecting" == state)
                     {
                        state = "disconnected";
                        callbacks.handleClosed();
                     }
                     else
                     {
                        state = "disconnecting";
                        callbacks.handleCloseRequested();
                     }
                  else
                     if (packetId >= nextExpectedPacketId)
                        recievedPackets[packetId] = incoming[packetId];

               while (true)
               {
                  if (!recievedPackets.hasOwnProperty(nextExpectedPacketId))
                     return;

                  var packet = recievedPackets[nextExpectedPacketId];

                  try
                  {
                     callbacks.handleIncomingData(packet, nextExpectedPacketId);
                  }
                  catch (exception)
                  {
                     callbacks.handleError(exception);
                  }

                  delete recievedPackets[nextExpectedPacketId];
                  ack = nextExpectedPacketId;
                  nextExpectedPacketId++;
               }
            },

            handleError: function(error) 
            {
               state = "disconnected";

               // communicate the error
               callbacks.handleError(error);
            },

            flashSuccess: function(transport, sendId)
            {
               if (waiting.hasOwnProperty(sendId))
               {
                  // Delete all packets that were successfully sent
                  for (var packetId in unsentPackets)
                     if (packetId <= waiting[sendId])
                        if (unsentPackets.hasOwnProperty(packetId))
                           delete unsentPackets[packetId];

                  delete waiting[sendId];

                  // Delete potentaly lost transmissions
                  for (var oldSendId in waiting)
                     if (oldSendId < sendId)
                        if (waiting.hasOwnProperty(oldSendId))
                           delete waiting[oldSendId];
               }

               // communicate a working transport
               callbacks.flashSuccess(transport, sendId);
            },

            flashError: function(transport, sendId)
            {
               // communicate a bungy transport
               callbacks.flashError(transport, sendId);
            },
         });

      transport.startSend();

      return toReturn;
   }
}




