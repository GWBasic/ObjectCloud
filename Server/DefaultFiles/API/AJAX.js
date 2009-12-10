// based on code from http://www.w3schools.com/Ajax/ajax_browsers.asp

function CreateHttpRequest()
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