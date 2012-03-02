// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Reads an incoming MIME message and breaks it up for easy consumption
    /// </summary>
    public class MimeReader : IEnumerable<MimeReader.Part>
    {
        /// <summary>
        /// Creates a MimeReader
        /// </summary>
        /// <param name="boundaryString">The boundary string.  This is all of the text after boundary= in the Content-type of the HTTP request header</param>
        /// <param name="stream">A stream that contains the POSTed contents of the HTTP request</param>
        public MimeReader(string boundaryString, Stream stream)
        {
            byte[] boundary = Encoding.UTF8.GetBytes(boundaryString);

            List<byte> bytesRead = new List<byte>();

            byte[] bufferStart = new byte[boundary.Length];
            stream.Read(bufferStart, 0, bufferStart.Length);

            List<byte> buffer = new List<byte>(bufferStart);

            int notByte;
            while (-1 != (notByte = stream.ReadByte()))
            {
                if (Enumerable.Equals(boundary, buffer))
                {
                    AddPart(bytesRead);

                    int numBytesRead = stream.Read(bufferStart, 0, bufferStart.Length);
                    if (numBytesRead < bufferStart.Length)
                    {
                        // the end of the stream is reached!
                        for (int ctr = 0; ctr < numBytesRead; ctr++)
                            bytesRead.Add(bufferStart[ctr]);
                    }

                    buffer = new List<byte>(bufferStart);

                    bytesRead.Clear();
                }
                else
                {
                    bytesRead.Add(buffer[0]);
                    buffer.RemoveAt(0);
                    buffer.Add(Convert.ToByte(notByte));
                }
            }

            AddPart(bytesRead);
        }

        private void AddPart(List<byte> bytesRead)
        {
            Part part;

            try
            {
                part = new Part(bytesRead.ToArray());
            }
            catch
            {
                return;
            }

            AllParts.Add(part);

            if (null != part.Name)
                NamedParts[part.Name] = part;
        }

        /// <summary>
        /// All of the parts in the MIME message
        /// </summary>
        private List<Part> AllParts = new List<Part>();

        /// <summary>
        /// Returns the part at the given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Part this[int index]
        {
            get { return AllParts[index]; }
        }

        public IEnumerator<MimeReader.Part> GetEnumerator()
        {
            return AllParts.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return AllParts.GetEnumerator();
        }

        /// <summary>
        /// All of the parts in the MIME message where the name was successfully parsed
        /// </summary>
        private Dictionary<string, Part> NamedParts = new Dictionary<string, Part>();

        /// <summary>
        /// Returns the part with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Part this[string name]
        {
            get { return NamedParts[name]; }
        }

        /// <summary>
        /// Returns true if there is a part with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool ContainsNamedPart(string name)
        {
            return NamedParts.ContainsKey(name);
        }

        /// <summary>
        /// A part of the MIME message
        /// </summary>
        public class Part
        {
            public Part(byte[] bytes)
            {
                // determine where the text ends and the content starts
                byte[] toInspect = new byte[4];

                for (int ctr = 3; ctr < bytes.Length && null == _Contents; ctr++)
                {
                    toInspect[0] = bytes[ctr - 3];
                    toInspect[1] = bytes[ctr - 2];
                    toInspect[2] = bytes[ctr - 1];
                    toInspect[3] = bytes[ctr];

                    string toInspectString = Encoding.UTF8.GetString(toInspect);

                    if (toInspectString.Equals("\r\n\r\n") | toInspectString.Equals("\n\r\n\r") | toInspectString.EndsWith("\n\n"))
                    {
                        _Contents = new byte[bytes.Length - ctr - 1];
                        Array.Copy(bytes, ctr + 1, _Contents, 0, _Contents.Length);
                    }
                }

                // Now that the content is known, read the headers

                MemoryStream stream = new MemoryStream(bytes);

                using (StreamReader contentsReader = new StreamReader(stream))
                {
                    string line;

                    // Parse the metadata that comes on as a line-by-line basis
                    while ((line = contentsReader.ReadLine()).Length > 0 || Headers.Count == 0)
                        if (line.Length > 0)
                        {
                            string[] tokens = line.Split(new char[] { ':' }, 2);
                            Headers[tokens[0].Trim().ToUpper()] = tokens[1].Trim();
                        }

                    // Parse the Content-Disposition
                    if (Headers.ContainsKey("CONTENT-DISPOSITION"))
                    {
                        string contentDispositionString = ContentDispositionString;

                        foreach (string contentDispositionValue in contentDispositionString.Split(';'))
                        {
                            string[] nameAndValue = contentDispositionValue.Split(new char[] { '=' }, 2);

                            if (nameAndValue.Length == 2)
                            {
                                string value = nameAndValue[1].Trim();

                                if (value.StartsWith("\""))
                                    value = value.Substring(1);

                                if (value.EndsWith("\""))
                                    value = value.Substring(0, value.Length - 1);

                                _ContentDisposition[nameAndValue[0].Trim().ToUpper()] = value;
                            }
                            else
                                _ContentDisposition[nameAndValue[0].Trim().ToUpper()] = null;
                        }

                        if (_ContentDisposition.ContainsKey("NAME"))
                            _Name = _ContentDisposition["NAME"];
                    }
                }

                // Read the actual uploaded content
                /*_Contents = new byte[stream.Length - stream.Position];
                stream.Read(_Contents, 0, _Contents.Length);*/
            }

            public string Name
            {
                get { return _Name; }
            }
            private readonly string _Name = null;

            /// <summary>
            /// Each named line sent as a header
            /// </summary>
            Dictionary<string, string> Headers = new Dictionary<string, string>();

            /// <summary>
            /// Returns the header with the given name
            /// </summary>
            /// <param name="headerName"></param>
            /// <returns></returns>
            public string this[string headerName]
            {
                get { return Headers[headerName]; }
            }
    
            /// <summary>
            /// The Content-Disposition, if sent
            /// </summary>
            public string ContentDispositionString
            {
                get { return Headers["CONTENT-DISPOSITION"]; }
            }

            /// <summary>
            /// The Content-Type, if sent
            /// </summary>
            public string ContentType
            {
                get { return Headers["CONTENT-TYPE"]; }
            }

            /// <summary>
            /// Returns true if the part is a file
            /// </summary>
            public bool IsFile
            {
                get
                {
                    string filename;
                    if (_ContentDisposition.TryGetValue("FILENAME", out filename))
                        return filename.Length > 0;

                    return false;
                }
            }

            /// <summary>
            /// The parsed Content-Disposition, if sent.  All keys are in upper case
            /// </summary>
            public Dictionary<string, string> ContentDisposition
            {
                get { return _ContentDisposition; }
            }
            readonly Dictionary<string, string> _ContentDisposition = new Dictionary<string, string>();

            /// <summary>
            /// The contents of the message
            /// </summary>
            public byte[] Contents
            {
                get { return _Contents; }
            }
            private readonly byte[] _Contents = null;
        }
    }
}
