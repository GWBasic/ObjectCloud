var DefaultNamedPermissions_JustNames = [];
var Permissions;
var defaultNamedPermissions;

function UpdatePermission(userOrGroupId)
{
   var permissionElement = $('#FilePermission_' + userOrGroupId);
   var permission = permissionElement.val();

   if ("" == permission)
      for (var i = 0; i < defaultNamedPermissions.length; i++)
      {
         var namedPermissionCheckbox = $('#' + defaultNamedPermissions[i].NamedPermission + '_' + userOrGroupId);
         if (namedPermissionCheckbox.is(':checked'))
         {
            namedPermissionCheckbox.removeAttr('checked');
            UpdateNamedPermission(defaultNamedPermissions[i].NamedPermission, userOrGroupId)
         }
      }

   permissionElement.attr('disabled', 'disabled');

   File.SetPermission(
      {
         UserOrGroupId: userOrGroupId,
         FilePermission: permission
      },
      function(transport)
      {
         if ("" == permission)
            $('#' + userOrGroupId).remove();
         else
            permissionElement.removeAttr('disabled', null);
      });
}

function UpdateNamedPermission(namedPermission, userOrGroupId)
{
   if ($('#' + namedPermission + '_' + userOrGroupId).is(':checked'))
      File.SetNamedPermission(
      {
         UserOrGroupId: userOrGroupId,
         namedPermission: namedPermission,
         inherit: true
      }, function(){});
   else
      File.RemoveNamedPermission(
      {
         UserOrGroupId: userOrGroupId,
         namedPermission: namedPermission
      }, function(){});
}

function doPermissions(inPermissions, inDefaultNamedPermissions)
{
   Permissions = inPermissions;
   defaultNamedPermissions = inDefaultNamedPermissions;

   for (var i = 0; i < defaultNamedPermissions.length; i++)
      if (defaultNamedPermissions[i].Default)
         DefaultNamedPermissions_JustNames.push(defaultNamedPermissions[i].NamedPermission);

   function displayPermissions()
   {
      // This dates back to old document.writes...  Someday it should be cleaned up and use proper jQuery event handlers!

      var htmlBuilder = '<table><tr><th>User or Group</th><th>Level</th>';

      for (var i = 0; i < defaultNamedPermissions.length; i++)
         htmlBuilder += '<th>' + defaultNamedPermissions[i].Label + '</th>';

      htmlBuilder += '</tr>';

      for (var pi = 0; pi < Permissions.length; pi++)
      {
         var permission = Permissions[pi];

         htmlBuilder = htmlBuilder + '<tr id="' + permission.Id + '"><td><img src="' + permission.Identity
            + '?Method=GetAvatar&amp;maxWidth=100" /> ' + permission.Name + '</td><td>' 
            + '<select id="FilePermission_' + permission.Id + '" ';
         htmlBuilder = htmlBuilder + 'onchange="UpdatePermission(' + "'" + permission.Id + "'" + ')" >';
               
         htmlBuilder = htmlBuilder + '<option value="" >(Remove)</option>';
                  
         htmlBuilder = htmlBuilder + '<option value="Read"';
         if ("Read" == permission.Permission)
            htmlBuilder = htmlBuilder + ' selected="true"';
         htmlBuilder = htmlBuilder + ' >Read</option>';

         htmlBuilder = htmlBuilder + '<option value="Write"';
         if ("Write" == permission.Permission)
            htmlBuilder = htmlBuilder + ' selected="true"';
         htmlBuilder = htmlBuilder + ' >Write</option>';

         htmlBuilder = htmlBuilder + '<option value="Administer"';
         if ("Administer" ==  permission.Permission)
            htmlBuilder = htmlBuilder + ' selected="true"';
         htmlBuilder = htmlBuilder + ' >Administer</option>';

         htmlBuilder = htmlBuilder + '</select>';

         htmlBuilder = htmlBuilder + '</td>';

         for (var i = 0; i < defaultNamedPermissions.length; i++)
         {
            htmlBuilder += '<td>';
            htmlBuilder += '<input type="checkbox"  id="' + defaultNamedPermissions[i].NamedPermission + '_' + permission.Id + '"';
            htmlBuilder += ' onchange="UpdateNamedPermission(' + "'" + defaultNamedPermissions[i].NamedPermission + "','" + permission.Id + "'" + ')"';

            if (permission.NamedPermissions[defaultNamedPermissions[i].NamedPermission])
               htmlBuilder += ' checked="true"';

            htmlBuilder += '/></td>';
         }

         htmlBuilder = htmlBuilder + '</tr>';
      }

      htmlBuilder = htmlBuilder + "</table>";

      $("div.PermissionsDiv").html(htmlBuilder);
   }


   $(document).ready(function()
   {
      displayPermissions();

      enableUserSuggest($('input.UserOrGroupInput')[0]);

      $('form.addForm').submit(function()
      {
         var userOrGroupInput = $("[name=UserOrGroup]", this);
         var userOrGroup = userOrGroupInput.val();
         var filePermission = $("[name=FilePermission]", this).val();

         var me = $(this);
         me.attr('disabled', 'disabled');

         File.SetPermission(
            {
               UserOrGroup: userOrGroup,
               FilePermission: filePermission,
               Inherit: true,
               SendNotifications: false,
               namedPermissions: DefaultNamedPermissions_JustNames
            },
            function(result)
            {
               File.GetPermissions(
                  {},
                  function(permissions)
                  {
                     Permissions = permissions;
                     displayPermissions();
                  });

               UserOrGroupInput.val("");

               me.removeAttr('disabled');
            },
            function(transport)
            {
               alert(transport.responseText);
               me.removeAttr('disabled');
            });

         return false;
      });
   });
}