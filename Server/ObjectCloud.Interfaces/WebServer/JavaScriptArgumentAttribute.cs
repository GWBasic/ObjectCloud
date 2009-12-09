using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Represents an argument for a method in JavaScript
    /// </summary>
    public class JavaScriptArgumentAttribute
    {
        /// <summary>
        /// Declares an argument in the automatically-generated JavaScript API for a file / object
        /// </summary>
        /// <param name="name">The arugment's name</param>
        /// <param name="type">The argument's type</param>
        /// <param name="description">The argument's description that shows up in documentation for this method</param>
        public JavaScriptArgumentAttribute(string name, JavaScriptType type, string description)
        {
            _Name = name;
            _Type = type;
            _Description = description;
        }

        /// <summary>
        /// The argument's name
        /// </summary>
        public string Name
        {
            get { return _Name; }
        }
        private string _Name;

        /// <summary>
        /// The argument's type
        /// </summary>
        public JavaScriptType Type
        {
            get { return _Type; }
        }
        private JavaScriptType _Type;

        /// <summary>
        /// The argument's description that shows up in documentation for this method
        /// </summary>
        public string Description
        {
            get { return _Description; }
        }
        private string _Description;
    }
}
