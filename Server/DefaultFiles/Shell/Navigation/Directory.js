// Scripts: /API/Prototype.js, /API/buttonmenu.js

   function displayFiles(directory, files)
   {
      var htmlBuilder = '<table><tr><th>File name</th><th>My Permission</th><th>Owner</th><th>Last Modified</th><th>Created</th></tr>';
      
      // Sort by name
      files.sort(function(a, b)
      {
         var aS = a.Filename.toLowerCase();
         var bS = b.Filename.toLowerCase();

         if (aS < bS)
            return -1;
         if (aS > bS)
            return 1;

         return 0;
      });

      var menus = {};

      files.each(function(file, i)
      {
         // Filename and menu
         htmlBuilder += '<tr><td><span style="font-size: 2em"><a id="' + i + '_link' + '" href="' + directory + file.Filename + '"';

         if (file.TypeId != "directory")
            htmlBuilder += ' target="_blank"';

         htmlBuilder += '>' + file.Filename + '</a></span>';

         // Metadata
         htmlBuilder += "<td>" + file.Permission + "</td>";
         htmlBuilder += "<td>" + file.Owner + "</td>";
         htmlBuilder += "<td>" + new Date(file.LastModified) + "</td>";
         htmlBuilder += "<td>" + new Date(file.Created) + "</td>";
         htmlBuilder += "</tr>";

         // Create the menu.
         var menuItems = [];

         if (("Read" == file.Permission) || ("Write" == file.Permission) || ("Administer" == file.Permission))
            menuItems.push(
            {
               name: 'View',
               callback: function()
               {
                  window.open(directory + file.Filename);
               }
            });

         if (("Write" == file.Permission) || ("Administer" == file.Permission))
            menuItems.push(
            {
               name: 'Edit',
               callback: function()
               {
                  window.open(directory + file.Filename + '?Action=Edit');
               }
            });

         if ("Administer" == file.Permission)
         {
            menuItems.push(
            {
               name: 'Permissions',
               callback: function()
               {
                  window.open('/Shell/Security/Permissions.wchtml?FileName=' + directory + file.Filename);
               }
            });

            menuItems.push(
            {
               separator: true
            });

            menuItems.push(
            {
               name: 'Rename',
               callback: function()
               {
                  var newFilename = prompt("Enter new file name", file.Filename);

                  if (null != newFilename)
                  {
                     Dir.RenameFile(
                        file.Filename,
                        newFilename,
                        {},
                        function() {});
                  }
               }
            });

            menuItems.push(
            {
               name: 'Defrag',
               callback: function()
               {
                  new Ajax.Request(
                     directory + file.Filename + '?Method=Vacuum',
                     {
                        method: 'post',
                        onSuccess: function(transport)
                        {
                           alert("Vacuum successful");
                        },
                        onFailure: function(transport)
                        {
                           alert("Vacuum failed");
                        }
                     });
               }
            });

            menuItems.push(
            {
               name: 'Get Server-Side Javascript Errors',
               callback: function()
               {
                  window.open(directory + file.Filename + '?Method=GetServersideJavascriptErrors');
               }
            });

            menuItems.push(
            {
               separator: true
            });

            menuItems.push(
            {
               name: 'Delete',
               callback: function()
               {
                  if (confirm("Delete " + file.Filename))
                  {
                     Dir.DeleteFile(
                        file.Filename,
                        {},
                        function() {});
                  }
               }
            });
         }

         menus[i + '_link'] = menuItems;
      });

      htmlBuilder += '</table>';

      $("FilesDiv").innerHTML = htmlBuilder;

      for (var element in menus)
      {
         Proto.CreateMenu(menus[element], $(element));
      }
   }