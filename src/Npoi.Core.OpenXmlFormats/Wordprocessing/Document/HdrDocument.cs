﻿using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Npoi.Core.OpenXmlFormats.Wordprocessing
{
    public class HdrDocument
    {
        private CT_Hdr hdr = null;

        public HdrDocument()
        {
            hdr = new CT_Hdr();
        }

        public static HdrDocument Parse(XDocument doc, XmlNamespaceManager namespaceMgr)
        {
            CT_Hdr obj = CT_Hdr.Parse(doc.Document.Root, namespaceMgr);
            return new HdrDocument(obj);
        }

        public void Save(Stream stream)
        {
            using (StreamWriter sw = new StreamWriter(stream))
            {
                hdr.Write(sw);
            }
        }

        public HdrDocument(CT_Hdr hdr)
        {
            this.hdr = hdr;
        }

        public CT_Hdr Hdr
        {
            get
            {
                return this.hdr;
            }
        }

        public void SetHdr(CT_Hdr hdr)
        {
            this.hdr = hdr;
        }
    }
}