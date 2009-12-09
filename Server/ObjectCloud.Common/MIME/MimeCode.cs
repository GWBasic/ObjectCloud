// Downloaded from http://www.codeproject.com/KB/cs/MIME_De_Encode_in_C_.aspx
// Original source in Dependancies/MIME.zip

using System;

namespace ObjectCloud.Common.MIME
{
	/// <summary>
	/// 
	/// </summary>
	public class MimeCode
	{
		public MimeCode()
		{
			// 
			// TODO: Add constructor logic here
			//
		}

		private string m_charset;

		public string Charset
		{
			get{ return m_charset; }
			set{ this.m_charset = value; }
		}

		public virtual byte[] DecodeToBytes(string s)
		{
			if(s == null)
				throw new ArgumentNullException();

			if(m_charset != null)
			{
				return System.Text.Encoding.GetEncoding(m_charset).GetBytes(s);
			}
			else
			{
				m_charset = System.Text.Encoding.Default.BodyName;
				return System.Text.Encoding.Default.GetBytes(s);
			}
		}

		public virtual string DecodeToString(string s)
		{
			byte[] b = DecodeToBytes(s);
			if(m_charset != null)
			{
				return System.Text.Encoding.GetEncoding(m_charset).GetString(b);
			}
			else
			{
				m_charset = System.Text.Encoding.Default.BodyName;
				return System.Text.Encoding.Default.GetString(b);
			}
		}

		public virtual string EncodeFromBytes(byte[] inArray, int offset, int length)
		{
			if(inArray == null)
				throw new ArgumentNullException();

			if(m_charset != null)
			{
				return System.Text.Encoding.GetEncoding(m_charset).GetString(inArray, offset, length);
			}
			else
			{
				m_charset = System.Text.Encoding.Default.BodyName;
				return System.Text.Encoding.Default.GetString(inArray, offset, length);
			}
		}
		
		public string EncodeFromBytes(byte[] inArray)
		{
			return EncodeFromBytes(inArray, 0, inArray.Length);
		}

		public virtual string EncodeFromString(string s)
		{
			if(s == null)
				throw new ArgumentNullException();

			byte[] inArray;
			if(m_charset != null)
			{
				inArray = System.Text.Encoding.GetEncoding(m_charset).GetBytes(s);
			}
			else
			{
				m_charset = System.Text.Encoding.Default.BodyName;
				inArray = System.Text.Encoding.Default.GetBytes(s);
			}
			return EncodeFromBytes(inArray);
		}
	}
}
