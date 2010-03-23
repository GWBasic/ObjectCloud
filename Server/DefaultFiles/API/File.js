// Scripts: /API/Prototype.js, /API/AJAX.js

// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// See /Docs/license.wchtml

/*
JavaScript wrapper for opening files
*/

var File =
{
   /**
    * Gets an object that allows for calling the given file
    *
    * @param filename The filename to manipulate
    * @param onSuccess Callback for success
    * @param onFailure Callback for failure
    * @param onException Callback for a transport exception
    */
   GetWrapper : function(filename, onSuccess, onFailure)
   {
      if (!onFailure)
         onFailure = function(transport)
         {
            alert("Error opening " + filename + ": " + transport.responseText);
         };

      if (onSuccess)
      {
         var passedOnSuccess = onSuccess;
         onSuccess = function(transport)
         {
            var js;

            try
            {
               js = eval('(' + transport.responseText + ')');
            }
            catch (error)
            {
               alert('Error evaluating response from opening ' + filename + ': ' + error);
            }

            if (js)
               passedOnSuccess(js, transport);
         };
      }
      else
         onSuccess = function(transport)
         {
            eval('(' + transport.responseText + ')');
         };

      var httpRequest = CreateHttpRequest();
      httpRequest.onreadystatechange = function()
      {
         if (4 == httpRequest.readyState)
            if ((httpRequest.status >= 200) && (httpRequest.status < 300))
               onSuccess(httpRequest);
            else
               onFailure(httpRequest);
      }
      httpRequest.open('GET', filename +  '?Method=GetJSW', true);
      httpRequest.send(null);
   },

   /**
    * Splits the passed in path into a directory and file name.  No error checking is performed
    *
    * @param path The full path, with file name
    * @return An object with Directory and Filename properties
    */
   SplitFullPath : function(path)
   {
      var indexOfLastSlash = path.lastIndexOf("/");

      var toReturn =
      {
         Directory : path.substring(0, indexOfLastSlash),
         Filename : path.substring(indexOfLastSlash + 1, path.length)
      }

      if (0 == toReturn.Directory.length)
         toReturn.Directory = "/";

      return toReturn;
   }
};