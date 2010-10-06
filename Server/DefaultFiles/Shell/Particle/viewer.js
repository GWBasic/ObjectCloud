// Scripts: /API/jquery.js, /Users/[name].user?Method=GetJSW&assignToVariable=NotificationsProxy, /API/Comet/CometProtocol.js, /API/jquery-ui.auto.js, /API/Url.js

// http://jasonwyatt.tumblr.com/post/206787093/javascript-escapehtml-string-function
String.prototype.escapeHTML = function(){
    var result = "";
    for(var i = 0; i < this.length; i++){
        if(this.charAt(i) == "&" 
              && this.length-i-1 >= 4 
              && this.substr(i, 4) != "&amp;"){
            result = result + "&amp;";
        } else if(this.charAt(i)== "<"){
            result = result + "&lt;";
        } else if(this.charAt(i)== ">"){
            result = result + "&gt;";
        } else {
            result = result + this.charAt(i);
        }
    }
    return result;
};

var notificationsOnScreen = {};
var notificationsDiv;

var notificationTemplate;
var linkTemplate;

function runNotificationViewer(currentUserName)
{
   $(document).ready(function()
   {
      notificationsDiv = $('#notificationsDiv');

      var notifications = $('.notification');
      for (var i = notifications.length - 1; i >= 0; i--)
         reformatNotification($(notifications[i]));

      // Connect back to the server to get COMET updates when the page changes
      CP_QualityReliable.connect(
         '/Users/' + currentUserName + '.user?ChannelEndpoint=IncomingNotificationEventThroughTemplate',
         {
            handleIncomingData: function(notificationXml)
            {
               var notification = $(notificationXml);
               notificationsDiv.prepend(notification);
               reformatNotification(notification);
            }
         });
   });
}

var url = Url.parseCurrent();

function reformatNotification(notification)
{
   var objectElements;
   var objectUrl = notification.attr('src');
   var senderIdentity = notification.attr('senderIdentity');

   $('a', notification).each(function()
   {
      var me = $(this);

      if (undefined == me.attr('target'))
         juiauto_makeiFrameLink(me);

      var href = me.attr('href');
      if (undefined != href)
         if (url.server != Url.parse(href).server)
            me.attr(
               'href',
               url.protocol + url.server + '/Shell/OpenID/OpenIDRedirect.oc?senderIdentity=' +
               escape(senderIdentity) + '&url=' + escape(objectUrl));
   });

   if (notificationsOnScreen[objectUrl])
   {
      notificationsOnScreen[objectUrl].remove();

      var notificationLinks = $('.notificationLinks', notification);
      notificationLinks.prepend($('.notificationLink', notificationsOnScreen[objectUrl]));
   }

   notificationsOnScreen[objectUrl] = notification;
}
