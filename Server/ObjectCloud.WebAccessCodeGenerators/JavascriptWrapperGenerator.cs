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
    public class JavascriptWrapperGenerator : IWebAccessCodeGenerator
    {
        static JavascriptWrapperGenerator()
        {
            WrappersCache = new Cache<TypeAndWrapperCallsThrough, IEnumerable<string>>(GenerateWrapperForCache);
        }

        private struct TypeAndWrapperCallsThrough
        {
            public Type Type;
            public WrapperCallsThrough WrapperCallsThrough;

            public override bool Equals(object obj)
            {
                if (obj is TypeAndWrapperCallsThrough)
                {
                    TypeAndWrapperCallsThrough other = (TypeAndWrapperCallsThrough)obj;
                    return Type.Equals(other.Type) && WrapperCallsThrough.Equals(other.WrapperCallsThrough);
                }

                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                uint typeHashCode = BitConverter.ToUInt32(BitConverter.GetBytes(Type.GetHashCode()), 0);

                uint toReturn = (Convert.ToUInt32(WrapperCallsThrough) >> 3) + typeHashCode;

                return BitConverter.ToInt32(BitConverter.GetBytes(toReturn), 0);
            }
        }

        public IEnumerable<string> GenerateWrapper(Type webHandlerType, WrapperCallsThrough wrapperCallsThrough)
        {
            TypeAndWrapperCallsThrough typeAndWrapperCallsThrough = new TypeAndWrapperCallsThrough();
            typeAndWrapperCallsThrough.Type = webHandlerType;
            typeAndWrapperCallsThrough.WrapperCallsThrough = wrapperCallsThrough;

            return WrappersCache[typeAndWrapperCallsThrough];
        }

        private static Cache<TypeAndWrapperCallsThrough, IEnumerable<string>> WrappersCache;

        private static IEnumerable<string> GenerateWrapperForCache(TypeAndWrapperCallsThrough typeAndWrapperCallsThrough)
        {
            WrapperCallsThrough wrapperCallsThrough = typeAndWrapperCallsThrough.WrapperCallsThrough;
        
            List<string> javascriptMethods = new List<string>();

            foreach (MethodAndWebCallableAttribute methodAndWCA in GetWebCallableMethods(typeAndWrapperCallsThrough.Type))
            {
                switch (methodAndWCA.WebCallableAttribute.WebCallingConvention)
                {
                    case WebCallingConvention.GET:
                        javascriptMethods.Add(GenerateGET_urlencoded(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.GET_application_x_www_form_urlencoded:
                        javascriptMethods.Add(GenerateGET_urlencoded(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_application_x_www_form_urlencoded:
                        javascriptMethods.Add(GeneratePOST_urlencoded(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_JSON:
                        javascriptMethods.Add(GeneratePOST_JSON(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_string:
                        javascriptMethods.Add(GeneratePOST(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_bytes:
                        javascriptMethods.Add(GeneratePOST(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_stream:
                        javascriptMethods.Add(GeneratePOST(methodAndWCA, wrapperCallsThrough));
                        break;

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
        /// Default string that's used at the beginning of almost all methods.  Allows default handlers and declarations that are common to all wrapped AJAX methods
        /// </summary>
        public static string GenerateWrapperMethodDeclaration(WrapperCallsThrough wrapperCallsThrough)
        {
            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
                return
@", onSuccess, onFailure, onException, requestParameters, urlPostfix)
{
   if (!requestParameters)
      requestParameters = {};

   if (!onFailure)
      onFailure = function(transport)
      {
         alert(transport.responseText);
      };

   if (!onException)
      onException = function(requester, error)
      {
         alert('Transport error for ' + requester.url + ': ' + error);
      };

   requestParameters.onFailure = onFailure;
   requestParameters.onException = onException;";
            else
                return ")\n{\n";
        }

        /// <summary>
        /// Used to initialize the parameters array if it's not passed in
        /// </summary>
        public const string InitParameters =
@"   if (!parameters)
      parameters = {};

";

        /// <summary>
        /// Default string that's used to put in the AJAX call and end the method
        /// </summary>
        public const string WrapperMethodAjaxCall =
@"
   new Ajax.Request(" + "\"{0}\" + (urlPostfix != null ? urlPostfix : \"\")," + @" requestParameters);
}";

        /// <summary>
        /// Returns the correct stringify function
        /// </summary>
        /// <param name="wrapperCallsThrough"></param>
        /// <returns></returns>
        public static string GetStringifyFunction(WrapperCallsThrough wrapperCallsThrough)
        {
            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
                return "Object.toJSON";
            else
                return "JSON.stringify";
        }

        /// <summary>
        /// Generates the BypassJavascript variable
        /// </summary>
        /// <param name="wrapperCallsThrough"></param>
        /// <returns></returns>
        private static string GenerateBypassJavascriptVariable(WrapperCallsThrough wrapperCallsThrough)
        {
            if ((WrapperCallsThrough.BypassServerSideJavascript & wrapperCallsThrough) > 0)
                return "   var bypassJavascript = true;\n";
            else
                return "   var bypassJavascript = false;\n";
        }

        /// <summary>
        /// Generates a GET wrapper
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GenerateGET_urlencoded(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GenerateGET_urlencoded(
                methodAndWCA.MethodInfo.Name,
                new List<string>(GetArgumentsForWeb(methodAndWCA.MethodInfo)),
                methodAndWCA.WebCallableAttribute.WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a GET wrapper
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        public static string GenerateGET_urlencoded(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");
            toReturn.Append(StringGenerator.GenerateCommaSeperatedList(parameterNames));

            if (parameterNames.Count > 0)
                toReturn.Append(", ");

            toReturn.Append("parameters");

            toReturn.Append(GenerateWrapperMethodDeclaration(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
                toReturn.Append(GenerateOnSuccessHandler(webReturnConvention));
            
            toReturn.Append(InitParameters);

            foreach (string parameterName in parameterNames)
                toReturn.AppendFormat(
@"   if (typeof {0} == ""string"")
        parameters.{0} = {0};
     else if (null != {0})
        parameters.{0} = {1}({0});
", parameterName, GetStringifyFunction(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
            {
                toReturn.AppendFormat("   parameters.Method = \"{0}\";\n", methodName);

                toReturn.Append(
    @"
   requestParameters.method = 'get';
   requestParameters.parameters = parameters;
");

                toReturn.Append(WrapperMethodAjaxCall);
            }
            else
            {
                toReturn.Append(GenerateBypassJavascriptVariable(wrapperCallsThrough));
                toReturn.Append(
@"
   var toReturn = Shell_GET(""{0}"", """ + methodName + @""", parameters, bypassJavascript);
   ");
                toReturn.Append(GenerateResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates a POST wrapper for urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GeneratePOST_urlencoded(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GeneratePOST_urlencoded(
                methodAndWCA.MethodInfo.Name,
                new List<string>(GetArgumentsForWeb(methodAndWCA.MethodInfo)),
                methodAndWCA.WebCallableAttribute.WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a POST wrapper for urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        public static string GeneratePOST_urlencoded(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");
            toReturn.Append(StringGenerator.GenerateCommaSeperatedList(parameterNames));

            if (parameterNames.Count > 0)
                toReturn.Append(", ");

            toReturn.Append("parameters");
            toReturn.Append(GenerateWrapperMethodDeclaration(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
                toReturn.Append(GenerateOnSuccessHandler(webReturnConvention));

            toReturn.Append(InitParameters);

            foreach (string parameterName in parameterNames)
                toReturn.AppendFormat(
@"   if (typeof {0} == ""string"")
        parameters.{0} = {0};
     else if (null != {0})
        parameters.{0} = {1}({0});
", parameterName, GetStringifyFunction(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
            {
                toReturn.Append(
    @"
   requestParameters.method = 'post';
   requestParameters.parameters = parameters;
");

                string wrapperMethodAjaxCallAndMethod = WrapperMethodAjaxCall.Replace("{0}", "{0}?Method=" + methodName);
                toReturn.Append(wrapperMethodAjaxCallAndMethod);
            }
            else
            {
                toReturn.Append(GenerateBypassJavascriptVariable(wrapperCallsThrough));
                toReturn.Append(
@"
   var toReturn = Shell_POST_urlencoded(""{0}"", """ + methodName + @""", parameters, bypassJavascript);
   ");
                toReturn.Append(GenerateResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates a POST wrapper for non-urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GeneratePOST(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GeneratePOST(
                methodAndWCA.MethodInfo.Name,
                new List<string>(GetArgumentsForWeb(methodAndWCA.MethodInfo)),
                methodAndWCA.WebCallableAttribute.WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a POST wrapper for non-urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        public static string GeneratePOST(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");

            string wrapperMethodDeclaration = JavascriptWrapperGenerator.GenerateWrapperMethodDeclaration(wrapperCallsThrough);

            if (parameterNames.Count > 0)
            {
                toReturn.Append(parameterNames[0]);
                toReturn.Append(wrapperMethodDeclaration);
            }
            else
                if (wrapperMethodDeclaration.StartsWith(","))
                    toReturn.Append(wrapperMethodDeclaration.Substring(2));
                else
                    toReturn.Append(wrapperMethodDeclaration);

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
            {
                toReturn.Append(GenerateOnSuccessHandler(webReturnConvention));


                toReturn.Append(@"
   requestParameters.method = 'post';"); ;

                if (parameterNames.Count > 0)
                    toReturn.Append(@"
   requestParameters.postBody = " + parameterNames[0] + @";
");

                string wrapperMethodAjaxCallAndMethod = WrapperMethodAjaxCall.Replace("{0}", "{0}?Method=" + methodName);
                toReturn.Append(wrapperMethodAjaxCallAndMethod);
            }
            else
            {
                toReturn.Append(GenerateBypassJavascriptVariable(wrapperCallsThrough));
                if (parameterNames.Count > 0)
                    toReturn.Append(
@"
   var toReturn = Shell_POST(""{0}"", """ + methodName + @""", " + parameterNames[0] + @", bypassJavascript);
   ");
                else
                    toReturn.Append(
@"
   var toReturn = Shell_POST(""{0}"", """ + methodName + @""", null, bypassJavascript);
   ");

                toReturn.Append(GenerateResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates a POST wrapper for non-urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GeneratePOST_JSON(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GeneratePOST_JSON(
                methodAndWCA.MethodInfo.Name,
                new List<string>(GetArgumentsForWeb(methodAndWCA.MethodInfo)),
                methodAndWCA.WebCallableAttribute.WebReturnConvention,
                wrapperCallsThrough);
        }

        /// <summary>
        /// Generates a POST wrapper for non-urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        public static string GeneratePOST_JSON(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");

            string wrapperMethodDeclaration = JavascriptWrapperGenerator.GenerateWrapperMethodDeclaration(wrapperCallsThrough);

            if (parameterNames.Count > 0)
            {
                toReturn.Append(parameterNames[0]);
                toReturn.Append(wrapperMethodDeclaration);
            }
            else
                if (wrapperMethodDeclaration.StartsWith(","))
                    toReturn.Append(wrapperMethodDeclaration.Substring(2));
                else
                    toReturn.Append(wrapperMethodDeclaration);

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
            {
                toReturn.Append(GenerateOnSuccessHandler(webReturnConvention));


                toReturn.Append(@"
   requestParameters.method = 'post';"); ;

                if (parameterNames.Count > 0)
                    toReturn.Append(@"
   requestParameters.postBody = " + GetStringifyFunction(wrapperCallsThrough) + "(" + parameterNames[0] + @");
");

                string wrapperMethodAjaxCallAndMethod = WrapperMethodAjaxCall.Replace("{0}", "{0}?Method=" + methodName);
                toReturn.Append(wrapperMethodAjaxCallAndMethod);
            }
            else
            {
                toReturn.Append(GenerateBypassJavascriptVariable(wrapperCallsThrough));
                if (parameterNames.Count > 0)
                    toReturn.Append(
@"
   var toReturn = Shell_POST(""{0}"", """ + methodName + @""", " + parameterNames[0] + @", bypassJavascript);
   ");
                else
                    toReturn.Append(
@"
   var toReturn = Shell_POST(""{0}"", """ + methodName + @""", null, bypassJavascript);
   ");

                toReturn.Append(GenerateResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates the success handler
        /// </summary>
        /// <returns></returns>
        public static string GenerateOnSuccessHandler(WebReturnConvention? webReturnConvention)
        {
            switch (webReturnConvention)
            {
                case WebReturnConvention.JSON:
                    return @"
   if (onSuccess)
      requestParameters.onSuccess = function(transport)
      {
         onSuccess(transport.responseText.evalJSON(true), transport);
      };
   else
      requestParameters.onSuccess = function(transport) {};

";
                case WebReturnConvention.Primitive:
                    return @"
   if (onSuccess)
      requestParameters.onSuccess = function(transport)
      {
         onSuccess(transport.responseText, transport);
      };
   else
      requestParameters.onSuccess = function(transport)
      {
         alert(transport.responseText);
      };

";
                case WebReturnConvention.JavaScriptObject:
                    return @"
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
            alert('Error evaluating response: ' + error);
         }

         onSuccess(js, transport);
      };
   }
   else
      requestParameters.onSuccess = function(transport)
      {
         eval('(' + transport.responseText + ')');
      };

";
                default:
                    return @"
   if (onSuccess)
      requestParameters.onSuccess = onSuccess;
   else
      requestParameters.onSuccess = function(transport)
      {
         alert(transport.responseText);
      };

";
            }
        }

        /// <summary>
        /// The WebReturnConvention of the wrapped method
        /// </summary>
        /// <param name="webReturnConvention"></param>
        /// <returns></returns>
        public static string GenerateResultsHandler(WebReturnConvention? webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            string jsonParseFunction;

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
                jsonParseFunction = "Object.parseJSON";
            else
                jsonParseFunction = "JSON.parse";

            switch (webReturnConvention)
            {
                case WebReturnConvention.JSON:
                    return "return " + jsonParseFunction + "(toReturn.Content);\n}";

                case WebReturnConvention.JavaScriptObject:
                    return "return eval('(' + toReturn.Content + ')');\n}";

                default:
                    return "return toReturn.Content;\n}";
            }
        }
    }
}