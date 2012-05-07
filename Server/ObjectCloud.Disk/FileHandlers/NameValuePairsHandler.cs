// Copyright 2009 - 2012 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.FileHandlers
{
    public class NameValuePairsHandler : LastModifiedFileHandler, INameValuePairsHandler
    {
        public NameValuePairsHandler(FileHandlerFactoryLocator fileHandlerFactoryLocator, string path)
			: base(fileHandlerFactoryLocator, path)
		{
			this.persistedPairs = new PersistedBinaryFormatterObject<Dictionary<string, string>>(
				path,
				() => new Dictionary<string, string>());
		}

		private readonly PersistedBinaryFormatterObject<Dictionary<string, string>> persistedPairs;
		
        public string this[string name]
        {
            get
            {
				return this.persistedPairs.Read<string>(pairs =>
				{
					string toReturn = null;
					pairs.TryGetValue(name, out toReturn);
					return toReturn;
				});
            }
        }

        public void Set(IUser changer, string name, string value)
        {
			this.persistedPairs.Write(pairs =>
			{
				if (null == value)
					pairs.Remove(name);
				else
					pairs[name] = value;
			});

            // TODO, figure out a way to describe the change in the changedata
            SendUpdateNotificationFrom(changer);
        }

        public bool Contains(string key)
        {
            return this.persistedPairs.Read<bool>(pairs => pairs.ContainsKey(key));
        }

        public void Clear(IUser changer)
        {
			this.persistedPairs.Write(pairs => pairs.Clear());

            this.SendUpdateNotificationFrom(changer);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
			return this.persistedPairs.Read<IEnumerator<KeyValuePair<string, string>>>(
				pairs => new Dictionary<string, string>(pairs).GetEnumerator());
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void WriteAll(IUser changer, IEnumerable<KeyValuePair<string, string>> contents, bool clearExisting)
        {
            this.persistedPairs.Write(pairs =>
            {
                if (clearExisting)
                    pairs.Clear();

                foreach (KeyValuePair<string, string> kvp in contents)
					pairs[kvp.Key] = kvp.Value;
            });

            SendUpdateNotificationFrom(changer);
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
			this.persistedPairs.Read(pairs =>
            {
                DateTime destinationCreated = DateTime.MinValue;

                if (File.Exists(path))
                    destinationCreated = File.GetLastWriteTimeUtc(path);

                if (destinationCreated < LastModified)
                {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.IndentChars = "\t";
                    settings.NewLineChars = "\n";
                    settings.NewLineHandling = NewLineHandling.Entitize;
                    settings.NewLineOnAttributes = false;

                    using (XmlWriter xmlWriter = XmlWriter.Create(path, settings))
                    {
                        xmlWriter.WriteStartDocument();
                        xmlWriter.WriteStartElement("NameValuePairs");

                        foreach (KeyValuePair<string, string> kvp in pairs)
                        {
                            xmlWriter.WriteStartElement("NameValuePair");

                            xmlWriter.WriteAttributeString("Name", kvp.Key);
                            xmlWriter.WriteAttributeString("Value", kvp.Value);

                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();
                        xmlWriter.WriteEndDocument();

                        xmlWriter.Flush();
                        xmlWriter.Close();
                    }
                }
            });
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force, DateTime lastModified)
        {
            if (lastModified > this.LastModified || force)
				this.persistedPairs.Write(pairs =>
				{
		                using (TextReader tr = File.OpenText(localDiskPath))
		                using (XmlReader xmlReader = XmlReader.Create(tr))
		                {
		                    xmlReader.MoveToContent();
		
		                    while (!xmlReader.Name.Equals("NameValuePairs"))
		                        if (!xmlReader.Read())
		                            throw new SystemFileException("<NameValuePairs> tag missing");
		
		                    while (xmlReader.Read())
		                    {
		                        if ("NameValuePair".Equals(xmlReader.Name))
		                        {
		                            string name = xmlReader.GetAttribute("Name");
		                            string value = xmlReader.GetAttribute("Value");
		
		                            pairs[name] = value;
		                        }
		                    }
		                }
				});
        }
    }
}