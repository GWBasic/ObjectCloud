// Scripts: /API/jquery.js, /API/jquery.rte.js, /API/jquery.rte.tb.js, /API/pretty.js

function doBlogum(object_target)
{
   var postButton = $('input.post');
   var postarea = $($('#postarea')[0]);
   var posttextarea = $($('#posttextarea')[0]);
   var saveButton = $('input.savepost');
   var savingSpan = $('.saving');

   postarea.hide();
   savingSpan.hide();

   postButton.click(function()
   {
      postButton.hide();
      postarea.show();

      var editorTextarea = $('<textarea />');
      postarea.append(editorTextarea);

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
         postarea.hide();
         saveButton.hide();

         object_target.AddPostWithContent(
         {
            title: $('input.posttitle').val(),
            contents: rte.get_content()
         },
         function()
         {
            // TODO:  this should be replaced with something that loads new replies through comet
            $('body').html('<div class="title">Loading...</div>');
            window.location.href=window.location.href
         },
         function()
         {
            saveButton.show();
            savingSpan.hide();
            postarea.show();
            alert("Could not save the post");
         });
      };

      saveButton.click(saveFunction);
   });

   var morelinks = $('.morelink');
   for (var i = 0; i < morelinks.length - 1; i++)
      $(morelinks[i]).hide();
}