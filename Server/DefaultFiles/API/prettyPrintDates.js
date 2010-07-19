// Scripts: /API/jquery.js, /API/pretty.js

$(document).ready(function() {
   $('.date_ago').each(function(i, element)
   {
      var date = new Date(parseFloat(element.innerHTML));
      element.innerHTML = prettyDate(date);
   });

   $('.date').each(function(i, element)
   {
      var date = new Date(parseFloat(element.innerHTML));
      element.innerHTML = prettyDate(date) + ' (' + date + ')';
   });
});