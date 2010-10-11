// Scripts: /API/jquery.js, /API/jquery.rte.js, /API/jquery.rte.tb.js, /API/jquery.addhiddentoform.js

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
         function(linkConfirmationInformation)
         {
            var args = linkConfirmationInformation.args;
            args.redirectUrl = window.location.href;

            var form = $('<form method="POST" />');
            form.attr('action', linkConfirmationInformation.confirmLinkPage);
            form.addHiddenItems(args);

            var body = $('body');
            body.html('<div class="title">Redirecting to confirmation page</div>');
            body.append(form);

            form.submit();


            /*saveButton.unbind('click', saveFunction);
            savingSpan.hide();
            replyButton.show();
            replierDiv.empty();
            replierDiv.show();

            // TODO:  this should be replaced with something that loads new replies through comet
            $('body').html('<div class="title">Loading...</div>');
            window.location.href=window.location.href;*/
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