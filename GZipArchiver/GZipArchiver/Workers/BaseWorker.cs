using System;
using System.IO;
using System.IO.Compression;

using GZipArchiver;

namespace GZipArchiver.Workers
{
    internal abstract class BaseWorker
    {
        public void DoWork(object param)
        {
            int blockId = -1;
            try
            {
                var tuple = param as Tuple<BlockQueue, BlockQueue>;
                BlockQueue inQueue = tuple.Item1;
                BlockQueue outQueue = tuple.Item2;

                while (true)
                {
                    Block inputBlock = inQueue.DequeueBlock();
                    blockId = inputBlock.Id;
                    Block outputBlock = ApplyAlgorythm(inputBlock);
                    outQueue.EnqueueBlock(outputBlock);
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format("'{0}' blockId: '{1}' exception: '{2}'", this.GetType().Name, blockId, ex.ToString());
                Logger.TraceError(msg);
            }
        }

        protected abstract Block ApplyAlgorythm(Block inputBlock);
    }

    internal sealed class CompressWorker : BaseWorker
    {
        protected override Block ApplyAlgorythm(Block inputBlock)
        {
            if (inputBlock == null)
                throw new ArgumentNullException("inputBlock");

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (GZipStream gzStream = new GZipStream(memoryStream, CompressionMode.Compress))
                    gzStream.Write(inputBlock.Content, 0, inputBlock.RawSize);

                byte[] compressedData = memoryStream.ToArray();
                return new Block(inputBlock.Id, compressedData, inputBlock.RawSize, compressedData.Length);
            }
        }
    }

    internal sealed class DecompressWorker : BaseWorker
    {
        protected override Block ApplyAlgorythm(Block inputBlock)
        {
            if (inputBlock == null)
                throw new ArgumentNullException("inputBlock");

            using (MemoryStream memoryStream = new MemoryStream(inputBlock.Content))
            using (GZipStream gzStream = new GZipStream(memoryStream, CompressionMode.Decompress))
            {
                byte[] decompressedData = new byte[inputBlock.RawSize];
                gzStream.Read(decompressedData, 0, decompressedData.Length);

                return new Block(inputBlock.Id, decompressedData, decompressedData.Length, 0);
            }
        }
    }
}
