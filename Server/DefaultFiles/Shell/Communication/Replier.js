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

            // TODO:  this should be replaced with something that loads new replies through comet
            window.location.href=window.location.href
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