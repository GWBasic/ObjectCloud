// Scripts: /API/Prototype.js

var CP_Transport =
{
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

      var toReturn =
      {
         url: url,

         isNew: true,

         callbacks: callbacks,

         sendId: 0,

         longPoll: 3000,

         maxLongPoll: 30000,

         throwErrorOnSend: false,

         startSend: function(delay)
         {
            if (this.throwErrorOnSend)
               throw "This transport is no longer valid";

            if (null == delay)
               delay = 200;

            var me = this;
            var sendId = this.sendId;

            setTimeout(
               function() { me._startSend(sendId); },
               delay);
         },

         _startSend: function(queuedSendId)
         {
            // If _startSend was called prior to when it was queued, ignore the request
            if (queuedSendId < this.sendId)
               return;

            this.sendId++;

            var data = this.callbacks.getDataToSend();

            var body = { };

            if (null != data)
               body.d = data;

            if (this.isNew)
            {
               this.transportId = Math.floor(Math.random()*99999);
               body.isNew = true;
            }

            body.tid = this.transportId;
            body.lp = this.longPoll;

            var me = this;
            me.isNew = false;

            new Ajax.Request(
               url,
               {
                  method: 'post',
                  postBody: Object.toJSON(body),
                  onComplete: function(transport)
                  {
                     if (200 == transport.status)
                     {
                        if (transport.responseText.length > 0)
                           try
                           {
                              var incomingData = eval('(' + transport.responseText + ')');
                              me.callbacks.handleIncomingData(incomingData);
                           }
                           catch (exception)
                           {
                              me.callbacks.handleError(exception);
                              me.throwErrorOnSend = true;
                              return;
                           }

                        me.longPoll = 2 * me.longPoll;
                        if (me.longPoll > me.maxLongPoll)
                           me.longPoll = me.maxLongPoll;

                        // If the sendId has changed, it means that someone else queued another send
                        if (queuedSendId == me.sendId - 1)
                           me.startSend(0);

                        me.callbacks.flashSuccess(transport, me.sendId);
                     }
                     else if (409 == transport.status)
                     {
                        me.isNew = true;

                        // If the sendId has changed, it means that someone else queued another send
                        if (queuedSendId == me.sendId - 1)
                           me.startSend(0);
                     }
                     else if (transport.status >= 400 && transport.status <= 599)
                     {
                        me.throwErrorOnSend = true;
                        me.callbacks.handleError(transport.status + ", transport dropped");
                     }
                     else
                     {
                        me.longPoll = 1000;

                        // If the sendId has changed, it means that someone else queued another send
                        if (queuedSendId == me.sendId - 1)
                           me.startSend(2500);

                        me.callbacks.flashError(transport, me.sendId);
                     }
                  }
               });
         }
      };

      return toReturn;
   }
};