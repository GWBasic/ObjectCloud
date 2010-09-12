// Scripts: /API/jquery.js, /API/jquery.rte.js, /API/jquery.rte.tb.js

function Ecrit(page)
{
   $(document).ready(function()
   {
      var titleInput = $('input.title')
      titleInput.val(page.Title);

      var contents = $('#contents');
      contents.val(page.Contents);

      var windowHeight = window.innerHeight; //$(window).height();
      //var editorOffset = editorTop.position();
      //var rteTop = editorTop.height() + editorOffset.top;
alert(windowHeight + '   ' + contents.position().top);

      var rte = contents.rte(
      {
         controls_rte: rte_toolbar,
         controls_html: html_toolbar,
         width: 800,
         height: windowHeight - contents.position().top
      });

      $('#contentsBack').height(windowHeight - contents.position().top);

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
