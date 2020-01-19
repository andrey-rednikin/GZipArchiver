using System;
using System.IO;

namespace GZipArchiver
{
    internal static class BlockHeader
    {
        private const int HeaderSize = 8;

        public static void WriteBlockHeader(this FileStream fs, int rawSize, int compressedSize)
        {
            byte[] blockHeader = new byte[HeaderSize];
            BitConverter.GetBytes(rawSize).CopyTo(blockHeader, 0);
            BitConverter.GetBytes(compressedSize).CopyTo(blockHeader, 4);
            fs.Write(blockHeader, 0, blockHeader.Length);
        }

        public static void ReadBlockHeader(this FileStream fs, out int rawSize, out int compressedSize)
        {
            if (fs.Length - fs.Position <= HeaderSize)
            {
                string msg = "'BlockHeader' exception: block header corrupted";
                Logger.TraceError(msg);
                throw new InvalidOperationException(msg);
            }

            byte[] blockHeader = new byte[HeaderSize];
            fs.Read(blockHeader, 0, blockHeader.Length);
            rawSize = BitConverter.ToInt32(blockHeader, 0);
            compressedSize = BitConverter.ToInt32(blockHeader, 4);
        }
    }
}
