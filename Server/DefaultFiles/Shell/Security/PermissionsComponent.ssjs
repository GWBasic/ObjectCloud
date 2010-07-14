<? Scripts(/API/Prototype.js, <? $_GET["FileName"] ?>?Method=GetJSW&assignToVariable=File, /API/UserSuggest.js) ?>

<?
   scope.filename = '<? $_GET["FileName"] ?>';
   scope.extension = scope.filename.substring(scope.filename.lastIndexOf('.') + 1);

   try
   {
      scope.defaultNamedPermissionsString = open('/Actions/Security/ByExtension/' + scope.extension + '.json').ReadAll_Sync();
   }
   catch (exception)
   {
      scope.defaultNamedPermissionsString = '[]';
   }

   //scope.defaultNamedPermissionsString;
   true;
?>

   <script>
      var Permissions = <? WebComponent($_GET["FileName"] . "?Method=GetPermissions") ?>;
      var defaultNamedPermissions = <? scope.defaultNamedPermissionsString; ?>;
      var DefaultNamedPermissions_JustNames = [];

      for (var i = 0; i < defaultNamedPermissions.length; i++)
         if (defaultNamedPermissions[i].Default)
            DefaultNamedPermissions_JustNames.push(defaultNamedPermissions[i].NamedPermission);

      function UpdatePermission(userOrGroupId)
      {
         var permissionElement = $('FilePermission_' + userOrGroupId);
         var permission = permissionElement.value;

         if ("" == permission)
            for (var i = 0; i < defaultNamedPermissions.length; i++)
            {
               var namedPermissionCheckbox = $(defaultNamedPermissions[i].NamedPermission + '_' + userOrGroupId);
               if (namedPermissionCheckbox.checked)
               {
                  namedPermissionCheckbox.checked = false;
                  UpdateNamedPermission(defaultNamedPermissions[i].NamedPermission, userOrGroupId)
               }
            }

         permissionElement.disable();

         File.SetPermission(
            {
               UserOrGroupId: userOrGroupId,
               FilePermission: permission
            },
            function(transport)
            {
               permissionElement.enable();

               if ("" == permission)
                  $(userOrGroupId).remove();
            });
      }

      function UpdateNamedPermission(namedPermission, userOrGroupId)
      {
         if ($(namedPermission + '_' + userOrGroupId).checked)
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

      function DisplayPermissions()
      {
         var htmlBuilder = '<table><tr><th>User or Group</th><th>Level</th>';

         for (var i = 0; i < defaultNamedPermissions.length; i++)
            htmlBuilder += '<th>' + defaultNamedPermissions[i].Label + '</th>';

         htmlBuilder += '</tr>';

         Permissions.each(function(permission, i)
         {
            htmlBuilder = htmlBuilder + '<tr id="' + permission.Id + '"><td><img src="' + permission.Identity
               + '?Method=GetAvatar&maxWidth=100" /> ' + permission.Name + '</td><td>' 
               + '<select id="FilePermission_' + permission.Id + '" ';
            htmlBuilder = htmlBuilder + 'onchange="UpdatePermission(' + "'" + permission.Id + "'" + ')" >';
                  
            htmlBuilder = htmlBuilder + '<option value="" >(Remove)</option>';
                  
            htmlBuilder = htmlBuilder + '<option value="Read"';
            if ("Read" == permission.Permission)
               htmlBuilder = htmlBuilder + ' selected';
            htmlBuilder = htmlBuilder + ' >Read</option>';

            htmlBuilder = htmlBuilder + '<option value="Write"';
            if ("Write" == permission.Permission)
               htmlBuilder = htmlBuilder + ' selected';
            htmlBuilder = htmlBuilder + ' >Write</option>';

            htmlBuilder = htmlBuilder + '<option value="Administer"';
            if ("Administer" ==  permission.Permission)
               htmlBuilder = htmlBuilder + ' selected';
            htmlBuilder = htmlBuilder + ' >Administer</option>';

            htmlBuilder = htmlBuilder + '</select>';

            htmlBuilder = htmlBuilder + '</td>';

            for (var i = 0; i < defaultNamedPermissions.length; i++)
            {
               htmlBuilder += '<td>';
               htmlBuilder += '<input type="checkbox"  id="' + defaultNamedPermissions[i].NamedPermission + '_' + permission.Id + '"';
               htmlBuilder += ' onchange="UpdateNamedPermission(' + "'" + defaultNamedPermissions[i].NamedPermission + "','" + permission.Id + "'" + ')"';

               if (permission.NamedPermissions[defaultNamedPermissions[i].NamedPermission])
                  htmlBuilder += ' checked';

               htmlBuilder += '/></td>';
            }

            htmlBuilder = htmlBuilder + '</tr>';
         });

         htmlBuilder = htmlBuilder + "</table>";

         var permissionsDiv = $("PermissionsDiv");
         permissionsDiv.innerHTML = htmlBuilder;
      }

      function addPermission()
      {
         var addForm = $("addForm");
         var userOrGroup = $("UserOrGroupInput").value;
         var filePermission = $("FilePermissionInput").value;

         addForm.disable();

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
                     DisplayPermissions();
                  });

               alert(result);
               $("UserOrGroupInput").value = "";

               addForm.enable();
            },
            function(transport)
            {
               alert(transport.responseText);
               addForm.enable();
            });

         return false;
      }

   </script>

   <h1>Permissions for <? $_GET["FileName"] ?></h1>
   <div id="PermissionsDiv">Loading...</div>

   <h1>Add a Permission</h1>
   <form id="addForm" onsubmit="return false" >
      User, Group, or <img src="/Shell/OpenID/login-bg.gif" /> OpenID: <input type="text" name="UserOrGroup" id="UserOrGroupInput" />
      Level: <select name="FilePermission" id="FilePermissionInput" >
         <option value="Read">Read</option>
         <option value="Write">Write</option>
         <option value="Administer">Administer</option>
      </select>
      <input type="submit" value="add" onclick="addPermission()" />
   </form>

   <? WebComponent("/Shell/Security/PermissionInstructions.webcomponent") ?>

   <script>
      DisplayPermissions();
      var userTextBox = enableUserSuggest($('UserOrGroupInput'));
   </script>