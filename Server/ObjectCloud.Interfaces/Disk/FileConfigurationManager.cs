// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using JsonFx.Json;

using ObjectCloud.Common;

namespace ObjectCloud.Interfaces.Disk
{
    public class FileConfigurationManager
    {
        public FileConfigurationManager(string fileType)
        {
            _Deserialized = new Dictionary<string, object>();
            _Deserialized["Actions"] = _Actions = new Dictionary<string, string>();
            _Deserialized["FileType"] = fileType;
        }

        public FileConfigurationManager(ITextHandler configurationFile)
        {
            configurationFile.ContentsChanged += new Common.EventHandler<ITextHandler, EventArgs>(configurationFile_ContentsChanged);
            ConfigurationFile = configurationFile;
        }

        void configurationFile_ContentsChanged(ITextHandler sender, EventArgs e)
        {
            _Deserialized = null;
            _Actions = null;
            _Javascript = null;
            _ViewComponents = null;
        }

        /// <summary>
        /// The deserialized options
        /// </summary>
        public Dictionary<string, object> Deserialized
        {
            get 
            {
                Dictionary<string, object> deserialized = _Deserialized;

                if (null == deserialized)
                    do
                        deserialized = JsonReader.Deserialize<Dictionary<string, object>>(ConfigurationFile.ReadAll());
                    while (null == Interlocked.CompareExchange<Dictionary<string, object>>(ref _Deserialized, deserialized, null));

                return deserialized; 
            }
        }
        private Dictionary<string, object> _Deserialized = null;

        private ITextHandler ConfigurationFile;

        /// <summary>
        /// The actions
        /// </summary>
        public Dictionary<string, string> Actions
        {
            get
            {
                Dictionary<string, string> actions = _Actions;

                if (null == actions)
                    do
                    {
                        actions = new Dictionary<string, string>();

                        object actionsObj;
                        if (Deserialized.TryGetValue("Actions", out actionsObj))
                            foreach (KeyValuePair<string, object> kvp in (IEnumerable<KeyValuePair<string, object>>)actionsObj)
                                actions[kvp.Key] = kvp.Value.ToString();

                    } while (null == Interlocked.CompareExchange<Dictionary<string, string>>(ref _Actions, actions, null));

                return actions;
            }
        }
        private Dictionary<string, string> _Actions = null;

        /// <summary>
        /// The view components
        /// </summary>
        public Dictionary<string, object>[] ViewComponents
        {
            get
            {
                Dictionary<string, object>[] viewComponents = _ViewComponents;

                if (null == viewComponents)
                    do
                    {
                        object viewComponentsObj;
                        if (Deserialized.TryGetValue("ViewComponents", out viewComponentsObj))
                            viewComponents = Enumerable<Dictionary<string, object>>.ToArray(
                                Enumerable<Dictionary<string, object>>.Cast((IEnumerable)viewComponentsObj));
                        else
                            viewComponents = new Dictionary<string, object>[0];

                    } while (null == Interlocked.CompareExchange<Dictionary<string, object>[]>(ref _ViewComponents, viewComponents, null));

                return viewComponents;
            }
        }
        private Dictionary<string, object>[] _ViewComponents = null;

        /// <summary>
        /// The file type
        /// </summary>
        public string FileType
        {
            get 
            {
                object fileType;
                if (Deserialized.TryGetValue("FileType", out fileType))
                    return fileType.ToString();

                return null;
            }
        }

        /// <summary>
        /// The Javascript options, or null if unused
        /// </summary>
        public Dictionary<string, object> Javascript
        {
            get
            {
                Dictionary<string, object> javascript = _Javascript;

                if (null == javascript)
                    do
                    {
                        object javascriptObject;
                        if (Deserialized.TryGetValue("Javascript", out javascriptObject))
                            javascript = (Dictionary<string, object>)javascriptObject;
                        else
                            return null;
                    }
                    while (null == Interlocked.CompareExchange<Dictionary<string, object>>(ref _Javascript, javascript, null));

                return javascript;
            }
        }
        private Dictionary<string, object> _Javascript = null;

        /// <summary>
        /// The javascript file, or null if unused
        /// </summary>
        public string JavascriptFile
        {
            get 
            {
                Dictionary<string, object> javascript = Javascript;

                if (null == javascript)
                    return null;

                return javascript["File"].ToString();
            }
        }

        /// <summary>
        /// True if only Javascript web methods should be called, false otherwise
        /// </summary>
        public bool BlockWebMethods
        {
            get
            {
                Dictionary<string, object> javascript = Javascript;

                if (null != javascript)
                {
                    object blockWebMethods;
                    if (javascript.TryGetValue("BlockWebMethods", out blockWebMethods))
                        if (blockWebMethods is bool)
                            return (bool)blockWebMethods;
                }

                return false;
            }
        }
    }
}
