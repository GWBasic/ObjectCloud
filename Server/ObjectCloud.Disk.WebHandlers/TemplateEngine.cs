// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;

namespace ObjectCloud.Disk.WebHandlers
{
    /// <summary>
    /// Generates complete files from templates
    /// </summary>
    public class TemplateEngine : WebHandler
    {
        private const string TemplateNamespace = "objectcloud_templating";

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="webConnection"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public IWebResults Evaluate(IWebConnection webConnection, string filename)
        {
            return WebResults.FromString(Status._200_OK, EvaluateToString(webConnection.GetParameters, filename));
        }

        /// <summary>
        /// Evaluates the named template
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        [WebCallable(WebCallingConvention.GET_application_x_www_form_urlencoded, WebReturnConvention.Primitive, FilePermissionEnum.Read)]
        public string EvaluateToString(IDictionary<string, string> getParameters, string filename)
        {
            IFileContainer templateFileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(filename);
            IFileHandler templateFileHandler = templateFileContainer.FileHandler;

            if (!(templateFileHandler is ITextHandler))
                throw new WebResultsOverrideException(WebResults.FromString(Status._400_Bad_Request, filename + " must be a text file"));

            string templateContents = ((ITextHandler)templateFileHandler).ReadAll();
            templateContents = ReplaceGetParameters(getParameters, templateContents);

            XmlDocument templateDocument = new XmlDocument();
            templateDocument.LoadXml(templateContents);

            // While the first node isn't HTML, keep loading header/footers
            /*XmlNode firstChild = templateDocument.FirstChild;
            while ("html" != firstChild.Name)
            {
                string headerFooter = "/DefaultTemplate/headerfooter.ochf";

                if (("componentdef" == firstChild.Name) && (TemplateNamespace == firstChild.NamespaceURI))
                {
                    firstChild.Attributes.
                }
            }*/

            return templateContents;
        }

        /// <summary>
        /// Delimeter for the beginning of GET arguments
        /// </summary>
        static string[] ArgBegin = new string[] { "[_" };

        /// <summary>
        /// Delimeter for the end of GET arguments
        /// </summary>
        static string[] ArgEnd = new string[] { "_]" };

        /// <summary>
        /// Replaces all of the GET parameters in a string
        /// </summary>
        /// <param name="getParameters"></param>
        /// <param name="templateContents"></param>
        /// <returns></returns>
        private string ReplaceGetParameters(IDictionary<string, string> getParameters, string templateContents)
        {
            // Split at [_
            StringBuilder getArgumentsResolvedBuilder = new StringBuilder(Convert.ToInt32(1.1 * Convert.ToDouble(templateContents.Length)));

            // Allocate the results builder, give a little breathing room in case the size grows
            string[] templateSplitAtArgs = templateContents.Split(ArgBegin, StringSplitOptions.None);

            int ctr;
            if (templateContents.StartsWith("[_"))
                ctr = 0;
            else
            {
                ctr = 1;
                getArgumentsResolvedBuilder.Append(templateSplitAtArgs[0]);
            }

            for (; ctr < templateSplitAtArgs.Length; ctr++)
            {
                string[] argumentAndTemplateParts = templateSplitAtArgs[ctr].Split(ArgEnd, 2, StringSplitOptions.None);

                if (argumentAndTemplateParts.Length != 2)
                {
                    // If there is no _], put this back as-is
                    getArgumentsResolvedBuilder.Append("[_");
                    getArgumentsResolvedBuilder.Append(templateSplitAtArgs[ctr]);
                }
                else
                {
                    string argument = StringParser.XmlDecode(argumentAndTemplateParts[0].Trim());
                    string remainder = argumentAndTemplateParts[1];

                    if (getParameters.ContainsKey(argument))
                        getArgumentsResolvedBuilder.Append(StringParser.XmlEncode(getParameters[argument]));

                    getArgumentsResolvedBuilder.Append(remainder);
                }
            }

            return getArgumentsResolvedBuilder.ToString();
        }


    }
}
