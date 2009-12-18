// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Assists in getting documentation for ObjectCloud's classes
    /// </summary>
    public class DocumentationWebHandler : WebHandler<IFileHandler>
    {
        /// <summary>
        /// Returns all of the object types that the server supports
        /// </summary>
        /// <param name="webConnection"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetObjectTypes(IWebConnection webConnection)
        {
            List<object> toReturn = new List<object>();

            foreach (KeyValuePair<string, Type> objectTypeAndCSharpType in FileHandlerFactoryLocator.WebHandlerClasses)
            {
                Dictionary<string, object> toAdd = new Dictionary<string, object>();
                toAdd["ObjectType"] = objectTypeAndCSharpType.Key;

                using (XmlReader xmlReader = GetXmlReaderForType(objectTypeAndCSharpType.Value))
                    if (null != xmlReader)
                    {
                        Type cSharpType = objectTypeAndCSharpType.Value;
                        string classNameInXml = "T:" + cSharpType.Namespace + "." + cSharpType.Name;

                        while (xmlReader.Read())
                            if (xmlReader.Name == "member")
                            {
                                string nameAttribute = xmlReader.GetAttribute("name");

                                if (null != nameAttribute)
                                    if (classNameInXml == nameAttribute)
                                    {
                                        int currentLevel = xmlReader.Depth;

                                        do
                                        {
                                            xmlReader.Read();

                                            if ("summary" == xmlReader.Name)
                                                toAdd["Summary"] = xmlReader.ReadElementContentAsString();

                                        } while (xmlReader.Depth > currentLevel);
                                    }
                            }
                    }

                toReturn.Add(toAdd);
            }

            return WebResults.ToJson(toReturn);
        }

        /// <summary>
        /// Returns an XmlReader that has the documentation for the given type
        /// </summary>
        /// <param name="cSharpType"></param>
        /// <returns></returns>
        private XmlReader GetXmlReaderForType(Type cSharpType)
        {
            string documentationFileName = cSharpType.Assembly.ManifestModule.ScopeName;
            documentationFileName = documentationFileName.Substring(0, documentationFileName.IndexOf(".dll")) + ".xml";

            if (File.Exists(documentationFileName))
                return XmlReader.Create(documentationFileName);
            else
                return null;
        }

        /// <summary>
        /// Returns the methods in the given ObjectType
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="objectType"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.JavaScriptObject, FilePermissionEnum.Read)]
        public IWebResults GetMethodsForObjectType(IWebConnection webConnection, string objectType)
        {
            Type cSharpType = default(Type);
            if (!FileHandlerFactoryLocator.WebHandlerClasses.TryGetValue(objectType, out cSharpType))
                throw new WebResultsOverrideException(WebResults.FromString(Status._404_Not_Found, objectType + " isn't a known object type"));

            List<object> toReturn = new List<object>();

            GetMethodsForObjectTypeHelper(cSharpType, toReturn);

            return WebResults.ToJson(toReturn);
        }

        /// <summary>
        /// Recursively generates documentation for the web-accessible methods on the given type
        /// </summary>
        /// <param name="cSharpType"></param>
        /// <param name="toReturn"></param>
        private void GetMethodsForObjectTypeHelper(Type cSharpType, List<object> toReturn)
        {
            string methodNamePrefix = "M:" + cSharpType.Namespace + "." + cSharpType.Name;

            string documentationFileName = cSharpType.Assembly.ManifestModule.ScopeName;
            documentationFileName = documentationFileName.Substring(0, documentationFileName.IndexOf(".dll")) + ".xml";


            if (File.Exists(documentationFileName))
                using (XmlReader xmlReader = GetXmlReaderForType(cSharpType))
                    if (null != xmlReader)
                    {
                        while (xmlReader.Read())
                            if (xmlReader.Name == "member")
                            {
                                string nameAttribute = xmlReader.GetAttribute("name");

                                if (null != nameAttribute)
                                    if (nameAttribute.StartsWith(methodNamePrefix))
                                    {
                                        string methodName = nameAttribute.Substring(methodNamePrefix.Length + 1);

                                        if (methodName.Contains("("))
                                        {
                                            methodName = methodName.Substring(0, methodName.IndexOf('('));

                                            MethodInfo mi = cSharpType.GetMethod(methodName);

                                            // Only return methods that are web callable
                                            if (null != mi)
                                                if (mi.GetCustomAttributes(typeof(WebCallableAttribute), true).Length > 0)
                                                {
                                                    Dictionary<string, object> toAdd = new Dictionary<string, object>();
                                                    toAdd["Class"] = methodNamePrefix;
                                                    toAdd["Method"] = methodName;

                                                    int currentLevel = xmlReader.Depth;

                                                    do
                                                    {
                                                        xmlReader.Read();

                                                        if ("summary" == xmlReader.Name)
                                                            toAdd["Summary"] = xmlReader.ReadElementContentAsString();

                                                    } while (xmlReader.Depth > currentLevel);

                                                    toReturn.Add(toAdd);
                                                }
                                        }
                                    }
                            }
                    }

            // recurse for base types
            if (typeof(object) != cSharpType.BaseType)
                GetMethodsForObjectTypeHelper(cSharpType.BaseType, toReturn);
        }
    }
}
