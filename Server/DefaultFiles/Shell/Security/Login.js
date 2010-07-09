// Scripts:  /API/jquery.js, /Users/UserDB?Method=GetJSW&assignToVariable=UserDB, /API/Filehandlers/SessionManager.js

function Login_UpdateKeepAlive()
{
   var session = SessionManager.GetSession();

   if (session.KeepAlive)
   {
      $('input.Login_KeepAlive').attr('checked', 'checked');
      $('input.Login_MaxAge').attr('value', session.MaxAge);
      $('.Login_MaxAgeSpan').show();
   }
   else
      $('.Login_MaxAgeSpan').hide();
}

$(document).ready(function()
{
   // Register login / out handlers

   $('input.Login_logoutButton').click(function()
   {
         if (confirm("Are you sure you want to log out?"))
         {
            UserDB.Logout(
               {},
               function(responseText)
               {
                  window.location.reload();
               });
         }
   });

   $('form.Login_LoginForm').submit(function()
   {
      var me = $(this);
      var usernameInput = me.find("input[name$='username']")[0];
      var passwordInput = me.find("input[name$='password']")[0];

      UserDB.Login(
         {
            username: usernameInput.value,
            password: passwordInput.value
         },
         function(responseText)
         {
            window.location.reload();
         });

      return false;
   });

   $('input.Login_KeepAlive').click(function()
   {
      SessionManager.SetKeepAlive(
         {
            KeepAlive: this.checked
         },
         Login_UpdateKeepAlive);
   });

   Login_UpdateKeepAlive();

});