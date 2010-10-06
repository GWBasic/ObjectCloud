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
    public class JSONSetReader : HasFileHandlerFactoryLocator
    {
        public JSONSetReader(FileHandlerFactoryLocator fileHandlerFactoryLocator, string filename)
        {
            FileHandlerFactoryLocator = fileHandlerFactoryLocator;
            Filename = filename;
        }

        private readonly string Filename;

        /// <summary>
        /// The named set that's stored in this JSON file
        /// </summary>
        public Set<string> Set
        {
            get
            {
                Set<string> set = _Set;

                if (null == set)
                {
                    set = new Set<string>();

                    IFileContainer fileContainer = FileHandlerFactoryLocator.FileSystemResolver.ResolveFile(Filename);
                    ITextHandler textHandler = fileContainer.CastFileHandler<ITextHandler>();

                    foreach (string item in JsonReader.Deserialize<object[]>(textHandler.ReadAll()))
                        set.Add(item);

                    _Set = set;
                }

                return set;
            }
        }
        private Set<string> _Set = null;

        void textHandler_ContentsChanged(ITextHandler sender, EventArgs e)
        {
            sender.ContentsChanged -= new EventHandler<ITextHandler, EventArgs>(textHandler_ContentsChanged);
            _Set = null;
        }
    }
}
