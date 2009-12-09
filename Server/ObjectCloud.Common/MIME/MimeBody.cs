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
	public class MimeBody : MIME.MimeHeader
	{
		protected MimeBody()
		{
			// 
			// TODO: Add constructor logic here
			//
		}

		private ArrayList ChildList = null;
		private string mContent;

		//store all mime part to a string buffer
		public void StoreBody(StringBuilder sb)
		{
			if(sb == null)
				throw new ArgumentNullException();

			StoreHead(sb);
			sb.Append(mContent);

			if(MimeType.MediaType.MEDIA_MULTIPART == GetMediaType())
			{
				string boundary = GetBoundary();
				for(int i=0; i<ChildList.Count; i++)
				{
					sb.AppendFormat("--{0}\r\n", boundary);
					MimeBody aMimeBody = (MimeBody)ChildList[i];
					aMimeBody.StoreBody(sb);
				}
				sb.AppendFormat("--{0}--\r\n", boundary);
			}
		}

		//load all mime part from a string buffer
		public void LoadBody(string strData)
		{
			if(strData == null)
				throw new ArgumentNullException();

			int headend = strData.IndexOf("\r\n\r\n");
			LoadHead(strData.Substring(0,headend+2));

			int bodystart = headend + 4;
			if(MimeType.MediaType.MEDIA_MULTIPART == GetMediaType())
			{
				string boundary = GetBoundary();
				if(null == boundary)
					return;
				else
				{
					string strBstart = "--" + boundary;
					string strBend = strBstart + "--";

					int nBstart = strData.IndexOf(strBstart, bodystart);
					if(nBstart == -1) return;
					int nBend = strData.IndexOf(strBend, bodystart);
					if(nBend == -1) nBend = strData.Length;

					if(nBstart > bodystart)
					{
						mContent = strData.Substring(bodystart, nBstart - bodystart);
					}

					while(nBstart < nBend)
					{
						nBstart = nBstart + strBstart.Length + 2;
						int nBstart2 = strData.IndexOf(strBstart, nBstart);
						if(nBstart2 != -1)
						{
							MimeBody ChildBody = CreatePart();
							ChildBody.LoadBody(strData.Substring(nBstart, nBstart2 - nBstart));
						}
						nBstart = nBstart2;
					}
				}
			}
			else
			{
				mContent = strData.Substring(bodystart, strData.Length - bodystart);
			}
		}

		//create a child mime part
		public MimeBody CreatePart(MimeBody parent)
		{
			if(ChildList == null) ChildList = new ArrayList();

			MimeBody aMimeBody = new MimeBody();
			if(parent != null)
			{
				int index = ChildList.IndexOf(parent);
				if(index != -1)
				{
					ChildList.Insert(index+1, aMimeBody);
					return aMimeBody;
				}
			}

			ChildList.Add(aMimeBody);
			return aMimeBody;
		}

		public MimeBody CreatePart()
		{
			return CreatePart(null);
		}
		
		// get a list of mime part
		public void GetBodyPartList(ArrayList BodyList)
		{			
			if(BodyList == null)
				throw new ArgumentNullException();

			if(GetMediaType() != MimeType.MediaType.MEDIA_MULTIPART)
			{
				BodyList.Add(this);
			}
			else
			{
				BodyList.Add(this);
				for(int i=0; i<ChildList.Count; i++)
				{
					MimeBody aMimeBody = (MimeBody)ChildList[i];
					aMimeBody.GetBodyPartList(BodyList);
				}
			}
		}

		//operation for text or message media
		public bool IsText()
		{
			return GetMediaType() == MimeType.MediaType.MEDIA_TEXT;
		}

		public void SetText(string text)
		{
			if(text == null)
				throw new ArgumentNullException();

			string encoding = GetTransferEncoding();
			if(encoding == null)
			{
				encoding = MimeConst.Encoding7Bit;
				SetTransferEncoding(encoding);
			}
			MimeCode aCode = MimeCodeManager.Instance.GetCode(encoding);
			aCode.Charset = GetCharset();
			mContent = aCode.EncodeFromString(text) + "\r\n";

			SetContentType("text/plain");
			SetCharset(aCode.Charset);

		}

		public string GetText()
		{
			string encoding = GetTransferEncoding();
			if(encoding == null)
			{
				encoding = MimeConst.Encoding7Bit;
			}
			MimeCode aCode = MimeCodeManager.Instance.GetCode(encoding);
			aCode.Charset = GetCharset();
			return aCode.DecodeToString(mContent);
		}

		//operations for message media
		public bool IsMessage()
		{
			return GetMediaType() == MimeType.MediaType.MEDIA_MESSAGE;
		}

		public void GetMessage(MimeMessage aMimeMessage)
		{
			if(aMimeMessage == null)
				throw new ArgumentNullException();

			aMimeMessage.LoadBody(mContent);
		}

		public void SetMessage(MimeMessage aMimeMessage)
		{
			StringBuilder sb = new StringBuilder();
			aMimeMessage.StoreBody(sb);
			mContent = sb.ToString();
			SetContentType("message/rfc822");
		}

		// operations for 'image/audio/vedio/application' (attachment) media
		public bool IsAttachment()
		{
			return GetName()!=null;
		}

		public void ReadFromFile(string filePathName)
		{
			if(filePathName == null)
				throw new ArgumentNullException();

			StreamReader sr = new StreamReader(filePathName);
			Stream bs = sr.BaseStream;

			byte[] b = new byte[bs.Length];
			bs.Read(b,0,(int)bs.Length);
            
			string encoding = GetTransferEncoding();
			if(encoding == null)
			{
				encoding = MimeConst.EncodingBase64;
				SetTransferEncoding(encoding);
			}

			MimeCode aCode = MimeCodeManager.Instance.GetCode(encoding);
			aCode.Charset = GetCharset();
			mContent = aCode.EncodeFromBytes(b) + "\r\n";

			string filename;
			int index = filePathName.LastIndexOf('\\');
			if(index != -1)
			{
				filename = filePathName.Substring(index+1, filePathName.Length - index - 1);
			}
			else
			{
				filename = filePathName;
			}
			SetName(filename);

			sr.Close();
			sr=null;
		}

		public void WriteToFile(string filePathName)
		{
			if(filePathName == null)
				throw new ArgumentNullException();

			StreamWriter sw = new StreamWriter(filePathName);
			Stream bs = sw.BaseStream;

			string encoding = GetTransferEncoding();
			if(encoding == null)
			{
				encoding = MimeConst.Encoding7Bit;
			}

			MimeCode aCode = MimeCodeManager.Instance.GetCode(encoding);
			aCode.Charset = GetCharset();
			byte[] b = aCode.DecodeToBytes(mContent);

			bs.Write(b,0,b.Length);

			sw.Close();
			sw=null;
		}

		public bool IsMultiPart()
		{
			return GetMediaType() == MimeType.MediaType.MEDIA_MULTIPART;
		}

		public void DeleteAllPart()
		{
			ChildList.RemoveRange(0, ChildList.Count);
		}

		public void ErasePart(MimeBody ChildPart)
		{
			ChildList.Remove(ChildPart);
		}

	}
}
