// This code is released under the LGPL
// See /Docs/license.wchtml

var User =
{
   /**
    * Returns an object that can be used to manipulate the user with the given username
    *
    * @param username The username
    * @returns An object to manipulate the user
    */ 
   Open: function(username)
   {
      var toReturn =
      {
         Username: username,

         /**
          * Changes the user's password
          *
          * @param oldPassword The old password, required for security purposes
          * @param newPassword The new password
          * @param onSuccess Called on success, passed the transport object
          * @param onFailure Called on failure, passed the transport object
          */
         SetPassword: function(oldPassword, newPassword, onSuccess, onFailure)
         {
            var parameters =
            {
               OldPassword: oldPassword,
               NewPassword: newPassword
            };

            new Ajax.Request("/Users/" + this.Username + ".user?Method=SetPassword",
            {
               method: 'post',
               parameters: parameters,
               onSuccess: onSuccess,
               onFailure: onFailure
            });
         }
      };

      return toReturn;
   }
};