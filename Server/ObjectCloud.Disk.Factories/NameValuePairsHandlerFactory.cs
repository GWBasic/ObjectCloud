// Copyright 2009, 2010 Andrew Rondeau
// This code is released under the LGPL license
// For more information, see either DefaultFiles/Docs/license.wchtml or /Docs/license.wchtml

using System;
using System.IO;
using System.Xml;

using Common.Logging;

using ObjectCloud.Common;
using ObjectCloud.DataAccess.NameValuePairs;
using ObjectCloud.Disk.FileHandlers;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Factories
{
    public class NameValuePairsHandlerFactory : FileHandlerFactory<NameValuePairsHandler>
    {
        /// <summary>
        /// Service locator for data access objects
        /// </summary>
        public DataAccessLocator DataAccessLocator
        {
            get { return _DataAccessLocator; }
            set { _DataAccessLocator = value; }
        }
        private DataAccessLocator _DataAccessLocator;

        public override NameValuePairsHandler CreateFile(string path)
        {
            Directory.CreateDirectory(path);

            string databaseFilename = CreateDatabaseFilename(path);

            DataAccessLocator.DatabaseCreator.Create(databaseFilename);

            return ConstructNameValuePairsHander(databaseFilename);
        }

        public override NameValuePairsHandler OpenFile(string path)
        {
            return ConstructNameValuePairsHander(CreateDatabaseFilename(path));
        }

        /// <summary>
        /// Creates the database file name
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CreateDatabaseFilename(string path)
        {
            return string.Format("{0}{1}db.sqlite", path, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Constructs the NameValuePairsHandler to return
        /// </summary>
        /// <param name="databaseFilename"></param>
        /// <returns></returns>
        private NameValuePairsHandler ConstructNameValuePairsHander(string databaseFilename)
        {
            NameValuePairsHandler toReturn = new NameValuePairsHandler(
                DataAccessLocator.DatabaseConnectorFactory.CreateConnectorForEmbedded(databaseFilename),
                FileHandlerFactoryLocator);

            return toReturn;
        }

        public override IFileHandler CopyFile(IFileHandler sourceFileHandler, ID<IFileContainer, long> fileId, ID<IUserOrGroup, Guid>? ownerID)
        {
            NameValuePairsHandler toReturn = CreateFile(FileSystem.GetFullPath(fileId));
            toReturn.WriteAll(null, (NameValuePairsHandler)sourceFileHandler, false);

            return toReturn;
        }

        public override IFileHandler RestoreFile(ID<IFileContainer, long> fileId, string pathToRestoreFrom, ID<IUserOrGroup, Guid> userId)
        {
            IUser user = FileHandlerFactoryLocator.UserManagerHandler.GetUserNoException(userId);

            NameValuePairsHandler toReturn = CreateFile(FileSystem.GetFullPath(fileId));

            using (TextReader tr = File.OpenText(pathToRestoreFrom))
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

                        toReturn.Set(user, name, value);
                    }
                }
            }

            return toReturn;
        }
    }
}