// Scripts:  /API/jquery.js

/**
 * Quick-and-dirty Context Button jQuery Plugin
 *
 * http://appfeeds.appfeeds.com/Lifestream/quickanddirtycontextbutton.page
 *
 * Copyright (c) 2010 Andrew Rondeau
 * Dual licensed under the MIT or SimPL 2.0 licenses:
 * http://www.opensource.org/licenses/mit-license.php
 * http://opensource.org/licenses/simpl-2.0.html
 *
 */

/*
   Usage

   First, create some divs that will turn into menus:

   <div class="contextMenu">
      <a>Item 1</a><br />
      <a>Item 2</a><br />
      <a>Item 3</a>
   </div>
   Then, add a script to swap in buttons:

   <script>
      $(document).ready(function()
      {
         $('.contextMenu').contextButton();
      });
   </script>
*/

jQuery.fn.contextButton = function(options) {

   if (!options)
      options = {};

   if (!options.menuClass)
      options.menuClass = "ui-widget-content";

   if (!options.buttonClass)
      options.buttonClass = "ui-button";

   if (!options.maxWidth)
      options.maxWidth = '200px';

   return this.each(function()
   {
      var me = $(this);

      var button;

      if (me.button)
      {
        if (!options.buttonOptions)
           options.buttonOptions =
           {
              icons:
              {
                 primary: "ui-icon-triangle-1-s"
              }
           };

         button = $('<span></span>').button(options.buttonOptions);
      }
      else
      {
        if (!options.buttonValue)
           options.buttonValue = '\u25bc';

         button = $('<input type="button" />');
         button.val(options.buttonValue);
      }

      button.addClass(options.buttonClass);

      var contextMenu = $('<div></div>');
      contextMenu.addClass(options.menuClass);
      contextMenu.css('position', 'absolute');
      contextMenu.css('max-width', options.maxWidth);

      me.before(button);
      contextMenu.append(me);

      button.after(contextMenu);
      contextMenu.hide();

      button.click(function()
      {
         contextMenu.show();

         var maxZ = Math.max.apply(null,$.map($('body > *'), function(e,n)
         {
            //if($(e).css('position')=='absolute')
               return parseInt($(e).css('z-index'))||1 ;
         }));

         contextMenu.css('zIndex', maxZ + 10);

         $(document).one("click", function()
         {
            contextMenu.hide();
            contextMenu.css('zIndex', 0);
         });

         return false;
      });

      return button;
   });
};


jQuery.fn.contextMenu = function(options) {
   this.contextButton();

   if (this.menu)
      this.menu();
};