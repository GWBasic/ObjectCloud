// Scripts:  /API/autosuggest2.js, /Users/UserDB?Method=GetJSW&assignToVariable=UserDB

// Suggests users
// Note:  When using this script, make sure that your CSS includes declarations like found in autosuggest.css

// To enable user suggest on a text box, get the text box using something like the $() function, and then pass it to enableUserSuggest()

/* Enables user suggest for the passed-in text box */
function enableUserSuggest(textBox)
{
      return new AutoSuggestControl(
         textBox,
         {
            requestSuggestions : function (oAutoSuggestControl /*:AutoSuggestControl*/,
                                                          bTypeAhead /*:boolean*/)
            {
               var sTextboxValue = oAutoSuggestControl.textbox.value;
    
               if (sTextboxValue.length > 0)
               {
                  UserDB.SearchUsersAndGroups(
                     { query: sTextboxValue },
                     function(matchingUsers)
                     {
                        var usersArray = [];

                        for (var i = 0; i < matchingUsers.length; i++)
                           usersArray.push(matchingUsers[i].Name);

                        oAutoSuggestControl.autosuggest(usersArray, bTypeAhead);
                     });
               }
            }
         });
}