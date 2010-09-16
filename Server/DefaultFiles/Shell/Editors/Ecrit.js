// Scripts: /API/jquery.js, /API/jquery-ui.js, /API/jquery.rte.js, /API/jquery.rte.tb.js, /API/shareDialog.js, /API/Url.js, /API/htmlparser.js

var titleInput;
var editor;
var page;
var contentsBack;

function setUpEditor(text)
{
   var contents = $('<textarea />');

   contentsBack.empty();
   contentsBack.height(0);
   contentsBack.append(contents);
   contents.val(text);

   var documentHeight = $(document).height();
   var editorTop = contents.offset().top;

   var editorHeight = 0.975 * (documentHeight - editorTop);

   editor = contents.rte(
   {
      controls_rte: rte_toolbar,
      controls_html: html_toolbar,
      width: titleInput.width(),

      // 60 is a hardcoded estimate for the size of the toolbars
      // The RTE doesn't adjust its height for toolbars
      height: editorHeight - 60
   })[0];

   // The rte is transparent, thus an empty div needs to be superimposed
   // behind it
   contentsBack.height(editorHeight);

   var rte = $('.rte-zone');
   rte.css( { position: 'absolute' });
}

function Ecrit(filename, inPage)
{
   page = inPage;

   $(document).ready(function()
   {
      $('.footer').remove();
      $('#footer').remove();
      contentsBack = $('#contentsBack');

      titleInput = $('input.documentTitle')

      // For some reason, .change isn't working
      titleInput.keyup(function()
      {
         document.title = "Editing: " + $(this).val();
      });
      titleInput.val(page.Title);
      titleInput.keyup();

      $('.share').click(function()
      {
         shareDialog_show(filename);
         return false;
      });

      var url = Url.parseCurrent();
      var href = url.protocol + url.server + filename;
      $('.view').attr('href', href);

      $('.preview').click(function()
      {
         var previewDialog = $('<div />');
         previewDialog.append($('<div class="title">' + escape(titleInput.val()) + '</div>'));
         previewDialog.append(editor.get_content());

         previewDialog.dialog(
         {
            modal:true,
            position:'top',
            height: $('#contentsBack').height(),
            width: 800,
            title: 'Preview'
         });

         return false;
      });

      $('.save').click(function()
      {
         var newPage = 
         {
            Title: titleInput.val(),
            Contents: editor.get_content()
         };

         var converted =
         {
            Title: titleInput.val(),
            Contents: editor.get_content()
         };

         try
         {
            converted.Contents = HTMLtoXML(converted.Contents);
         }
         catch (exception)
         {
            alert("Warning:  Could not convert page to proper XML\n" + exception);
         }

         // TODO:  The user shouldn't be able to click the X to close the dialog
         var savingDialog = $('<div>Saving...</div>').dialog(
         {
            disabled: true,
            closeOnEscape: false,
            modal: true,
            position: 'center'
         });

         if (object_target.TypeId != 'directory')
            object_target.WriteAll(
               JSON.stringify(converted),
               function()
               {
                  savingDialog.dialog('close');
                  page = newPage;
               },
               function(transport)
               {
                  savingDialog.html(transport.responseText);
               });
         else
            // Special case when the target is a directory, instead of writing to the file,
            // a new one is created!
            object_target.CreateFile(
               {
                  extension: 'page',
                  fileNameSuggestion: newPage.Title.length > 0 ? newPage.Title : newPage.Contents
               },
               function(new_target)
               {
                  new_target.WriteAll(
                     JSON.stringify(newPage),
                     function()
                     {
                        window.location.href = new_target.Url + '?Action=Edit';
                        page = newPage;
                     },
                     function(transport)
                     {
                        savingDialog.html(transport.responseText);
                     });
               },
               function(transport)
               {
                  savingDialog.html(transport.responseText);
               });

         return false;
      });

      window.onbeforeunload = function()
      {
         if ((page.Title != titleInput.val()) || (page.Contents != editor.get_content()))
            return "The page is changed.  Are you sure you want to exit?";
      };
   });

   $(window).load(function()
   {
      setUpEditor(page.Contents);
      $(window).resize(function()
      {
         setUpEditor(editor.get_content());
      });
   });
}
