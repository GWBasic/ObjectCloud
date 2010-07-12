// Scripts: /API/jquery.js, /API/jquery.rte.js, /API/jquery.rte.tb.js

function Ecrit(page)
{
   $(document).ready(function()
   {
      $('input.title').val(page.Title);
      $('textarea.contents').val(page.Contents);
   });
}
