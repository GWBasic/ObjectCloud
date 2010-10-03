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

$(document).ready(function()
{
   notificationsDiv = $('#notificationsDiv');

   NotificationsProxy.GetNotifications(
      {
         maxNotifications: 25
      },
      function(notifications)
      {
         for (var i = notifications.length - 1; i >= 0; i--)
            displayNotification(notifications[i]);

         // Connect back to the server to get COMET updates when the page changes
         CP_QualityReliable.connect(
            '/Users/' + currentUserName + '.user?ChannelEndpoint=IncomingNotificationEvent',
            {
               handleIncomingData: function(notification)
               {
                  displayNotification(notification);
               }
            });
      });
});

function displayNotification(notification)
{
   var objectElements;
   if (notificationsOnScreen[notification.objectUrl])
      objectElements = notificationsOnScreen[notification.objectUrl];
   else
   {
      var notificationDiv = $('<div class="notification" />');
      var summaryDiv = $('<div />');

      var repliesDiv = $('<div class="notificationLinks" />');
      notificationDiv.append(summaryDiv);
      notificationDiv.append(repliesDiv);

      objectElements =
      {
         notificationDiv: notificationDiv,
         summaryDiv: summaryDiv,
         repliesDiv: summaryDiv
      };

      notificationsOnScreen[notification.objectUrl] = objectElements;
   }

   notificationsDiv.prepend(objectElements.notificationDiv);

   var notificationCopy = JSON.parse(JSON.stringify(notification));
   delete notificationCopy.changeData;


   objectElements.summaryDiv.html(JSON.stringify(notificationCopy).escapeHTML());

   if ("link" == notification.verb)
   {
      var replyDiv = $('<div class="notificationLink" />');
      replyDiv.html(JSON.stringify(notification.changeData));

      objectElements.repliesDiv.append(replyDiv);
   }
}