// Scripts: /API/jquery.js, /API/Url.js

function setupCreateAccount(assignSession, welcomePage)
{
   $(document).ready(function()
   {
      $('form.createAccount').submit(function()
      {
         var me = $(this);
         var usernameInput = me.find("input[name$='username']");
         var passwordInput = me.find("input[name$='password']");

         UserDB.CreateUser(
            {
               username: usernameInput.val(),
               password: passwordInput.val(),
               assignSession: assignSession
            },
            function(result)
            {
               alert(result);

               if (assignSession)
               {
                  var url = Url.parseCurrent();
                  window.location.href = 'http://' + url.server + welcomePage;
               }
            });

         return false;
      });
   });
}