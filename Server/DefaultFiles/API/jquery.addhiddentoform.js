// Scripts:  /API/jquery.js

/**
 * Add Hidden Items from object to Form jQuery Plugin
 *
 * Converts items in an object to hidden inputs in a form
 *
 * Copyright (c) 2010 Andrew Rondeau
 * Dual licensed under the MIT or SimPL 2.0 licenses:
 * http://www.opensource.org/licenses/mit-license.php
 * http://opensource.org/licenses/simpl-2.0.html
 *
 */

/*

   Usage:  First, create or load a form:

   var myForm = $('<form method="POST" action="/targeturl" /');

   - or -

   var myForm = $('form.toAugment');

   Then, call addHiddenItems

   myForm.addHiddenItems(
   {
      a: 'first',
      b: 'second',
      c: 'ect...'
   });

*/

jQuery.fn.addHiddenItems = function(items)
{
   return this.each(function()
   {
      var me = $(this);

      for (argname in items)
      {
         var arg = $('<input type="hidden" />');
         arg.attr('name', argname);

         var val = items[argname];
         var valType = typeof val;

         if ((valType == 'string') || (valType == 'number') || (valType == 'boolean'))
            arg.val(val);
         else
            arg.val(JSON.stringify(val));

         me.append(arg);
      }
   });
};