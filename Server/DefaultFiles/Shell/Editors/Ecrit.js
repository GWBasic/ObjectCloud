// Scripts: /API/jquery.js, /API/jquery.rte.js, /API/jquery.rte.tb.js

function Ecrit(page)
{
   $(document).ready(function()
   {
      $('input.title').val(page.Title);

      var editorTop = $('#editorTop');
      var contents = $('textarea.contents');
      contents.val(page.Contents);

      var windowHeight = $(window).height();
      var editorOffset = editorTop.position();
      var rteTop = editorTop.height() + editorOffset.top;

      var rte = contents.rte(
      {
         controls_rte: rte_toolbar,
         controls_html: html_toolbar,
         width: editorTop.width(),
         height: windowHeight - rteTop - 75
      });

      // hide the resizer because this will resize with the window
      $('.rte-resizer').hide();

      var rte = $('.rte-zone');
      rte.css( { position: 'absolute' });

      /*function resize()
      {
         // Hide the text editor while its new size and position are calculated
         rte.hide();


//editorTop.html(rteTop);

         rte.offset(
         {
            left: editorOffset.left,
            top: rteTop
         });

         rte.size(
         {
            width: ,
            height: 
         });

         rte.show();

      }

      $(window).resize(resize);

      resize();*/
   });
}
