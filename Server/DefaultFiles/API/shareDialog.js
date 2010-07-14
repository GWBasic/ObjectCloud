// Scripts: /API/jquery.js, /API/jquery-ui.js, /API/Url.js

// jquery ui way of setting permissions on an object


function shareDialog_show(filename)
{
   var html = '<div><iframe style="width: 100%; height: 100%" src="/Shell/Security/PermissionsComponent.oc?FileName=' + Url.encode(filename) + '"></iframe></div>';

   var dialog = $(html).dialog(
      {
         modal:true,
         position:'top',
         height: 500,
         width: 800,
         title: 'Share: ' + filename/*,
         buttons:
         {
            "Ok": function()
            {
               $(this).dialog("close");
            }
         }*/
      });
}