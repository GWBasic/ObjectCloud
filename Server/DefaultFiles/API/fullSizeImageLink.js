// Scripts: /API/jquery.js, /API/Url.js

$(document).ready(function()
{
   var images = $('img.fullSizeLink');

   images.each(function()
   {
      var me = $(this);

      var imageUrlInfo = Url.parse(this.src);

      var imageUrl = 'http://' + imageUrlInfo.server + imageUrlInfo.file;

      if (imageUrlInfo.arguments.BrowserCache)
         imageUrl += '?BrowserCache=' + imageUrlInfo.arguments.BrowserCache;

      var link = $('<a href="' + imageUrl + '" />');

      me.replaceWith(link);
      link.append(me);
   });
});