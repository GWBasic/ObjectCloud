// Downloaded from http://www.codeproject.com/KB/cs/MIME_De_Encode_in_C_.aspx
// Original source in Dependancies/MIME.zip

using System;
using System.IO;
using System.Text;
using System.Collections;

namespace ObjectCloud.Common.MIME
{
	/// <summary>
	/// 
	/// </summary>
	public class MimeHeader
	{
		protected MimeHeader()
		{
			// 
			// TODO: Add constructor logic here
			//
		}

		protected ArrayList m_listFields = new ArrayList();

		//store a head content to a string buffer
		protected virtual void StoreHead(StringBuilder sb)
		{
			if(sb == null)
				throw new ArgumentNullException();

			for(int i=0; i<m_listFields.Count; i++)
			{
				MimeField aMimeField = (MimeField)m_listFields[i];
				aMimeField.Store(sb);
			}
			sb.Append("\r\n");
		}

		//load a head content from a string buffer
		protected void LoadHead(string strData)
		{
			if(strData == null)
				throw new ArgumentNullException();
			string field="";
			string line;
			StringReader sr = new StringReader(strData);
			try
			{
				line = sr.ReadLine();
				field = line + "\r\n";
				while(line != null)
				{
					line = sr.ReadLine();

                    bool endLine = false;

                    if (line != null)
                        if (line.Length > 0)
                            if (line[0] == ' ' || line[0] == '\t')
                                endLine = true;

                    if (endLine)
					{
						field += line + "\r\n";
					}
					else
					{
						MimeField aMimeField = new MimeField();
						aMimeField.LoadField(field);
						m_listFields.Add(aMimeField);
						field = line + "\r\n";
					}
				}
			}
			finally
			{
				sr.Close();
				sr = null;
			}
		}

		//find a field according to its field name
		protected MimeField FindField(string pszFieldName)
		{
			for(int i=0; i<m_listFields.Count; i++)
			{
				MimeField aMimeField = (MimeField)m_listFields[i];
				if(aMimeField.GetName().ToLower() == pszFieldName.ToLower())
				{
					return aMimeField;
				}
			}
			return null;
		}

		public MimeField GetField(string pszFieldName)
		{
			MimeField aMimeField = FindField(pszFieldName);
			return aMimeField != null?aMimeField:null;
		}

		public void SetFieldValue(string pszFieldName, string pszFieldValue, string pszFieldCharset)
		{
			MimeField aMimeField = GetField(pszFieldName);
			if(aMimeField != null)
			{
				aMimeField.SetValue(pszFieldValue);
				if(pszFieldCharset != null) aMimeField.SetCharset(pszFieldCharset);
			}
			else
			{
				aMimeField = new MimeField();
				aMimeField.SetName(pszFieldName);
				aMimeField.SetValue(pszFieldValue);
				if(pszFieldCharset != null) aMimeField.SetCharset(pszFieldCharset);
				m_listFields.Add(aMimeField);
			}
		}

		public string GetFieldValue(string pszFieldName)
		{
			MimeField aMimeField = GetField(pszFieldName);
			return aMimeField!=null?aMimeField.GetValue():null;
		}

		// Content-Type: mediatype/subtype
		public void SetContentType(string pszValue, string pszCharset)
		{
			SetFieldValue(MimeConst.ContentType, pszValue, pszCharset);
		}
		public void SetContentType(string pszValue)
		{
			SetContentType(pszValue, null);
		}

		public string GetContentType()
		{
			return GetFieldValue(MimeConst.ContentType);
		}

		// Content-Type: text/...; charset=...
		public void SetCharset(string pszCharset)
		{
			MimeField aMimeField = GetField(MimeConst.ContentType);
			if(aMimeField == null)
			{
				aMimeField = new MimeField();
				aMimeField.SetName(MimeConst.ContentType);
				aMimeField.SetValue("text/plain");
				aMimeField.SetParameter(MimeConst.Charset, "\""+pszCharset+"\"");
				m_listFields.Add(aMimeField);
			}
			else
			{
				aMimeField.SetParameter(MimeConst.Charset, "\""+pszCharset+"\"");
			}
		}

		public string GetContentMainType()
		{
			string mainType;
			string contentType = GetContentType();
			if(null != contentType)
			{
				int slashIndex = contentType.IndexOf('/', 0);
				if(slashIndex != -1)
				{
					mainType = contentType.Substring(0, slashIndex);
				}
				else
				{
					mainType = contentType;
				}
			}
			else
			{
				mainType = "text";
			}
			return mainType;
		}

		public string GetContentSubType()
		{
			string subType;
			string contentType = GetContentType();
			if(null != contentType)
			{
				int slashIndex = contentType.IndexOf('/', 0);
				if(slashIndex != -1)
				{
					int subTypeEnd = contentType.IndexOf(';', slashIndex+1);
					if(subTypeEnd == -1) subTypeEnd = contentType.IndexOf('\r', slashIndex+1);
					subType = contentType.Substring(slashIndex+1, subTypeEnd-slashIndex-1);
				}
				else
				{
					subType = "";
				}
			}
			else
			{
				subType = "text";
			}
			return subType;
		}

