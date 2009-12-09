// Scripts: /API/Prototype.js, /API/Cookies.js, /System/SessionManager?Method=GetJavascriptWrapper&assignToVariable=SessionManager

// This code is released under the LGPL
// See /Docs/license.wchtml

/*
JavaScript wrapper for manipulating the session
*/

SessionManager.GetSession = function()
{
   var sessionString = Cookies.get("SESSION");
   var sessionIdAndMaxAge = sessionString.split(",");

   var keepAliveInput = $("Login_KeepAlive");
   var maxAgeInput = $("Login_MaxAge");

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
