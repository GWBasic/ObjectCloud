// Scripts: /API/jquery.js, /API/Url.js, /API/jquery.contextButton.js, /API/jquery.ui.menu.js, /API/AJAX.js, /API/shareDialog.js

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
         IndexFile: indexFile
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

function displayFiles(directory, files, dir)
{
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

//   var menus = {};
   var filesDiv = $(".filesDiv");
   filesDiv.empty();
   var table = $('<table />');
   filesDiv.append(table);

   table.append('<tr><th>File name</th><th>Permission</th><th>Owner</th><th>Last Modified</th><th>Created</th></tr>');

   function createFileRow(file)
   {
      var row = $('<tr></tr>');
      table.append(row);

      var filenameCell = $('<td />');
      var filenameLink = $('<a style="font-size: 1.5em" href="' + directory + file.Filename + '">' + file.Filename + '</a>');

      if (file.TypeId != "directory")
         filenameLink.attr('target', '_blank'); 

      filenameCell.append(filenameLink);

      var menuList = $('<ul />');
      filenameCell.append(menuList);

      row.append(filenameCell);

      // Metadata
      row.append("<td>" + file.Permission + "</td>");
      row.append("<td>" + file.Owner + "</td>");
      row.append("<td>" + new Date(file.LastModified) + "</td>");
      row.append("<td>" + new Date(file.Created) + "</td>");

      // Create the menu.

      if (("Read" == file.Permission) || ("Write" == file.Permission) || ("Administer" == file.Permission))
         menuList.append($('<li><a>View</a></li>').click(function()
         {
            window.open(directory + file.Filename);
         }));

      if (("Write" == file.Permission) || ("Administer" == file.Permission))
         menuList.append($('<li><a>Edit</a></li>').click(function()
         {
            window.open(directory + file.Filename + '?Action=Edit');
         }));

      if ("Administer" == file.Permission)
      {
         menuList.append($('<li><a>Share</a></li>').click(function()
         {
            shareDialog_show(directory + file.Filename);
         }));

         menuList.append($('<li>---------</li>'));

         menuList.append($('<li><a>Rename</a></li>').click(function()
         {
            var newFilename = prompt("Enter new file name", file.Filename);
            if (null != newFilename)
            {
               dir.RenameFile(
                  {
                     OldFileName: file.Filename,
                     NewFileName: newFilename
                  },
                  function() {});
            }
         }));

         menuList.append($('<li><a>Defrag</a></li>').click(function()
         {
            POST(
               directory + file.Filename + '?Method=Vacuum',
               null,
               null,
               function(transport)
               {
                  alert("Defrag successful");
               },
               function(transport)
               {
                  alert("Defrag failed");
               });
         }));

         menuList.append($('<li><a>Get Server-Side Javascript Errors</a></li>').click(function()
         {
            window.open(directory + file.Filename + '?Method=GetServersideJavascriptErrors');
         }));

         menuList.append($('<li>---------</li>'));

         menuList.append($('<li><a>Delete</a></li>').click(function()
         {
            if (confirm("Delete " + file.Filename))
            {
               dir.DeleteFile(
                  {
                     FileName: file.Filename
                  },
                  function() {});
            }
         }));
      }

      try
      {
         menuList.contextMenu();
      }
      // The context menu doesn't work on Safari
      catch (exception) {}
   }

   for (var i = 0; i < files.length; i++)
      createFileRow(files[i]);
   //{
     // var file = files[i];

   //}
}