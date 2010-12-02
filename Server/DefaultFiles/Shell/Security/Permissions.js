// Scripts: /API/jquery.js, /API/UserSuggest.js


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
         FilePermission: permission,
         Inherit: true,
         SendNotifications: true
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
         inherit: true,
         SendNotifications: true
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

         htmlBuilder = htmlBuilder + '<tr id="' + permission.Id + '"><td><img src="' + permission.AvatarUrl.replace(/&/, '&amp;')
            + '&amp;width=100&amp;maxHeight=100" /> ' + permission.DisplayName + '</td><td>' 
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

      $('input.UserOrGroupInput').each(function()
      {
         enableUserSuggest(this);
      });

      $('form.addForm').submit(function()
      {
         var userOrGroupInput = $("input:text[name=UserOrGroup]", this);
         var userOrGroup = userOrGroupInput.val();
         var filePermission = $("[name=FilePermission]", this).val();

         var me = $(this);
         me.attr('disabled', 'disabled');

         File.SetPermission(
            {
               UserOrGroup: userOrGroup,
               FilePermission: filePermission,
               Inherit: true,
               SendNotifications: true,
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

      $('form.chown').submit(function()
      {
         var ownerHasPermission = false;

         // Warn if the owner isn't an administrator
         if (null != File.Owner)
         {
            for (var i = 0; i < Permissions.length; i++)
               if (Permissions.Name == File.Owner)
                  if (Permissions.Permission == "Administer")
                     ownerHasPermission = true;

            if (!ownerHasPermission)
               if (!confirm(File.Owner + 
                  ' doesn\'t have Administer permission to this object! Changing Ownership means that ' +
                  File.Owner + ' will not be able to do as many things with this object. Are you sure you want to do this?'))
               {
                  return false;
               }
         }

         var newOwner = $("input:text[name=newOwner]", this).val();
         File.Chown(
            {
               newOwner: newOwner
            },
            function(result)
            {
               alert(result);
               window.location.href = window.location.href;
            });

         return false;
      });
   });
}