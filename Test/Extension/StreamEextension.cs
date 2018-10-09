using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Test.Extension
{
    public static class StreamExtensions
    {
        public static void CopyTo(this Stream src, Stream dest)
        {
            byte[] buffer = new byte[1024 * 1024];
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                dest.Write(buffer, 0, read);
            }
        }
    }
}
