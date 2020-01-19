using System;
using System.IO;
using System.Threading;

using GZipArchiver;

namespace GZipArchiver.Readers
{
    internal abstract class BaseReader
    {
        protected volatile int _nextBlockId;
        protected volatile bool _done;

        public int BlocksRead
        {
            get
            {
                return _nextBlockId;
            }
        }

        public bool Done
        {
            get
            {
                return _done;
            }
        }

        public void StartRead(FileInfo inFile, BlockQueue inputQueue)
        {
            Thread innerThread = new Thread(new ParameterizedThreadStart(StartReadInner));
            innerThread.IsBackground = true;
            innerThread.Start(Tuple.Create<FileInfo, BlockQueue>(inFile, inputQueue));
        }

        private void StartReadInner(object param)
        {
            try
            {
                var tuple = param as Tuple<FileInfo, BlockQueue>;
                FileInfo inFile = tuple.Item1;
                BlockQueue inputQueue = tuple.Item2;

                using (FileStream inFileStream = inFile.OpenRead())
                {
                    ReadFileStream(inFileStream, inputQueue);
                    _done = true;
                }
            }
            catch(Exception ex)
            {
                string msg = string.Format("'{0}' exception: '{1}'", this.GetType().Name, ex.ToString());
                Logger.TraceError(msg);
            }
        }

        protected abstract void ReadFileStream(FileStream inFileStream, BlockQueue inputQueue);
    }

    internal sealed class RawReader : BaseReader
    {
        protected override void ReadFileStream(FileStream inFileStream, BlockQueue inputQueue)
        {
            if (inFileStream == null)
                throw new ArgumentNullException("inFileStream");
            if (inputQueue == null)
                throw new ArgumentNullException("inputQueue");

            while (inFileStream.Position < inFileStream.Length)
            {
                int bytesToRead = (int)Math.Min(Constants.BlockSizeBytes, inFileStream.Length - inFileStream.Position);
                byte[] rawBuffer = new byte[bytesToRead];
                inFileStream.Read(rawBuffer, 0, rawBuffer.Length);

                inputQueue.EnqueueBlock(new Block(_nextBlockId, rawBuffer, rawBuffer.Length, 0));
                _nextBlockId++;

                ProgressIndicator.ShowPercentProgress("Compressing... ", Math.Min(inFileStream.Position, inFileStream.Length - 1), inFileStream.Length);
            }
        }
    }

    internal sealed class CompressedReader : BaseReader
    {
        protected override void ReadFileStream(FileStream inFileStream, BlockQueue inputQueue)
        {
            if (inFileStream == null)
                throw new ArgumentNullException("inFileStream");
            if (inputQueue == null)
                throw new ArgumentNullException("inputQueue");

            while (inFileStream.Position < inFileStream.Length)
            {
                int rawSize, compressedSize;
                inFileStream.ReadBlockHeader(out rawSize, out compressedSize);
                byte[] compressedBuffer = new byte[compressedSize];
                inFileStream.Read(compressedBuffer, 0, compressedBuffer.Length);

                inputQueue.EnqueueBlock(new Block(_nextBlockId, compressedBuffer, rawSize, compressedBuffer.Length));
                _nextBlockId++;

                ProgressIndicator.ShowPercentProgress("Decompressing... ", Math.Min(inFileStream.Position, inFileStream.Length - 1), inFileStream.Length);
            }
        }
    }
}