		public MimeType.MediaType GetMediaType()
		{
			string mediaType = GetContentMainType();

			int i=0;
			for( ; MimeType.TypeTable[i]!=null; i++)
			{
				if(mediaType.IndexOf(MimeType.TypeTable[i], 0)!=-1)
				{
					return (MimeType.MediaType)i;
				}
			}
			return (MimeType.MediaType)i;
		}

		public string GetCharset()
		{
			return GetParameter(MimeConst.ContentType, MimeConst.Charset);
		}

		// Content-Type: image/...; name=...
		public void SetName(string pszName)
		{
			//MimeField aMimeField = GetField(pszName);
            MimeField aMimeField = GetField(MimeConst.ContentType);
			if(aMimeField == null)
			{
				aMimeField = new MimeField();
				int lastindex = pszName.LastIndexOf('.');
				string strType = "application/octet-stream";
				string ext = pszName.Substring(lastindex + 1, pszName.Length - lastindex - 1);
				int nIndex = 0;
				while(MimeType.TypeCvtTable[nIndex].nMediaType != MimeType.MediaType.MEDIA_UNKNOWN)
				{
					if(MimeType.TypeCvtTable[nIndex].pszFileExt == ext)
					{
						strType = MimeType.TypeTable[(int)MimeType.TypeCvtTable[nIndex].nMediaType];
						strType += '/';
						strType += MimeType.TypeCvtTable[nIndex].pszSubType;
						break;
					}
					nIndex++;
				}
				aMimeField.SetName(MimeConst.ContentType);
				aMimeField.SetValue(strType);
				aMimeField.SetParameter(MimeConst.Name, "\""+pszName+"\"");
				m_listFields.Add(aMimeField);
			}
			else
			{
				aMimeField.SetParameter(MimeConst.Name, "\""+pszName+"\"");
			}
		}

		public string GetName()
		{
			return GetParameter(MimeConst.ContentType, MimeConst.Name);
		}

		// Content-Type: multipart/...; boundary=...
		public void SetBoundary(string pszBoundary)
		{
			if(pszBoundary == null)
			{
				Random randObj = new Random((int)DateTime.Now.Ticks);
				pszBoundary = "__=_Part_Boundary_"+randObj.Next().ToString()+"_"+randObj.Next().ToString();
			}

			MimeField aMimeField = GetField(MimeConst.ContentType);
			if(aMimeField != null)
			{
				if(aMimeField.GetValue().IndexOf("multipart", 0, 9) == -1)
					aMimeField.SetValue("multipart/mixed");
				aMimeField.SetParameter(MimeConst.Boundary, "\""+pszBoundary+"\"");
			}
			else
			{
				aMimeField = new MimeField();
				aMimeField.SetName(MimeConst.ContentType);
				aMimeField.SetValue("multipart/mixed");
				aMimeField.SetParameter(MimeConst.Boundary, "\""+pszBoundary+"\"");
				m_listFields.Add(aMimeField);
			}
		}

		public string GetBoundary()
		{
			return GetParameter(MimeConst.ContentType, MimeConst.Boundary);
		}

		public bool SetParameter(string pszFieldName, string pszAttr, string pszValue)
		{
			MimeField aMimeField = GetField(pszFieldName);
			if(aMimeField != null)
			{
				aMimeField.SetParameter(pszAttr, pszValue);
				return true;
			}
			return false;
		}

		public string GetParameter(string pszFieldName, string pszAttr)
		{
			MimeField aMimeField = GetField(pszFieldName);
			return aMimeField != null?aMimeField.GetParameter(pszAttr):null;
		}

		public void SetFieldCharset(string pszFieldName, string pszFieldCharset)
		{
			MimeField aMimeField = GetField(pszFieldName);
			if(aMimeField != null)
			{
				aMimeField.SetCharset(pszFieldCharset);
			}
			else
			{
				aMimeField = new MimeField();
				aMimeField.SetCharset(pszFieldCharset);
				m_listFields.Add(aMimeField);
			}
		}

		public string GetFieldCharset(string pszFieldName)
		{
			MimeField aMimeField = GetField(pszFieldName);
			return aMimeField != null?aMimeField.GetCharset():null;
		}

		// Content-Transfer-Encoding: ...
		public void SetTransferEncoding(string pszValue)
		{
			SetFieldValue(MimeConst.TransferEncoding, pszValue, null);
		}

		public string GetTransferEncoding()
		{
			return GetFieldValue(MimeConst.TransferEncoding);
		}

		// Content-Disposition: ...
		public void SetDisposition(string pszValue, string pszCharset)
		{
			SetFieldValue(MimeConst.ContentDisposition, pszValue, pszCharset);
		}

		public string GetDisposition()
		{
			return GetFieldValue(MimeConst.ContentDisposition);
		}

		// Content-Disposition: ...; filename=...
		public string GetFilename()
		{
			return GetParameter(MimeConst.ContentDisposition, MimeConst.Filename);
		}

		public void SetDescription(string pszValue, string pszCharset)
		{
			SetFieldValue(MimeConst.ContentDescription, pszValue, pszCharset);
		}

		public string GetDiscription()
		{
			return GetFieldValue(MimeConst.ContentDescription);
		}
	}
}
