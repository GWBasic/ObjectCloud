// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using JsonFx.Json;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;

namespace ObjectCloud.Interfaces.Utilities
{
    public class JSONNamedSetReader : HasFileHandlerFactoryLocator
    {
        public JSONNamedSetReader(FileHandlerFactoryLocator fileHandlerFactoryLocator, string filename)
        {
            FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            Filename = filename;
        }

        private readonly string Filename;

        /// <summary>
        /// The named set that's stored in this JSON file
        /// </summary>
        public Dictionary<string, Set<string>> NamedSet
        {
            get
            {
                if (null == _NamedSet)
                    lock (Key)
                        if (null == _NamedSet)
                        {
                            Dictionary<string, Set<string>> namedSet = new Dictionary<string, Set<string>>();

                            IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(Filename);
                            ITextHandler textHandler = fileContainer.CastFileHandler<ITextHandler>();

                            Dictionary<string, object> namedSetFromFile = JsonReader.Deserialize<Dictionary<string, object>>(textHandler.ReadAll());
                            foreach (KeyValuePair<string, object> namespaceKVP in namedSetFromFile)
                            {
                                Set<string> validTags = new Set<string>(Enumerable<string>.Cast((IEnumerable)namespaceKVP.Value));
                                namedSet[namespaceKVP.Key] = validTags;
                            }

                            textHandler.ContentsChanged += new EventHandler<ITextHandler, EventArgs>(textHandler_ContentsChanged);

                            _NamedSet = namedSet;
                        }

                return _NamedSet;
            }
        }
        private Dictionary<string, Set<string>> _NamedSet = null;
        private object Key = new object();

        void textHandler_ContentsChanged(ITextHandler sender, EventArgs e)
        {
            _NamedSet = null;
        }
    }
}
