// Scripts: /API/jquery.js, /API/Url.js

function setupCreateAccount(assignSession, welcomePage, userNumbers)
{
   $(document).ready(function()
   {
      var totalusers = $('.totalusers');
      totalusers.html(totalusers.html() + userNumbers.TotalLocalUsers);

      var maxusers = $('.maxusers');
      var usersfull = $('.usersfull');

      if (!userNumbers.MaxLocalUsers)
      {
         maxusers.remove();
         usersfull.remove();         
      }
      else
      {
         maxusers.html(maxusers.html() + userNumbers.MaxLocalUsers);

         if (userNumbers.TotalLocalUsers >= userNumbers.MaxLocalUsers)
            $('.createAccount').remove();
         else
            usersfull.remove();
      }

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