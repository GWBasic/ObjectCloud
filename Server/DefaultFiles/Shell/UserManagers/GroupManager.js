// Scripts: /API/jquery.js, /API/Url.js, /Users/UserDB?Method=GetJSW&assignToVariable=userDB

$(document).ready(function()
{
   $('form.CreateGroup').submit(function()
   {
      var me = $(this);
      var groupname = me.find("[name$='groupname']").val();
      var grouptype = me.find("[name$='grouptype']").val();

      userDB.CreateGroup(
      {
         groupname: groupname,
         grouptype: grouptype
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
});

/*

Uncomment when re-enabling group aliases

function clearGroupAlias(groupId)
{
   $("Alias_" + groupId).value = "";
   setGroupAlias(groupId);
}

function setGroupAlias(groupId)
{
   userDB.SetGroupAlias(
   {
      groupId: groupId,
      alias: $("Alias_" + groupId).value
   },
   function(transport)
   {
      window.location.reload(true);
   });
}*/
