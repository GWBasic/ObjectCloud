using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Declares the name of an API method for manipulating an object / file
    /// </summary>
    public class JavaScriptMethodAttribute : Attribute
    {
        /// <summary>
        /// Declares a method presentend in the automatically-generated JavaScript API for a file/object of this type.
        /// </summary>
        /// <param name="name">The method name in the JavaScript API.  JavaScript does not support overloading, so use different names for variations on arguments or web methods.</param>
        /// <param name="webMethod">The web request method, either GET, POST, or PUT</param>
        /// <param name="documentation">The method's documention that's put into metadata</param>
        /// <param name="returnType">The kind of value returned by the method, either a value or a JSON-encoded object</param>
        public JavaScriptMethodAttribute(string name, WebMethod webMethod, string documentation, JavaScriptType returnType)
        {
            _Name = name;
            _WebMethod = webMethod;
            _Documentation = documentation;
            _ReturnType = ReturnType;
        }

        /// <summary>
        /// Declares a method presentend in the automatically-generated JavaScript API for a file/object of this type.
        /// </summary>
        /// <param name="name">The method name in the JavaScript API.  JavaScript does not support overloading, so use different names for variations on arguments or web methods.</param>
        /// <param name="webMethod">The web request method, either GET, POST, or PUT</param>
        /// <param name="documentation">The method's documention that's put into metadata</param>
        public JavaScriptMethodAttribute(string name, WebMethod webMethod, string documentation)
        {
            _Name = name;
            _WebMethod = webMethod;
            _Documentation = documentation;
        }

        /// <summary>
        /// The method name in the JavaScript API
        /// </summary>
        public string Name
        {
            get { return _Name; }
        }
        private string _Name;

        /// <summary>
        /// The web request method, either GET, POST, or PUT
        /// </summary>
        public WebMethod WebMethod
        {
            get { return _WebMethod; }
        }
        private WebMethod _WebMethod;

        /// <summary>
        /// The method's documention that's put into metadata
        /// </summary>
        public string Documentation
        {
            get { return _Documentation; }
        }
        private string _Documentation;

        /// <summary>
        /// The kind of value returned by the method, either a value or a JSON-encoded object; or null if the method does not return a value
        /// </summary>
        public JavaScriptType? ReturnType
        {
            get { return _ReturnType; }
        }
        private JavaScriptType? _ReturnType;
    }
}
