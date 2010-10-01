// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the Simple Public License (SimPL) 2.0.  Some additional privelages are granted.
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Common.Threading;
using ObjectCloud.DataAccess.NameValuePairs;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.FileHandlers
{
    public class NameValuePairsHandler : HasDatabaseFileHandler<IDatabaseConnector, IDatabaseConnection, IDatabaseTransaction>, INameValuePairsHandler
    {
        public NameValuePairsHandler(IDatabaseConnector databaseConnector, FileHandlerFactoryLocator fileHandlerFactoryLocator)
            : base(databaseConnector, fileHandlerFactoryLocator) { }

        public string this[string name]
        {
            get
            {
                IPairs_Readable pair = DatabaseConnection.Pairs.SelectSingle(Pairs_Table.Name == name);

                if (null == pair)
                    return null;

                return pair.Value;
            }
        }

        public void Set(IUser changer, string name, string value)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                // Not sure if it's worth trying to update as opposed to delete...
                // Update might be faster, but right now the data access system doesn't support it!
                DatabaseConnection.Pairs.Delete(Pairs_Table.Name == name);

                if (null != value)
                    DatabaseConnection.Pairs.Insert(delegate(IPairs_Writable pair)
                    {
                        pair.Name = name;
                        pair.Value = value;
                    });

                transaction.Commit();
            });

            // TODO, figure out a way to describe the change in the changedata
            SendUpdateNotificationFrom(changer);
        }

        public bool Contains(string key)
        {
            return this[key] != null;
        }

        public void Clear(IUser changer)
        {
            DatabaseConnection.Pairs.Delete();

            SendUpdateNotificationFrom(changer);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            List<KeyValuePair<string, string>> toReturn = new List<KeyValuePair<string, string>>();

            foreach (IPairs_Readable pair in DatabaseConnection.Pairs.Select())
                toReturn.Add(new KeyValuePair<string, string>(pair.Name, pair.Value));

            return toReturn.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void WriteAll(IUser changer, IEnumerable<KeyValuePair<string, string>> contents, bool clearExisting)
        {
            DatabaseConnection.CallOnTransaction(delegate(IDatabaseTransaction transaction)
            {
                if (clearExisting)
                    DatabaseConnection.Pairs.Delete();

                foreach (KeyValuePair<string, string> kvp in contents)
                {
                    DatabaseConnection.Pairs.Delete(Pairs_Table.Name == kvp.Key);

                    DatabaseConnection.Pairs.Insert(delegate(IPairs_Writable pair)
                    {
                        pair.Name = kvp.Key;
                        pair.Value = kvp.Value;
                    });
                }

                transaction.Commit();
            });

            SendUpdateNotificationFrom(changer);
        }

        public override void Dump(string path, ID<IUserOrGroup, Guid> userId)
        {
            using (TimedLock.Lock(this))
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

                        foreach (KeyValuePair<string, string> kvp in this)
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
            }
        }

        public override void SyncFromLocalDisk(string localDiskPath, bool force)
        {
            DateTime authoritativeCreated = File.GetLastWriteTimeUtc(localDiskPath);
            DateTime thisCreated = DatabaseConnector.LastModified;

            if (authoritativeCreated > thisCreated || force)
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

                            Set(null, name, value);
                        }
                    }
                }
        }
    }
}