// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using Common.Logging;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Encapsulates the parameters from a GET or POST request
    /// </summary>
    public class RequestParameters : IDictionary<string, string>, ICloneable
    {
		private static ILog log = LogManager.GetLogger<RequestParameters>();
		
        public RequestParameters() { }

        public RequestParameters(string unparsedParameters)
        {
            try
            {
                string[] parms = unparsedParameters.Split(new string[] {"&"}, StringSplitOptions.RemoveEmptyEntries);

                foreach (string parm in parms)
                {
                    string[] nameAndValue = parm.Split('=');

                    string name = HTTPStringFunctions.DecodeRequestParametersFromBrowser(nameAndValue[0]);

                    string value;
                    if (nameAndValue.Length > 1)
                        value = HTTPStringFunctions.DecodeRequestParametersFromBrowser(nameAndValue[1]);
                    else
                        value = "";

                    BaseDictionary[name] = value;
                }
            }
            catch (Exception e)
            {
				log.Error("Exception when parsing parameters", e);
            }
        }

        /// <summary>
        /// The base dictionary
        /// </summary>
        private Dictionary<string, string> BaseDictionary = new Dictionary<string,string>();

        /// <summary>
        /// Returns the given parameter.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string this[string key]
        {
            get { return BaseDictionary[key]; }
            set { BaseDictionary[key] = value; }
        }

        /// <summary>
        /// True if the named parameter is present, false otherwise
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(string key)
        {
            return BaseDictionary.ContainsKey(key);
        }

        /// <summary>
        /// Adds the request parameter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, string value)
        {
            BaseDictionary.Add(key, value);
        }

        /// <summary>
        /// Removes the request parameter
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            return BaseDictionary.Remove(key);
        }

        #region IDictionary<string,string> Members


        public ICollection<string> Keys
        {
            get { return BaseDictionary.Keys; }
        }

        public bool TryGetValue(string key, out string value)
        {
            return BaseDictionary.TryGetValue(key, out value);
        }

        public ICollection<string> Values
        {
            get { return BaseDictionary.Values; }
        }

        #endregion

        #region ICollection<KeyValuePair<string,string>> Members

        public void Add(KeyValuePair<string, string> item)
        {
            ((IDictionary<string, string>)BaseDictionary).Add(item);
        }

        public void Clear()
        {
            BaseDictionary.Clear();
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return ((IDictionary<string, string>)BaseDictionary).Contains(item);
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            ((IDictionary<string, string>)BaseDictionary).CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return BaseDictionary.Count; }
        }

        public bool IsReadOnly
        {
            get { return ((IDictionary<string, string>)BaseDictionary).IsReadOnly; }
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return ((IDictionary<string, string>)BaseDictionary).Remove(item);
        }

        #endregion

        #region IEnumerable<KeyValuePair<string,string>> Members

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return ((IDictionary<string, string>)BaseDictionary).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((System.Collections.IEnumerable)BaseDictionary).GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Returns a clone of this object
        /// </summary>
        /// <returns></returns>
        public RequestParameters Clone()
        {
            RequestParameters toReturn = new RequestParameters();
            toReturn.BaseDictionary = new Dictionary<string, string>(BaseDictionary);

            return toReturn;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public override int GetHashCode()
        {
            return BaseDictionary.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is RequestParameters)
                return BaseDictionary.Equals(((RequestParameters)obj).BaseDictionary);

            return base.Equals(obj);
        }

        /// <summary>
        /// Returns as p1=v1&p2=v2...
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToURLEncodedString();
        }

        /// <summary>
        /// Returns as p1=v1&p2=v2...
        /// </summary>
        /// <returns></returns>
        public string ToURLEncodedString()
        {
            List<string> encodedParameters = new List<string>();

            foreach (KeyValuePair<string, string> parameter in this)
            {
                string parmString = string.Format("{0}={1}",
                    HTTPStringFunctions.EncodeRequestParametersForBrowser(parameter.Key),
                    HTTPStringFunctions.EncodeRequestParametersForBrowser(parameter.Value));
                encodedParameters.Add(parmString);
            }

            return StringGenerator.GenerateSeperatedList(encodedParameters, "&");
        }

        /// <summary>
        /// Returns bytes that represent the contents of the parameters
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(ToString());
        }
    }
}
