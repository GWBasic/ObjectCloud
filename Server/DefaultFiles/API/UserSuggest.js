// Scripts:  /API/jquery.js, /Users/UserDB?Method=GetJSW&assignToVariable=UserDB

// Suggests users

// To enable user suggest on a text box, get the text box using something like the $() function, and then pass it to enableUserSuggest()

/* Enables user suggest for the passed-in text box */
function enableUserSuggest(textBox, clickCallback)
{
   textBox = $(textBox);

   var previousDialog = null;

   textBox.keyup(function()
   {
      var text = textBox.val();
    
      if (text.length > 0)
      {
         var pluginArgs = [];
         for (var i = 0; i < userSuggestIdentityPluginArgumentsFunctions.length; i++)
            pluginArgs.push(userSuggestIdentityPluginArgumentsFunctions[i]());

         UserDB.SearchUsersAndGroups(
            {
               query: text,
               pluginArgs: pluginArgs
            },
            function(matchingUsers)
            {
               if (matchingUsers.length == 0)
                  return;

               if (null != previousDialog)
                  previousDialog.remove();

               var pos = textBox.position();
               var suggestDiv = $('<div style="border: solid; background: white; position: absolute; left: ' + pos.left + '; top: ' + (pos.top + textBox.height()) + '" />');
               previousDialog = suggestDiv;

               suggestDiv.insertAfter(textBox);

               for (var i = 0; i < matchingUsers.length; i++)
               {
                  var suggestLink = $('<a href="" />');

                  suggestLink.html('<img src="' + matchingUsers[i].AvatarUrl.replace(/&/, '&amp;')
                     + '&amp;height=50&amp;BrowserCache=FB" />' + matchingUsers[i].DisplayName);

                  suggestDiv.append(suggestLink);
                  suggestDiv.append('<br />');

                  suggestLink[0].ObjectCloudUser = matchingUsers[i];

                  suggestLink.click(function()
                  {
                     textBox.val(this.ObjectCloudUser.Name);
                     if (clickCallback)
                        clickCallback(this.ObjectCloudUser);

                     previousDialog = null;
                     suggestDiv.remove();

                     return false;
                  });
               }
            });
      }
      else if (null != previousDialog)
         previousDialog.remove();
   });
}

// If this variable is set, push functions into here to return arguments when trying to do autosuggest
var userSuggestIdentityPluginArgumentsFunctions = [];