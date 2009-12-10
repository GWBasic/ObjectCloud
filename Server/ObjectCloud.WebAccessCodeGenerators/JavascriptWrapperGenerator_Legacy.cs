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
    public partial class JavascriptWrapperGenerator
    {
        public IEnumerable<string> GenerateLegacyWrapper(Type webHandlerType, WrapperCallsThrough wrapperCallsThrough)
        {
            LegacyTypeAndWrapperCallsThrough typeAndWrapperCallsThrough = new LegacyTypeAndWrapperCallsThrough();
            typeAndWrapperCallsThrough.Type = webHandlerType;
            typeAndWrapperCallsThrough.WrapperCallsThrough = wrapperCallsThrough;

            return LegacyWrappersCache[typeAndWrapperCallsThrough];
        }

        private struct LegacyTypeAndWrapperCallsThrough
        {
            public Type Type;
            public WrapperCallsThrough WrapperCallsThrough;

            public override bool Equals(object obj)
            {
                if (obj is LegacyTypeAndWrapperCallsThrough)
                {
                    LegacyTypeAndWrapperCallsThrough other = (LegacyTypeAndWrapperCallsThrough)obj;
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

        private static Cache<LegacyTypeAndWrapperCallsThrough, IEnumerable<string>> LegacyWrappersCache;

        private static IEnumerable<string> GenerateWrapperForLegacyCache(LegacyTypeAndWrapperCallsThrough typeAndWrapperCallsThrough)
        {
            WrapperCallsThrough wrapperCallsThrough = typeAndWrapperCallsThrough.WrapperCallsThrough;

            List<string> javascriptMethods = new List<string>();

            foreach (MethodAndWebCallableAttribute methodAndWCA in GetWebCallableMethods(typeAndWrapperCallsThrough.Type))
            {
                switch (methodAndWCA.WebCallableAttribute.WebCallingConvention)
                {
                    case WebCallingConvention.GET:
                        javascriptMethods.Add(GenerateLegacyGET_urlencoded(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.GET_application_x_www_form_urlencoded:
                        javascriptMethods.Add(GenerateLegacyGET_urlencoded(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_application_x_www_form_urlencoded:
                        javascriptMethods.Add(GenerateLegacyPOST_urlencoded(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_JSON:
                        javascriptMethods.Add(GenerateLegacyPOST_JSON(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_string:
                        javascriptMethods.Add(GenerateLegacyPOST(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_bytes:
                        javascriptMethods.Add(GenerateLegacyPOST(methodAndWCA, wrapperCallsThrough));
                        break;

                    case WebCallingConvention.POST_stream:
                        javascriptMethods.Add(GenerateLegacyPOST(methodAndWCA, wrapperCallsThrough));
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
        /// Default string that's used at the beginning of almost all methods.  Allows default handlers and declarations that are common to all wrapped AJAX methods
        /// </summary>
        public static string GenerateLegacyWrapperMethodDeclaration(WrapperCallsThrough wrapperCallsThrough)
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
        public const string LegacyInitParameters =
@"   if (!parameters)
      parameters = {};

";

        /// <summary>
        /// Default string that's used to put in the AJAX call and end the method
        /// </summary>
        public const string LegacyWrapperMethodAjaxCall =
@"
   new Ajax.Request(" + "\"{0}\" + (urlPostfix != null ? urlPostfix : \"\")," + @" requestParameters);
}";

        /// <summary>
        /// Generates a GET wrapper
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GenerateLegacyGET_urlencoded(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GenerateLegacyGET_urlencoded(
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
        public static string GenerateLegacyGET_urlencoded(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");
            toReturn.Append(StringGenerator.GenerateCommaSeperatedList(parameterNames));

            if (parameterNames.Count > 0)
                toReturn.Append(", ");

            toReturn.Append("parameters");

            toReturn.Append(GenerateLegacyWrapperMethodDeclaration(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
                toReturn.Append(GenerateLegacyOnSuccessHandler(webReturnConvention));

            toReturn.Append(LegacyInitParameters);

            foreach (string parameterName in parameterNames)
                toReturn.AppendFormat(
@"   if (typeof {0} == ""string"")
        parameters.{0} = {0};
     else if (null != {0})
        parameters.{0} = {1}({0});
", parameterName, LegacyGetStringifyFunction(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
            {
                toReturn.AppendFormat("   parameters.Method = \"{0}\";\n", methodName);

                toReturn.Append(
    @"
   requestParameters.method = 'get';
   requestParameters.parameters = parameters;
");

                toReturn.Append(LegacyWrapperMethodAjaxCall);
            }
            else
            {
                toReturn.Append(LegacyGenerateBypassJavascriptVariable(wrapperCallsThrough));
                toReturn.Append(
@"
   var toReturn = Shell_GET(""{0}"", """ + methodName + @""", parameters, bypassJavascript);
   ");
                toReturn.Append(GenerateLegacyResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates a POST wrapper for urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GenerateLegacyPOST_urlencoded(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GenerateLegacyPOST_urlencoded(
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
        public static string GenerateLegacyPOST_urlencoded(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");
            toReturn.Append(StringGenerator.GenerateCommaSeperatedList(parameterNames));

            if (parameterNames.Count > 0)
                toReturn.Append(", ");

            toReturn.Append("parameters");
            toReturn.Append(GenerateLegacyWrapperMethodDeclaration(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
                toReturn.Append(GenerateLegacyOnSuccessHandler(webReturnConvention));

            toReturn.Append(LegacyInitParameters);

            foreach (string parameterName in parameterNames)
                toReturn.AppendFormat(
@"   if (typeof {0} == ""string"")
        parameters.{0} = {0};
     else if (null != {0})
        parameters.{0} = {1}({0});
", parameterName, LegacyGetStringifyFunction(wrapperCallsThrough));

            if ((WrapperCallsThrough.AJAX & wrapperCallsThrough) > 0)
            {
                toReturn.Append(
    @"
   requestParameters.method = 'post';
   requestParameters.parameters = parameters;
");

                string wrapperMethodAjaxCallAndMethod = LegacyWrapperMethodAjaxCall.Replace("{0}", "{0}?Method=" + methodName);
                toReturn.Append(wrapperMethodAjaxCallAndMethod);
            }
            else
            {
                toReturn.Append(LegacyGenerateBypassJavascriptVariable(wrapperCallsThrough));
                toReturn.Append(
@"
   var toReturn = Shell_POST_urlencoded(""{0}"", """ + methodName + @""", parameters, bypassJavascript);
   ");
                toReturn.Append(GenerateLegacyResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates a POST wrapper for non-urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GenerateLegacyPOST(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GenerateLegacyPOST(
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
        public static string GenerateLegacyPOST(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");

            string wrapperMethodDeclaration = JavascriptWrapperGenerator.GenerateLegacyWrapperMethodDeclaration(wrapperCallsThrough);

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
                toReturn.Append(GenerateLegacyOnSuccessHandler(webReturnConvention));


                toReturn.Append(@"
   requestParameters.method = 'post';"); ;

                if (parameterNames.Count > 0)
                    toReturn.Append(@"
   requestParameters.postBody = " + parameterNames[0] + @";
");

                string wrapperMethodAjaxCallAndMethod = LegacyWrapperMethodAjaxCall.Replace("{0}", "{0}?Method=" + methodName);
                toReturn.Append(wrapperMethodAjaxCallAndMethod);
            }
            else
            {
                toReturn.Append(LegacyGenerateBypassJavascriptVariable(wrapperCallsThrough));
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

                toReturn.Append(GenerateLegacyResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates a POST wrapper for non-urlencoded queries
        /// </summary>
        /// <param name="methodAndWCA"></param>
        /// <returns></returns>
        private static string GenerateLegacyPOST_JSON(MethodAndWebCallableAttribute methodAndWCA, WrapperCallsThrough wrapperCallsThrough)
        {
            return GenerateLegacyPOST_JSON(
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
        public static string GenerateLegacyPOST_JSON(string methodName, IList<string> parameterNames, WebReturnConvention webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
        {
            StringBuilder toReturn = new StringBuilder(string.Format("\"{0}\"", methodName));
            toReturn.Append(" : function(");

            string wrapperMethodDeclaration = JavascriptWrapperGenerator.GenerateLegacyWrapperMethodDeclaration(wrapperCallsThrough);

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
                toReturn.Append(GenerateLegacyOnSuccessHandler(webReturnConvention));


                toReturn.Append(@"
   requestParameters.method = 'post';"); ;

                if (parameterNames.Count > 0)
                    toReturn.Append(@"
   requestParameters.postBody = " + LegacyGetStringifyFunction(wrapperCallsThrough) + "(" + parameterNames[0] + @");
");

                string wrapperMethodAjaxCallAndMethod = LegacyWrapperMethodAjaxCall.Replace("{0}", "{0}?Method=" + methodName);
                toReturn.Append(wrapperMethodAjaxCallAndMethod);
            }
            else
            {
                toReturn.Append(LegacyGenerateBypassJavascriptVariable(wrapperCallsThrough));
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

                toReturn.Append(GenerateLegacyResultsHandler(webReturnConvention, wrapperCallsThrough));
            }

            return toReturn.ToString();
        }

        /// <summary>
        /// Generates the success handler
        /// </summary>
        /// <returns></returns>
        public static string GenerateLegacyOnSuccessHandler(WebReturnConvention? webReturnConvention)
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
        public static string GenerateLegacyResultsHandler(WebReturnConvention? webReturnConvention, WrapperCallsThrough wrapperCallsThrough)
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

        /// <summary>
        /// Returns the correct stringify function
        /// </summary>
        /// <param name="wrapperCallsThrough"></param>
        /// <returns></returns>
        public static string LegacyGetStringifyFunction(WrapperCallsThrough wrapperCallsThrough)
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
        private static string LegacyGenerateBypassJavascriptVariable(WrapperCallsThrough wrapperCallsThrough)
        {
            if ((WrapperCallsThrough.BypassServerSideJavascript & wrapperCallsThrough) > 0)
                return "   var bypassJavascript = true;\n";
            else
                return "   var bypassJavascript = false;\n";
        }
    }
}
