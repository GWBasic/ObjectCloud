// Downloaded from http://www.codeproject.com/KB/cs/MIME_De_Encode_in_C_.aspx
// Original source in Dependancies/MIME.zip

using System;
using System.Text;
using System.Collections;

namespace ObjectCloud.Common.MIME
{
	/// <summary>
	/// 
	/// </summary>
	public class MimeMessage : MIME.MimeBody
	{
		public MimeMessage()
		{
			// 
			// TODO: Add constructor logic here
			//
		}

		// set/get RFC 822 message header fields
		public void SetFrom(string from, string charset)
		{
			SetFieldValue("From", from, charset);
		}
		public string GetFrom()
		{
			return GetFieldValue("From");
		}

		public void SetTo(string to, string charset)
		{
			SetFieldValue("To", to, charset);
		}
		public string GetTo()
		{
			return GetFieldValue("To");
		}

		public void SetCC(string cc, string charset)
		{
			SetFieldValue("CC", cc, charset);
		}
		public string GetCC()
		{
			return GetFieldValue("CC");
		}

		public void SetBCC(string bcc, string charset)
		{
			SetFieldValue("BCC", bcc, charset);
		}
		public string GetBCC()
		{
			return GetFieldValue("BCC");
		}

		public void SetSubject(string subject, string charset)
		{
			SetFieldValue("Subject", subject, charset);
		}
		public string GetSubject()
		{
			return GetFieldValue("Subject");
		}

		public void SetDate(string date, string charset)
		{
			SetFieldValue("Date", date, charset);
		}
		public void SetDate()
		{
			string dt = DateTime.Now.ToString("r",System.Globalization.DateTimeFormatInfo.InvariantInfo);
			dt = dt.Replace("GMT",DateTime.Now.ToString("zz",System.Globalization.DateTimeFormatInfo.InvariantInfo)+"00");
			SetFieldValue("Date",dt , null);
		}
		public string GetDate()
		{
			return GetFieldValue("Date");
		}

		public void Setversion()
		{
			SetFieldValue(MimeConst.MimeVersion, "1.0", null);
		}

	}
}
