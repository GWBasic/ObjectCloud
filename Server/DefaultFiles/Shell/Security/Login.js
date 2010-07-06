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

   $('input.Login_loginButton').click(function()
   {
      var parent = $(this).parent();
      var usernameInput = parent.find("input[name$='username']")[0];
      var passwordInput = parent.find("input[name$='password']")[0];

      UserDB.Login(
         {
            username: usernameInput.value,
            password: passwordInput.value
         },
         function(responseText)
         {
            window.location.reload();
         });
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


/*function Login_RegisterLogin(loginID)
{

   });
}

function Login_OnKeepAliveChanged()
{
}

function Login_OnMaxAgeChanged()
{
   SessionManager.SetMaxAge(
      {
         MaxAge: $("Login_MaxAge").value,
      },
      Login_UpdateKeepAlive);
}
*/