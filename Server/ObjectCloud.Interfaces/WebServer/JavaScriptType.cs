using System;
using System.Collections.Generic;
using System.Text;

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// Represents the type that an argument can be
    /// </summary>
    public enum JavaScriptType
    {
        /// <summary>
        /// The argument is a value type, such as a string, number, or boolean.  It will be transported as a string
        /// </summary>
        Value,

        /// <summary>
        /// The arugment is an object that will have its properties serialized into JSON for transport.
        /// </summary>
        Object
    }
}
