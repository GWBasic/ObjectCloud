using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ObjectCloud.Common
{
    /// <summary>
    /// Functions that help with working with streams
    /// </summary>
    public static class StreamFunctions
    {
        /// <summary>
        /// Reads all the bytes in a stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(Stream stream)
        {
            List<byte[]> dataRecieved = new List<byte[]>();
            byte[] buffer = new byte[0x10000];
            int bytesRead;
            int totalBytesRead = 0;

            do
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    byte[] copy = new byte[bytesRead];
                    Array.Copy(buffer, copy, bytesRead);

                    dataRecieved.Add(copy);

                    totalBytesRead += bytesRead;
                }
            }
            while (bytesRead > 0);

            byte[] toReturn = new byte[totalBytesRead];
            int bytesCopied = 0;
            foreach (byte[] copy in dataRecieved)
            {
                Array.Copy(copy, 0, toReturn, bytesCopied, copy.Length);
                bytesCopied += copy.Length;
            }

            return toReturn;
        }
    }
}
