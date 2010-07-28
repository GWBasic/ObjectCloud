// Scripts:  /API/jquery.js

jQuery.fn.contextButton = function(options) {

   if (!options)
      options = {};

   if (!options.class)
      options.class = "ui-widget-content";

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
      contextMenu.addClass(options.class);
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