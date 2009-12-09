// Downloaded from http://www.codeproject.com/KB/cs/MIME_De_Encode_in_C_.aspx
// Original source in Dependancies/MIME.zip

using System;
using System.Collections;

namespace ObjectCloud.Common.MIME
{
	/// <summary>
	/// 
	/// </summary>
	public class MimeCodeManager
	{
		private MimeCodeManager()
		{
			// 
			// TODO: Add constructor logic here
			//
			InitialCode();
		}

		private static Hashtable codeHT = new Hashtable();

		private static readonly MimeCodeManager instance = new MimeCodeManager();

		public static MimeCodeManager Instance
		{
			get{ return instance;}
		}

		private void InitialCode()
		{
			MimeCode aFieldCode = new MimeFieldCodeBase();
			SetCode("Subject", aFieldCode);
			SetCode("Comments", aFieldCode);
			SetCode("Content-Description", aFieldCode);

			aFieldCode = new MimeFieldCodeAddress();
			SetCode("From", aFieldCode);
			SetCode("To", aFieldCode);
			SetCode("Resent-To", aFieldCode);
			SetCode("Cc", aFieldCode);
			SetCode("Resent-Cc", aFieldCode);
			SetCode("Bcc", aFieldCode);
			SetCode("Resent-Bcc", aFieldCode);
			SetCode("Reply-To", aFieldCode);
			SetCode("Resent-Reply-To", aFieldCode);
			
			aFieldCode = new MimeFieldCodeParameter();
			SetCode("Content-Type", aFieldCode);
			SetCode("Content-Disposition", aFieldCode);

			MimeCode aCode = new MimeCode();
			SetCode("7bit", aCode);
			
			SetCode("8bit", aCode);

			aCode = new MimeCodeBase64();
			SetCode("base64", aCode);

			aCode = new MimeCodeQP();
			SetCode("quoted-printable", aCode);

		}

		public void SetCode(string name, MimeCode code)
		{
			codeHT.Add(name.ToLower(), code);
		}

		public MimeCode GetCode(string name)
		{
			return (MimeCode)codeHT[name.ToLower()];
		}
	}
}
