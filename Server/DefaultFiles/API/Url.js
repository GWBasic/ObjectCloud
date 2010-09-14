/**
*
*  URL encode / decode
*  http://www.webtoolkit.info/
*
**/
 
var Url = {
 
	// public method for url encoding
	encode : function (string) {
		return escape(this._utf8_encode(string));
	},
 
	// public method for url decoding
	decode : function (string) {
		return this._utf8_decode(unescape(string));
	},
 
	// private method for UTF-8 encoding
	_utf8_encode : function (string) {
		string = string.replace(/\r\n/g,"\n");
		var utftext = "";
 
		for (var n = 0; n < string.length; n++) {
 
			var c = string.charCodeAt(n);
 
			if (c < 128) {
				utftext += String.fromCharCode(c);
			}
			else if((c > 127) && (c < 2048)) {
				utftext += String.fromCharCode((c >> 6) | 192);
				utftext += String.fromCharCode((c & 63) | 128);
			}
			else {
				utftext += String.fromCharCode((c >> 12) | 224);
				utftext += String.fromCharCode(((c >> 6) & 63) | 128);
				utftext += String.fromCharCode((c & 63) | 128);
			}
 
		}
 
		return utftext;
	},
 
	// private method for UTF-8 decoding
	_utf8_decode : function (utftext) {
		var string = "";
		var i = 0;
		var c = c1 = c2 = 0;
 
		while ( i < utftext.length ) {
 
			c = utftext.charCodeAt(i);
 
			if (c < 128) {
				string += String.fromCharCode(c);
				i++;
			}
			else if((c > 191) && (c < 224)) {
				c2 = utftext.charCodeAt(i+1);
				string += String.fromCharCode(((c & 31) << 6) | (c2 & 63));
				i += 2;
			}
			else {
				c2 = utftext.charCodeAt(i+1);
				c3 = utftext.charCodeAt(i+2);
				string += String.fromCharCode(((c & 15) << 12) | ((c2 & 63) << 6) | (c3 & 63));
				i += 3;
			}
 
		}
 
		return string;
	},

/************************
functions after this point added by Andrew Rondeau as part of ObjectCloud */

// from: http://snipplr.com/view/12208/javascript-url-parser/
// Edited by Andrew Rondeau
      /*
       * Javascript URL Parser
       * @author jrharshath (http://jrharshath.wordpress.com/)
       * @description Parses any URL like string and returns an array or URL "Components"
       *
       * Working demo located at http://jrharshath.qupis.com/urlparser/
       * While using in a webapp, use "urlparse(window.location.href)" to parse the current location of the web page
       * Free to use, just provide credits.
       */
   urlparse: function( str )
   {
      // Remove #
      str = str.split('#')[0];

      // Skip urls without ?
      if (-1 == str.indexOf('?'))
         return {};

      var result = {};
      var args = str.split('?')[1];
      args = args.split('&');

      for (var i = 0; i < args.length; i++)
      {
         var keyval = args[i].split('=');
         result[Url.decode(keyval[0])] = Url.decode(keyval[1]);
      }

      return result;
   },
 
   getArguments: function()
   {
      return this.urlparse(window.location.href);
   },

   parse: function(url)
   {
      var slashslashloc = url.indexOf("://");

      if (-1 == slashslashloc)
         throw "not a url";

      var serverAndFileAndArgs = url.substring(slashslashloc + 3);
      var firstSlash = serverAndFileAndArgs.indexOf("/");
      
      var file = serverAndFileAndArgs.substring(firstSlash);
      var argloc = file.indexOf("?");

      if (argloc >= 0)
         file = file.substring(0, argloc);

      var toReturn = 
      {
         protocol: url.substring(0, slashslashloc + 3),
         server: serverAndFileAndArgs.substring(0, firstSlash),
         file: file,
         arguments: this.urlparse(url)
      };

      return toReturn;
   },

   parseCurrent: function()
   {
      return this.parse(window.location.href);
   }
}