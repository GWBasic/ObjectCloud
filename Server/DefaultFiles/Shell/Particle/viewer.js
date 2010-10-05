// Scripts: /API/jquery.js, /Users/[name].user?Method=GetJSW&assignToVariable=NotificationsProxy, /API/Comet/CometProtocol.js

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

function reformatNotification(notification)
{
   var objectElements;
   var objectUrl = notification.attr('src');

   if (notificationsOnScreen[objectUrl])
   {
      notificationsOnScreen[objectUrl].remove();

      var notificationLinks = $('.notificationLinks', notification);
      notificationLinks.prepend($('.notificationLink', notificationsOnScreen[objectUrl]));
   }

   notificationsOnScreen[objectUrl] = notification;
}
