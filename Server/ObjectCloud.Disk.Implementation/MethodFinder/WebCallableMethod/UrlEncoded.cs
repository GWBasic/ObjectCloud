// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.Implementation.MethodFinder
{
    public abstract class UrlEncoded : WebCallableMethod
    {
        public UrlEncoded(MethodInfo methodInfo, WebCallableAttribute webCallableAttribute, WebMethod? webMethod)
            : base(methodInfo, webCallableAttribute, webMethod) { }

        protected IWebResults CallMethod(IWebConnection webConnection, IWebHandlerPlugin webHandlerPlugin, IDictionary<string, string> parameters)
        {
            object[] arguments = new object[NumParameters];

            // Decode the arguments
            foreach (KeyValuePair<string, string> parameter in parameters)
                if (ParameterIndexes.ContainsKey(parameter.Key))
                {
                    string value = parameter.Value;
                    uint parameterIndex = ParameterIndexes[parameter.Key];

                    if (null != value)
                    {
                        ParameterInfo parameterInfo = Parameters[parameterIndex];
                        Type parameterType = parameterInfo.ParameterType;

                        // Attempt to convert the value to the requested parameter type

                        // JSON types
                        if ((typeof(Dictionary<string, string>) == parameterType)
                            || (typeof(Dictionary<string, object>) == parameterType)
                            || (parameterType.IsGenericType && typeof(Dictionary<,>) == parameterType.GetGenericTypeDefinition()))
                        {
                            JsonReader jsonReader = new JsonReader(value);
                            arguments[parameterIndex] = jsonReader.Deserialize(parameterType);
                        }
                        else if (typeof(JsonReader) == parameterType)
                            arguments[parameterIndex] = new JsonReader(value);

                        else if (typeof(bool) == parameterType || typeof(bool?) == parameterType)
                        {
                            if ("on".Equals(value.ToLower()))
                                arguments[parameterIndex] = true;
                            else
                                arguments[parameterIndex] = Convert.ToBoolean(value);
                        }

                        else if ((typeof(DateTime) == parameterType) || (typeof(DateTime?) == parameterType))
                        {
                            if (null != value)
                            {
                                JsonReader jsonReader = new JsonReader(value);
                                arguments[parameterIndex] = jsonReader.Deserialize<DateTime>();
                            }
                        }

                        else if (typeof(Guid) == parameterType || typeof(Guid?) == parameterType)
                            arguments[parameterIndex] = new Guid(value.ToString());

                        // Nullable
                        else if (parameterType.IsGenericType && typeof(Nullable<>) == parameterType.GetGenericTypeDefinition())
                            arguments[parameterIndex] = Convert.ChangeType(value, parameterType.GetGenericArguments()[0]);

                        // Arrays
                        else if (parameterType.IsArray)
                        {
                            JsonReader jsonReader = new JsonReader(value);
                            arguments[parameterIndex] = jsonReader.Deserialize(parameterType);
                        }

                        // Everything else
                        else
                            arguments[parameterIndex] = Convert.ChangeType(value, parameterType);
                    }
                    else
                        arguments[parameterIndex] = null;
                }

            // The first argument is always the web connection
            arguments[0] = webConnection;

            object toReturn = MethodInfo.Invoke(webHandlerPlugin, arguments);
            return (IWebResults)toReturn;
        }
    }
}
