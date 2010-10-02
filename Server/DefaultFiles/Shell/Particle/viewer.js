// Scripts: /API/jquery.js, /Users/[name].user?Method=GetJSW&assignToVariable=NotificationsProxy

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
         for (var i = 0; i < notifications.length; i++)
            displayNotification(notifications[i]);
      });
});

function displayNotification(notification)
{
   var notificationDiv = $('<div />');
   notificationDiv.html(JSON.stringify(notification));

   notificiationsOnScreen[notification.objectUrl] = notificationDiv;

   notificationsDiv.append(notificationDiv);
}