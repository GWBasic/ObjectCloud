// Scripts: /API/jquery.js, /API/Url.js, /Users/UserDB?Method=GetJSW&assignToVariable=userDB

$(document).ready(function()
{
   $('form.CreateGroup').submit(function()
   {
      var me = $(this);
      var groupname = me.find("[name$='groupname']").val();
      var displayName = me.find("[name$='displayName']").val();
      var grouptype = me.find("[name$='grouptype']").val();

      userDB.CreateGroup(
      {
         groupname: groupname,
         grouptype: grouptype,
         displayName: displayName
      },
      function(transport)
      {
         window.location.reload(true);
      });

      return false;
   });

   $('.DeleteGroup').click(function()
   {
      var me = $(this);
      var groupId = me.attr('groupid');

      userDB.DeleteGroup(
      {
         groupId: groupId
      },
      function(transport)
      {
         window.location.reload(true);
      });

      return false;
   });

   $('input.SetGroupAlias').change(function()
   {
      var me = $(this);
      var alias = me.val();

      var groupId = me.attr('groupid');
      var name = me.attr('groupname');

      if ((alias == '') || (alias == name))
      {
         me.val(name);

         userDB.SetGroupAlias(
         {
            groupId: groupId
         },
         function(transport)
         {
            window.location.reload(true);
         });
      }
      else
         userDB.SetGroupAlias(
         {
            groupId: groupId,
            alias: alias
         },
         function(transport)
         {
            window.location.reload(true);
         });
   });
});