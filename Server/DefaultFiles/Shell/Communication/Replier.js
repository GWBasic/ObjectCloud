// Scripts: /API/jquery.js, /API/jquery.rte.js, /API/jquery.rte.tb.js

var rpy_object = null;
var rpy_oldHTML;
var rpy_editor = null;

function rply_register(file)
{
   var saveButton = $('input.rply_save');
   saveButton.hide();

   var savingSpan = $('.rply_saving');
   savingSpan.hide();

   var replyButton = $('input.rply_reply');
   var replierDiv = $('#Replier_Replier');

   replyButton.click(function()
   {
      replyButton.hide();
      saveButton.show();

      var editorTextarea = $('<textarea />');
      replierDiv.append(editorTextarea);

      var rte = editorTextarea.rte(
      {
         controls_rte: rte_toolbar,
         controls_html: html_toolbar,
         width: '100%',
         height: 300
      })[0];

      var saveFunction;
      saveFunction = function()
      {
         savingSpan.show();
         replierDiv.hide();
         saveButton.hide();

         file.Replier_AddReply(
         {
            replyText: rte.get_content()
         },
         function()
         {
            saveButton.unbind('click', saveFunction);
            savingSpan.hide();
            replyButton.show();
            replierDiv.empty();
            replierDiv.show();
         },
         function()
         {
            saveButton.show();
            savingSpan.hide();
            replierDiv.show();
            alert("Could not save the reply");
         });
      };

      saveButton.click(saveFunction);
   });
}

function rply_addReply(inObject)
{
   rpy_object = inObject;

   var replier = $('Replier_Replier');

   rpy_oldHTML = replier.innerHTML;
   replier.innerHTML = "";
   replier.style.width="100%";

   rply_displayNicEdit();
}

function rply_displayNicEdit()
{
   rpy_editor = new nicEditor(
   {
      fullPanel: true,
      onSave : rpy_save,
      style: "contents"
   });

   rpy_editor.panelInstance(
      'Replier_Replier',
      {
         hasPanel: true,
         style: "contents"
      });
}

function rpy_save(content, id, instance)
{
   var replier = $('Replier_Replier');

   if (null != rpy_editor)
      rpy_editor.removeInstance('Replier_Replier');

   replier.innerHTML = 'Saving...<div>' + content + '</div>';

   rpy_object.Replier_AddReply(
      {
         replyText: content
      },
      function()
      {
         replier.innerHTML = rpy_oldHTML;

         var repliesDiv = $('Replier_Replies');
         repliesDiv.innerHTML = "Loading...";

         rpy_object.Replier_GetRepliesForDisplay(
            {},
            function(repliesHtml)
            {
               repliesDiv.innerHTML = repliesHtml;
            });
      },
      function()
      {
         alert("Could not save the reply");
         replier.innerHTML = content;
         rply_displayNicEdit();
      });
}
