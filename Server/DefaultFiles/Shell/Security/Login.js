// Scripts:  /API/jquery.js, /Users/UserDB?Method=GetJSW&assignToVariable=UserDB

function Login_RegisterLogin(loginID)
{
   $('#loginButton_' + loginID)[0].click(function()
   {
      UserDB.Login(
         {
            username: $('#usernameInput_' + loginID')[0].value,
            password: $('#passwordInput_' + loginID')[0].value
         },
         function(responseText)
         {
            window.location.reload();
         });
   });

   $('#logoutButton_' + loginID)[0].click(function()
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
}

/*function Login_UpdateKeepAlive()
{
   var keepAliveInput = $("Login_KeepAlive");
   var maxAgeInput = $("Login_MaxAge");
   var maxAgeSpan = $("Login_MaxAgeSpan");

   var session = SessionManager.GetSession();

   keepAliveInput.checked = session.KeepAlive;
   maxAgeInput.value = session.MaxAge;

   if (session.KeepAlive)
      maxAgeSpan.show();
   else
      maxAgeSpan.hide();
}

function Login_OnKeepAliveChanged()
{
   SessionManager.SetKeepAlive(
      {
         KeepAlive: $("Login_KeepAlive").checked
      },
      Login_UpdateKeepAlive);
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