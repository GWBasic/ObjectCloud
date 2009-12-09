// Scripts: /API/Prototype.js

// This code is released under the LGPL
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
    * @param requestParameters Additional request parameters for the prototype.js Ajax call
    */
   Open : function(filename, parameters, onSuccess, onFailure, onException, requestParameters)
   {
      if (!requestParameters)
         requestParameters = {};

      if (!onFailure)
         onFailure = function(transport)
         {
            alert("Error opening " + filename + ": " + transport.responseText);
         };

      if (!onException)
         onException = function(requester, error)
         {
            alert(error);
         };

      requestParameters.onFailure = onFailure;
      requestParameters.onException = onException;
      if (onSuccess)
      {
         requestParameters.onSuccess = function(transport)
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
               onSuccess(js, transport);
         };
      }
      else
         requestParameters.onSuccess = function(transport)
         {
            eval('(' + transport.responseText + ')');
         };

      if (!parameters)
         parameters = {};

      parameters.Method = "GetJavascriptWrapper";

      requestParameters.method = 'get';
      requestParameters.parameters = parameters;

      new Ajax.Request(filename, requestParameters);
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