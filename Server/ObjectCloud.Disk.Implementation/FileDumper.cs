using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using ObjectCloud.Common;
using ObjectCloud.Interfaces.Disk;
using ObjectCloud.Interfaces.Security;

namespace ObjectCloud.Disk.Implementation
{
    public class FileDumper : IFileDumper
    {
        public void DoDump(IFileContainer fileContainer, ID<IUserOrGroup, Guid> userId, Stream stream)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "\t";
            settings.NewLineChars = "\n";
            settings.NewLineHandling = NewLineHandling.Entitize;

            XmlWriter xmlWriter = XmlWriter.Create(stream, settings);
            xmlWriter.WriteStartDocument();

            xmlWriter.WriteStartElement("File");

            xmlWriter.WriteAttributeString("Name", fileContainer.Filename);

            if (null != fileContainer.OwnerId)
                xmlWriter.WriteAttributeString("OwnerId", fileContainer.OwnerId.Value.ToString());

            xmlWriter.WriteAttributeString("TypeId", fileContainer.TypeId);

            xmlWriter.WriteStartElement("Contents");

            fileContainer.FileHandler.Dump(xmlWriter, userId);

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndElement();

            xmlWriter.WriteEndDocument();

            xmlWriter.Flush();
        }
    }
}
