// Scripts: /API/jquery.js, /API/jquery.form.js

$(document).ready(function()
{
   $('form.AddUser').ajaxForm(function()
   {
      window.location.href = window.location.href;
   });

   $('form.RemoveUser').ajaxForm(function()
   {
      window.location.href = window.location.href;
   });
});

