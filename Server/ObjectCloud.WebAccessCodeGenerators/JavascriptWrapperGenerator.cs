// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.WebAccessCodeGenerators
{
    public partial class JavascriptWrapperGenerator : IWebAccessCodeGenerator
    {
        static JavascriptWrapperGenerator()
        {
            LegacyWrappersCache = new Cache<LegacyTypeAndWrapperCallsThrough, IEnumerable<string>>(GenerateWrapperForLegacyCache);
            WrappersCache = new Cache<Type, IEnumerable<string>>(GenerateWrapperForCache);
        }

        /// <summary>
        /// Cache of generated JavaScript wrappers
        /// </summary>
        private static Cache<Type, IEnumerable<string>> WrappersCache;

        public IEnumerable<string> GenerateWrapper(Type webHandlerType)
        {
            return WrappersCache[webHandlerType];
        }

        private static IEnumerable<string> GenerateWrapperForCache(Type type)
        {
            List<string> javascriptMethods = new List<string>();

            foreach (MethodAndWebCallableAttribute methodAndWCA in GetWebCallableMethods(type))
            {
                switch (methodAndWCA.WebCallableAttribute.WebCallingConvention)
                {
                    /*case WebCallingConvention.GET:
                        javascriptMethods.Add(GenerateLegacyGET_urlencoded(methodAndWCA, WrapperCallsThrough.AJAX));
                        break;

                    case WebCallingConvention.GET_application_x_www_form_urlencoded:
                        javascriptMethods.Add(GenerateLegacyGET_urlencoded(methodAndWCA, WrapperCallsThrough.AJAX));
                        break;*/

                    case WebCallingConvention.POST_application_x_www_form_urlencoded:
                        javascriptMethods.Add(GeneratePOST_urlencoded(methodAndWCA));
                        break;

                    /*case WebCallingConvention.POST_JSON:
                        javascriptMethods.Add(GenerateLegacyPOST_JSON(methodAndWCA, WrapperCallsThrough.AJAX));
                        break;

                    case WebCallingConvention.POST_string:
                        javascriptMethods.Add(GenerateLegacyPOST(methodAndWCA, WrapperCallsThrough.AJAX));
                        break;

                    case WebCallingConvention.POST_bytes:
                        javascriptMethods.Add(GenerateLegacyPOST(methodAndWCA, WrapperCallsThrough.AJAX));
                        break;

                    case WebCallingConvention.POST_stream:
                        javascriptMethods.Add(GenerateLegacyPOST(methodAndWCA, WrapperCallsThrough.AJAX));
                        break;*/

                    // For now everything else is unsupported
                }
            }

            javascriptMethods.Add("\"FullPath\": \"{0}\"");
            javascriptMethods.Add("\"Filename\": \"{1}\"");
            javascriptMethods.Add("\"Url\": \"{2}\"");
            javascriptMethods.Add("\"Permission\": \"{3}\"");

            return javascriptMethods;
        }

        /// <summary>
        /// Enumerates over all of the methods that are web callable
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IEnumerable<MethodAndWebCallableAttribute> GetWebCallableMethods(Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (MethodInfo method in methods)
            {
                foreach (WebCallableAttribute wca in method.GetCustomAttributes(typeof(WebCallableAttribute), true))
                {
                    yield return new MethodAndWebCallableAttribute(method, wca);
                    break;
                }
            }
        }

        /// <summary>
        /// Returns the parameters that the method expects from the web.  (parameters 1+)
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetArgumentsForWeb(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();

            for (int ctr = 1; ctr < parameters.Length; ctr++)
                yield return parameters[ctr].Name;
        }

        /// <summary>
        /// Groups a MethodInfo and its WebCallableAttribute
        /// </summary>
        private struct MethodAndWebCallableAttribute
        {
            public MethodAndWebCallableAttribute(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute)
            {
                MethodInfo = methodInfo;
                WebCallableAttribute = webCallableAttribute;
            }

            public MethodInfo MethodInfo;
            public WebCallableAttribute WebCallableAttribute;
        }

        /// <summary>
        /// Generates a POST wrapper for urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GeneratePOST_urlencoded(MethodAndWebCallableAttribute methodAndWCA)
        {
            string methodName = methodAndWCA.MethodInfo.Name;
            WebReturnConvention webReturnConvention = methodAndWCA.WebCallableAttribute.WebReturnConvention;

            // Create the funciton declaration
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(FunctionBegin);

            // Create a urlEncoded string for all of the parameters
            toReturn.Append(CreateEncodedParameters);

            // Create the AJAX request
            toReturn.Append(CreateAJAXRequest);

            // Open the AJAX request
            toReturn.Append("   httpRequest.open('POST', '{0}?Method=" + methodName + "', true);\n");
            toReturn.Append("   httpRequest.setRequestHeader('Content-type', 'application/x-www-form-urlencoded');\n");
            toReturn.Append("   httpRequest.send(encodedParameters);\n");
            toReturn.Append('}');

            return toReturn.ToString();
        }

        /// <summary>
        /// The beginning of each function wrapper, declares all of the default arguments and default error handlers
        /// </summary>
        private const string FunctionBegin =
@" : function(parameters, onSuccess, onFailure, urlPostfix)
{
   if (!onSuccess)
      onSuccess = function(transport)
      {
         alert(transport.responseText);
      };

   if (!onFailure)
      onFailure = function(transport)
      {
         alert(transport.responseText);
      };
";

        /// <summary>
        /// Snippit to encode all of the parameters into a string
        /// </summary>
        private const string CreateEncodedParameters =
@"   var encodedParameters = '';

   for (argName in parameters)
   {
      if (parameters.length > 0)
         encodedParameters += '&';

      var arg = parameters[argName];

      if (typeof arg == ""string"")
         encodedParameters += encodeURIComponent(argName) + '=' + encodeURIComponent(arg);
      else if (null != arg)
         encodedParameters += encodeURIComponent(argName) + '=' + encodeURIComponent(JSON.stringify(arg));
   }
";
        /// <summary>
        /// Snippit of code to create an AJAX request and callback handler
        /// </summary>
        private const string CreateAJAXRequest =
@"   var httpRequest = CreateHttpRequest();
   httpRequest.onreadystatechange = function()
   {
      if (4 == httpRequest.readyState)
         if ((httpRequest.status >= 200) && (httpRequest.status < 300))
            onSuccess(httpRequest);
         else
            onFailure(httpRequest);
   }
";
    }
}