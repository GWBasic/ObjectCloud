// Scripts: /API/jquery.js


function join()
{
   groupWrapper.joinGroup(
      {},
      function()
      {
         window.location.reload(true);
      });
}

function leave()
{
   groupWrapper.leaveGroup(
      {},
      function()
      {
         window.location.reload(true);
      });
}


$(document).ready(function()
{
   $('form.updatedescription').submit(function()
   {
      groupWrapper.Set(
      {
         Name: 'Description',
         Value: $(this).children('[name=description]').val()
      },
      function()
      {
         alert("updated");
      });

      return false;
   });

   $(".leave").click(function()
   {
      leave();
      return false;
   });

   $(".join").click(function()
   {
      join();
      return false;
   });
});
