// Scripts:  /API/jquery-ui.js

/**
 * jQuery-ui.auto:  Automatically applies jquery ui look-and-feel and behaviors to elements with known classes
 *
 *
 * Copyright (c) 2010 Andrew Rondeau
 * Dual licensed under the MIT or SimPL 2.0 licenses:
 * http://www.opensource.org/licenses/mit-license.php
 * http://opensource.org/licenses/simpl-2.0.html
 *
 */

/*
   Usage

   Merely give class names to items that should have special behaviors:

   example:

   <a class="juiauto_button" href="...">...  :  Automatically switched to a button
   <a class="juiauto_modal" href="...">...  : Displayed in an iFrame inside of a modal dialog box

   Note:  This script requires jQuery-ui, a wonderful jQuery-based UI framework
*/

$(document).ready(function()
{
   // from http://james.padolsey.com/javascript/get-document-height-cross-browser/
   function getDocHeight() {
       var D = document;
       return Math.max(
           Math.max(D.body.scrollHeight, D.documentElement.scrollHeight),
           Math.max(D.body.offsetHeight, D.documentElement.offsetHeight),
           Math.max(D.body.clientHeight, D.documentElement.clientHeight)
       );
   }

   // Links that should look like buttons
   $('.juiauto_button').button();

   // Links that are displayed in a modal dialog box with an iFrame
   $('.juiauto_modal').click(function()
   {
      // Make sure there's a link to display
      if (!this.href)
         return;

      var iframe = $('<iframe style="width: 100%; height: 100%" src="' + this.href + '"></iframe>');
      //var loading = $('<span>Loading...</span>');
      var div = $('<div></div>');
      //div.append(loading);
      div.append(iframe);

      var jWindow = $(window);

      // This doesn't work in Chrome
      /*iframe.load(function()
      {
         try
         {
            var height = this.contentDocument.height + 40;
            var windowHeight = jWindow.height();

            if (height > windowHeight * 0.75)
               this.style.height = (jWindow.height() * 0.75) + 'px';
            else
               //this.style.height = height + 'px';
               $(this).height(height);
         }
         catch (exception)
         {
            // In case of weirdo error, like IE or cross-domain issues
            this.style.height = () + 'px';
         }
      });*/

      div.dialog(
      {
         modal:true,
         position:'top',
         height: getDocHeight() * 0.6,
         width: jWindow.width() * 0.6,
         title: '<a href="' + this.href + '">' + $(this).html() + '</a>'
      });

      return false;
   });
});