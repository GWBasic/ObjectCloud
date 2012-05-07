using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ObjectCloud.Common.StreamEx
{
	public static class StreamExtensions
	{
		public static T Read<T>(this Stream stream)
            where T : struct
        {
            byte[] buffer = new byte[Marshal.SizeOf(typeof(T))];
			stream.Read(buffer, 0, buffer.Length);

            IntPtr ptr = Marshal.AllocHGlobal(buffer.Length);

            try
            {
                Marshal.Copy(buffer, 0x0, ptr, buffer.Length);
                T toReturn = (T)Marshal.PtrToStructure(ptr, typeof(T));
                
                return toReturn;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
		
		public static void Write<T>(this Stream stream, T val)
            where T : struct
		{
            byte[] buffer = new byte[Marshal.SizeOf(typeof(T))];
            IntPtr ptr = Marshal.AllocHGlobal(buffer.Length);

            try
            {
				Marshal.StructureToPtr(val, ptr, false);
				Marshal.Copy(ptr, buffer, 0, buffer.Length);
				
				stream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
		}
		
		/*public static T ReadEnum<T>(this Stream stream)
			where T : struct
		{
			var t = Enum.GetUnderlyingType(typeof(T));
			
			if (t == typeof(int))
				return (T)stream.Read<int>();
			
			else if (t == typeof(long))
				return (T)stream.Read<long>();
			
			else if (t == typeof(uint))
				return (T)stream.Read<uint>();
			
			else if (t == typeof(ulong))
				return (T)stream.Read<ulong>();
			
			else if (t == typeof(byte))
				return (T)stream.Read<byte>();
			
			return stream.Read<T>();
		}

		public static T WriteEnum<T>(this Stream stream, T val)
			where T : struct
		{
			var t = Enum.GetUnderlyingType(typeof(T));

			if (t == typeof(int))
				stream.Write((int)val);
			
			else if (t == typeof(long))
				stream.Write((long)val);
			
			else if (t == typeof(uint))
				stream.Write((uint)val);
			
			else if (t == typeof(ulong))
				stream.Write((ulong)val);
			
			else if (t == typeof(byte))
				stream.Write((byte)val);
			
			else			
				stream.Write(val);
		}*/

		public static T? ReadNullable<T>(this Stream stream)
            where T : struct
        {
			if (stream.Read<bool>())
				return stream.Read<T>();
			
			return null;
        }
		
		public static void WriteNullable<T>(this Stream stream, T? val)
            where T : struct
		{
			if (null != val)
			{
				stream.Write(true);
				stream.Write(val.Value);
			}
			else
				stream.Write(false);
		}
		
		public static DateTime ReadDateTime(this Stream stream)
		{
			return new DateTime(stream.Read<long>());
		}
		
		public static void Write(this Stream stream, DateTime dateTime)
		{
			stream.Write(dateTime.Ticks);
		}
		
		public static string ReadString(this Stream stream)
		{
			var length = stream.Read<int>();
			
			if (length < 0)
				return null;
			
			var buffer = new byte[length];
			
			stream.Read(buffer, 0, buffer.Length);
			
			return Encoding.UTF8.GetString(buffer);
		}
		
		public static void WriteString(this Stream stream, string val)
		{
			if (null == val)
			{
				stream.Write(int.MinValue);
				return;
			}
			
			var buffer = Encoding.UTF8.GetBytes(val);
			
			stream.Write(buffer.Length);
			
			stream.Write(buffer, 0, buffer.Length);
		}
	}
}

