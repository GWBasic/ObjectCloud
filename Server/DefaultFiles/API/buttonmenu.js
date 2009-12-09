// Scripts: /API/proto.menu.js

Proto.CreateMenu = function(menuItems, insertAfter)
{
   var menu = new Proto.Menu(
   {
      className: 'menu desktop',
      menuItems: menuItems
   });

   var button = new Element(
      'input',
      {
         type: 'button',
         value: '\u25bc' //'&#x25bc'
      });

   button.observe("click", function(e)
   {
      menu.show(e);
   });

   button.menu = menu;

   if (null != insertAfter)
   {
      insertAfter.up().insertBefore(button, insertAfter);
      insertAfter.up().insertBefore(insertAfter, button);
   }

   return button;
}