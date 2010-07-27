// Scripts: /API/jquery.js, /API/Url.js

// Updates the various inputs to match the selected file type
function UpdateCreateNewFile(filetypes, me)
{
   var fileNameInput = $("input.FileNameInput");
   var selectedFileType = filetypes[me.selectedIndex];

   // Remove the extension from the input
   var extensionPos = fileNameInput.val().lastIndexOf(".");

   var currentPrefix;
   if (extensionPos >= 0)
      currentPrefix = fileNameInput.val().substring(0, extensionPos);
   else
      currentPrefix = fileNameInput.val();

   fileNameInput.val(currentPrefix + selectedFileType.Extension);
}

function newFile(filetypes, me, dir)
{
   var selectedFileTypeInput = $("select.SelectFileType", me);
   var selectedFileType = filetypes[selectedFileTypeInput.val()];
   var source = selectedFileType.Template;
   var destination = $("input.FileNameInput", me).val();

   dir.CopyFile(
      {
         SourceFilename: source,
         DestinationFilename: destination
      },
      function()
      {
         alert(destination + " created");
      });
}

function copyFile(me, dir)
{
   var source = $("input.sourceFileNameInput", me).val();
   var destination = $("input.destinationFileNameInput", me).val();

   dir.CopyFile(
      {
         SourceFilename: source,
         DestinationFilename: destination
      },
      function()
      {
         alert(source + " copied to " + destination);
      });
}

function setIndexFile(me, dir)
{
   var indexFile = $("input.indexFile").val();

   dir.SetIndexFile(
      {
         IndexFile: indexFile,
      },
      function(message)
      {
         alert(message);
      });
}

function doDirectory(directory, filetypes, dir)
{
   $(document).ready(function()
   {
      $('select.SelectFileType').change(function()
      {
         UpdateCreateNewFile(filetypes, this);
      });
      $('select.SelectFileType').change();

      var url = Url.parseCurrent();
      var server = url.server;

      $('form.newFile').submit(function()
      {
         newFile(filetypes, this, dir);
         return false;
      });

      $('form.copyFile').submit(function()
      {
         copyFile(this, dir);
         return false;
      });

      $('form.setIndexFile').submit(function()
      {
         setIndexFile(this, dir);
         return false;
      });

      var newTitle = '<a href="/">http://' + server + '</a>&#160;';
 
      // Display links to all parent directories
      var parentDirs = directory.split("/");
      var pathBuilder = "";

      for (var i = 0; i < parentDirs.length; i++)
      {
         var parentDir = parentDirs[i];

         if (parentDir.length > 0)
         {
            pathBuilder = pathBuilder + parentDir + "/";
            newTitle += '/ <a href="/' + pathBuilder + '">' + parentDir + '</a> ';
         }
      }

      $('.title').html(newTitle);
   });
}

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

   for (var i = 0; i < files.length; i++)
   {
      var file = files[i];

      // Filename and menu
      htmlBuilder += '<tr><td><span style="font-size: 2em"><a id="' + i + '_link' + '" href="' + directory + '/' + file.Filename + '"';

      if (file.TypeId != "directory")
         htmlBuilder += ' target="_blank"';

      htmlBuilder += '>' + file.Filename + '</a></span></td>';

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
               window.open('/Shell/Security/Permissions.oc?FileName=' + directory + file.Filename);
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
                     {
                        OldFileName: file.Filename,
                        NewFileName: newFilename
                     },
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
                     {
                        FileName: file.Filename,
                     },
                     function() {});
               }
            }
         });
      }

      menus[i + '_link'] = menuItems;
   }

   htmlBuilder += '</table>';

   $(".filesDiv").html(htmlBuilder);

   /*for (var element in menus)
   {
      Proto.CreateMenu(menus[element], $(element));
   }*/
}