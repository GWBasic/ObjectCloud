// Scripts:  /API/jquery.js, /API/jquery-ui.js, /Users/UserDB?Method=GetJSW&assignToVariable=UserDB, /API/Filehandlers/SessionManager.js

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
   // Make sure that OpenID can properly redirect
   $('input.openId_redirect').val(window.location.href);

   // Register login / out handlers

   $('.Login_logoutButton').click(function()
   {
      var dialog = $('<div>Click OK to log out</div>').dialog(
      {
         modal:true,
         position:'top',
         title: 'Are you sure you want to log out?',
         buttons:
         {
            OK: function()
            {
               UserDB.Logout(
                  {},
                  function(responseText)
                  {
                     $('body').html('<div class="title">Logging out...</div>');
                     window.location.reload(true);
                  });
            },
            Cancel: function()
            {
               dialog.dialog('close');
            }
         }
      });

      return false;
   });

   var hiddenForm = $('.Login_hiddenForm');
   hiddenForm.hide();

   $('.Login_loginDialog').click(function()
   {
      var dialog = hiddenForm.dialog(
      {
         modal:true,
         position:'top',
         title: 'Login?'
      });

      return false;
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
            $('body').html('<div class="title">Logging in...</div>');
            window.location.reload(true);
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

   $('input.Login_MaxAge').keyup(function()
   {
      SessionManager.SetMaxAge(
         {
            MaxAge: $(this).val()
         },
         Login_UpdateKeepAlive);
   });

   Login_UpdateKeepAlive();
});