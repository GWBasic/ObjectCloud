// Emulates AJAX requests for server-side javascript

// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// See /Docs/license.wchtml

if (typeof CreateHttpRequest != 'function')
   CreateHttpRequest = function()
   {
      var toReturn =
      {
         headers: {},

         open: function(webMethod, url, asyncronous)
         {
            this.webMethod = webMethod;
            this.url = url;
            this.asyncronous = asyncronous;
         },

         setRequestHeader: function(name, value)
         {
            // header is currently ignored
            this.headers[name] = value;
         },

         send: function(payload)
         {
            results = Shell(
               this.webMethod,
               this.url,
               this.headers['Content-type'],
               payload);

            this.readyState = 4;
            this.status = results.Status;
            this.responseText = results.Content;

            if (this.asyncronous)
               this.onreadystatechange();
         }
      };

      return toReturn;
   };
