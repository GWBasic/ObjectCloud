// Copyright 2009 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

namespace ObjectCloud.Interfaces.WebServer
{
    /// <summary>
    /// The various calling conventions that ObjectCloud supports for calling methods on objects through HTTP.  This describes the HTTP method, either GET or POST, and the technique used to adapt the HTTP request into arguments
    /// </summary>
    public enum WebCallingConvention
    {
        /// <summary>
        /// HTTP method: GET; the called method is passed an IWebResults object and must discover its inputs
        /// </summary>
        GET,

        /// <summary>
        /// HTTP method: GET; URL arguments are automatically mapped to C# arguments.  ex: Method=Foo&num=123 maps to IWebResults Foo(int num) or Foo(int? num)  Primitives are automatically converted, non-nullable primitives will return an HTTP error if missing, nullable primitives and strings require null checking.  Dictionary will get a JSON-parsed object
        /// </summary>
        GET_application_x_www_form_urlencoded,

        /// <summary>
        /// HTTP method: POST; Body arguments are automatically mapped to C# arguments.  ex: num=123 maps to IWebResults Foo(IWebConnection wc, int num) or Foo(IWebConnection wc, int? num)  Primitives are automatically converted, non-nullable primitives will return an HTTP error if missing, nullable primitives and strings require null checking.  Dictionary will get a JSON-parsed object
        /// </summary>
        POST_application_x_www_form_urlencoded,

        /// <summary>
        /// HTTP method: POST; multipart/form-data.  Each part of the MIME body is mapped to a named C# argument of type MimeReader.Part.  For example, if expecting a part named ABC, use IWebResults MyMethod(IWebConnection wc, MimeReader.Part ABC)
        /// </summary>
        POST_multipart_form_data,

        /// <summary>
        /// HTTP method: POST, body contains a string in UTF8 encoding.  The first C# argument is an IWebConnection, and the second is a string
        /// </summary>
        POST_string,

        /// <summary>
        /// HTTP method: POST, body contains any data.  The first C# argument is an IWebConnection, and the second is a byte array
        /// </summary>
        POST_bytes,

        /// <summary>
        /// HTTP method: POST, body contains any data.  The first C# argument is an IWebConnection, and the second is a stream
        /// </summary>
        POST_stream,

        /// <summary>
        /// HTTP method: POST, body contains a JSON object.  The first C# argument is an IWebConnection, and the second is a Dictionary
        /// </summary>
        POST_JSON,

        /// <summary>
        /// Any kind of HTTP method.  The first and only supported argument is an IWebConnection
        /// </summary>
        other,

        /// <summary>
        /// Any HTTP verb.  The first and only supported argument is an IWebConnection.  The called method will perform verb checking and all argument parsing
        /// </summary>
        Naked
    }
}