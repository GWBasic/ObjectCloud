// based on code from http://www.w3schools.com/Ajax/ajax_browsers.asp

// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// See /Docs/license.wchtml

if (typeof CreateHttpRequest != 'function')
   CreateHttpRequest = function()
   {
      if (window.XMLHttpRequest)
         // code for IE7+, Firefox, Chrome, Opera, Safari
         return new XMLHttpRequest();

      else if (window.ActiveXObject)
        // code for IE6, IE5
        return new ActiveXObject("Microsoft.XMLHTTP");

      else
         throw "Your browser does not support AJAX!";
   }

function GET(url, onSuccess, onFailure)
{
   var httpRequest = CreateHttpRequest();
   httpRequest.onreadystatechange = function()
   {
      if (4 == httpRequest.readyState)
         if ((httpRequest.status >= 200) && (httpRequest.status < 300))
            onSuccess(httpRequest.responseText, httpRequest);
         else
            onFailure(httpRequest);
   }

   httpRequest.open('GET', url, true);
   httpRequest.send(null);
}