// Scripts: /API/Cookies.js, /System/SessionManager?Method=GetJSW&assignToVariable=SessionManager

// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// See /Docs/license.wchtml

/*
JavaScript wrapper for manipulating the session
*/

SessionManager.GetSession = function()
{
   var sessionString = Cookies.get("SESSION");
   var sessionIdAndMaxAge = sessionString.split(",");

   var toReturn;

   if (sessionIdAndMaxAge[1])
      toReturn =
      {
         KeepAlive: true,
         MaxAge: parseFloat(sessionIdAndMaxAge[1])
     };
   else
     toReturn =
     {
         KeepAlive: false
     };

   return toReturn;
};
