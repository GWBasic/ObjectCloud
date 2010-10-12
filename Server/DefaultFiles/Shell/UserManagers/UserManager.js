// Scripts: /API/jquery.js

function setupUserManager(userwrapper)
{
   $(document).ready(function()
   {
      var passwordsDontMatch = $('.passwordsdontmatch');
      passwordsDontMatch.hide();

      var updatePasswordForm = $('form.changepassword');
      var newPassword = $('input.newpassword', updatePasswordForm);
      var verifyPassword = $('input.verifypassword', updatePasswordForm);

      var submit = $('input[type=submit]', updatePasswordForm);

      function verifyPasswordsMatch()
      {
         if (newPassword.val() == verifyPassword.val())
         {
            passwordsDontMatch.hide();
            submit.removeAttr('disabled');
         }
         else
         {
            passwordsDontMatch.show();
            submit.attr('disabled', 'disabled');
         }
      }

      newPassword.keyup(verifyPasswordsMatch);
      verifyPassword.keyup(verifyPasswordsMatch);

      updatePasswordForm.submit(function()
      {
         if (newPassword.val() == verifyPassword.val())
            userwrapper.SetPassword(
            {
               OldPassword: $('input.oldpassword', updatePasswordForm).val(),
               NewPassword: newPassword.val()
            });

         return false;
      });

      var changeAvatarForm = $('form.changeavatar');
      changeAvatarForm.submit(function()
      {
         userwrapper.Set(
         {
            name: 'Avatar',
            value: $('input.changeavatarclass', changeAvatarForm).val()
         });

         return false;
      });
   });
}