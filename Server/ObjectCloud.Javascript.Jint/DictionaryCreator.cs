// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Jint;
using Jint.Expressions;
using Jint.Native;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;
using ObjectCloud.Interfaces.WebServer;
using ObjectCloud.WebAccessCodeGenerators;

namespace ObjectCloud.Javascript.Jint
{
    /// <summary>
    /// Creates dictionaries from Javascript Objects
    /// </summary>
    internal static class DictionaryCreator
    {
        /// <summary>
        /// Converts a Jint JsDictionaryObject to a JSON-serializable Dictionary
        /// </summary>
        /// <param name="fromJint"></param>
        /// <returns></returns>
        internal static IDictionary<object, object> ToDictionary(JsObject fromJint)
        {
            Dictionary<object, object> toReturn = new Dictionary<object, object>();

            foreach (KeyValuePair<string, JsInstance> kvp in fromJint)
            {
                if ((kvp.Value is JsNumber) || (kvp.Value is JsBoolean) || (kvp.Value is JsString))
                //if (null != kvp.Value.Value)
                    toReturn[kvp.Key] = kvp.Value.Value;

                else if (kvp.Value is JsObject)
                    toReturn[kvp.Key] = ToDictionary((JsObject)kvp.Value);

                else if (kvp.Value is JsNull)
                    toReturn[kvp.Key] = null;

                // else ignore non-JSONable types
            }

            return toReturn;
        }

        /// <summary>
        /// Converts a C# Dictionary from JSON to a Jint object
        /// </summary>
        /// <param name="fromJSON"></param>
        /// <returns></returns>
        internal static JsDictionaryObject ToObject(IDictionary<string, object> fromJSON)
        {
            return ToObject<string, object>(fromJSON);
        }

        /// <summary>
        /// Converts a C# Dictionary from JSON to a Jint object
        /// </summary>
        /// <param name="fromJSON"></param>
        /// <returns></returns>
        internal static JsDictionaryObject ToObject<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> fromJSON)
        {
            JsObject toReturn = new JsObject();

            foreach (KeyValuePair<TKey, TValue> kvp in fromJSON)
            {
                JsInstance value;

                if (null == kvp.Value)
                    value = JsNull.Instance;

                else if (kvp.Value is double)
                    value = new JsNumber((double)(object)kvp.Value);

                else if (kvp.Value is string)
                    value = new JsString(kvp.Value.ToString());

                else if (kvp.Value is bool)
                    value = new JsBoolean((bool)(object)kvp.Value);

                else if (kvp.Value is IDictionary<string, object>)
                    value = ToObject((IDictionary<string, object>)kvp.Value);

                else
                    throw new JavascriptException("Can not convert a " + kvp.Value.GetType().ToString() + " to a JsInstance");

                toReturn[kvp.Key.ToString()] = value;
            }

            return toReturn;
        }
    }
}
