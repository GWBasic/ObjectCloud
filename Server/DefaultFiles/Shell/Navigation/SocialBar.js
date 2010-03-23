// Scripts: /API/Prototype.js, /API/Url.js, /API/Comet/CometProtocol.js, /API/DetectMobile.js

// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// See /Docs/license.wchtml

var sb_connectedUsers = {};
var sb_isEditing = false;
var sb_currentFile;
var sb_bus;
var sb_users = 0;

function sb_render()
{
   var tableBuilder = "<table><tr>";

   for (i in sb_connectedUsers)
   {
      var user = sb_connectedUsers[i];

      if (user.NumWindows > 0)
      {
         tableBuilder += '<th id="sb_' + user.UserId + '"><a href="' + user.UserIdentity + '" target="_blank">' + user.User;

         if (user.NumWindows > 1)
            tableBuilder += "(" + user.NumWindows + ")";

         if (null != user.State)
            if (user.LastStateUpdate.getTime() + 30000 >= (new Date()).getTime())
               tableBuilder += "[" + user.State + "]";

         tableBuilder += "</a></th>";
      }
      else
      {
         delete sb_connectedUsers[i];
         sb_users--;
      }
   }

   $("SocialBarSpan").innerHTML = tableBuilder;
}

function sb_initSocialBar(transport)
{
   var response = eval('(' + transport.responseText + ')');

   for (var i = 0; i < response.length; i++)
   {
      var user = response[i];
      sb_connectedUsers[user.UserId] = user;
   }

   sb_bus = CP_QualityReliable.connect(
      Url.decode(sb_currentFile) + "?ChannelEndpoint=Bus",
      {
         handleIncomingData: function(data)
         {
            var oldUsers = sb_users;

            if (!sb_connectedUsers[data.UserId])
            {
               sb_connectedUsers[data.UserId] =
               {
                  User: data.User,
                  UserId: data.UserId,
                  UserIdentity: data.UserIdentity,
                  NumWindows: 0,
                  State: null,
                  LastStateUpdate: new Date()
               };

               sb_users++;
            }

            if (data.Connected)
            {
               sb_connectedUsers[data.UserId].NumWindows++;

               if (sb_isEditing)
                  sb_setStateAsEdit();
            }

            if (data.Disconnected)
               sb_connectedUsers[data.UserId].NumWindows--;

            if (null != data.Data)
            {
               if ("Write" == data.Source)
                  if (null != data.Data.State)
                  {
                     sb_connectedUsers[data.UserId].State = data.Data.State;
                     sb_connectedUsers[data.UserId].LastStateUpdate = new Date();
                  }
            }

            sb_render();

            if (((oldUsers == 0) && (sb_users > 0)) || ((oldUsers > 0) && (sb_users == 0)))
               if (null != onresize)
                  onresize();

            if (null != data.Data)
               if (null != data.Data.Chat)
               {
                  var chatBox = $('sb_chat_' + data.UserId);

                  if (null != chatBox)
                     $("sb_encase").removeChild(chatBox);

                  var tableElement = $('sb_' + data.UserId);
                  var tableElementLocation = tableElement.cumulativeOffset();

                  var chatBox = new Element(
                     "div",
                     {
                        id: 'sb_chat_' + data.UserId,
                        style: "position: absolute; left: " + (tableElementLocation.left + 10) + "px; top: " + (tableElementLocation.top - 20) + "px;"
                     });

                  $("sb_encase").appendChild(chatBox);

                  chatBox.innerHTML = '<table><tr><td id="sb_chattable_' + data.UserId + '"></td></tr></table>';

                  var chattable = $('sb_chattable_' + data.UserId);
                  chattable.update(data.Data.Chat);
               }
         },
         flashSuccess: function()
         {
            $("SocialBarSpan").enable();
         },
         flashError: function()
         {
            $("SocialBarSpan").disable();
         },
         handleError: function(error)
         {
            $("SocialBarSpan").innerHTML = "You must refresh in order to enable Comet: " + error;

            if (null != onresize)
               onresize();
         }
      });

   sb_render();

   if (null != onresize)
      onresize();
}

function sb_setStateAsEdit()
{
   new Ajax.Request(
      sb_currentFile + "?Method=PostBusAsWrite",
      {
         method: 'post',
         postBody: Object.toJSON({ State: "Edit" }),
         onFailure: function(transport)
         {
            $("SocialBarSpan").innerHTML = "Error: " + transport.responseText;
         },
         onException: function(requester, error)
         {
            $("SocialBarSpan").innerHTML = "Error: " + error;
         }
      });
}

if (!isMobile)
{
   var sb_oldOnload = onload;
   onload = function()
   {
      if (null != sb_oldOnload)
         try
         {
            sb_oldOnload();
         }
         catch (exception)
         {
            alert(exception);
         }

      var currentUrl = Url.parseCurrent();
      sb_currentFile = currentUrl.file;

      new Ajax.Request(
         sb_currentFile + "?Method=GetConnectedUsers",
         {
            method: 'get',
            onFailure: function(transport)
            {
               $("SocialBarSpan").innerHTML = "Error: " + transport.responseText;
            },
            onException: function(requester, error)
            {
               $("SocialBarSpan").innerHTML = "Error: " + error;
            },
            onSuccess: function(transport)
            {
               sb_initSocialBar(transport);
            }
         });

      if (null != currentUrl.arguments.Action)
         if ("Edit" == currentUrl.arguments.Action)
         {
            sb_isEditing = true;
            sb_setStateAsEdit();

            // Every 25 seconds poll to set the state as edit
            setInterval('sb_setStateAsEdit()', 25000);
         }
   }
}

function sb_speak()
{
   var sb_text = $("sb_text");
   var toSay = sb_text.value;
   sb_text.value = "";

   sb_bus.send({ Chat: toSay });
}