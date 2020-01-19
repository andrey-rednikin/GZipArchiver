using System;
using System.IO;
using System.Threading;

using GZipArchiver;

namespace GZipArchiver.Writers
{
    internal abstract class BaseWriter
    {
        private const int CancelTimeoutSec = 10;

        protected ManualResetEvent _blockWrittenEvent = new ManualResetEvent(false);
        protected volatile int _blocksWritten;
        protected volatile bool _cancelled;
        private volatile FileInfo _outFile;
        private Thread _innerThread;

        public ManualResetEvent BlockWrittenEvent
        {
            get
            {
                return _blockWrittenEvent;
            }
        }

        public int BlocksWritten
        {
            get
            {
                return _blocksWritten;
            }
        }

        public void StartWrite(FileInfo outFile, BlockQueue outputQueue)
        {
            _outFile = outFile;
            _innerThread = new Thread(new ParameterizedThreadStart(StartWriteInner));
            _innerThread.IsBackground = true;
            _innerThread.Start(Tuple.Create<FileInfo, BlockQueue>(outFile, outputQueue));
        }

        public void Cancel()
        {
            _cancelled = true;

            try
            {
                _innerThread.Join(TimeSpan.FromSeconds(CancelTimeoutSec));
            }
            catch { }

            try
            {
                _outFile.Delete();
            }
            catch { }
        }

        private void StartWriteInner(object param)
        {
            try
            {
                var tuple = param as Tuple<FileInfo, BlockQueue>;
                FileInfo outFile = tuple.Item1;
                BlockQueue outputQueue = tuple.Item2;

                using (FileStream outFileStream = outFile.Create())
                    WriteToFileStream(outFileStream, outputQueue);
            }
            catch (Exception ex)
            {
                string msg = string.Format("'{0}' exception: '{1}'", this.GetType().Name, ex.ToString());
                Logger.TraceError(msg);
            }
        }

        protected abstract void WriteToFileStream(FileStream outFileStream, BlockQueue outputQueue);
    }

    internal sealed class CompressedWriter : BaseWriter
    {
        protected override void WriteToFileStream(FileStream outFileStream, BlockQueue outputQueue)
        {
            while (!_cancelled)
            {
                Block compressedBlock = outputQueue.DequeueBlock();
                outFileStream.WriteBlockHeader(compressedBlock.RawSize, compressedBlock.CompressedSize);
                outFileStream.Write(compressedBlock.Content, 0, compressedBlock.Content.Length);

                _blocksWritten++;
                _blockWrittenEvent.Set();
            }
        }
    }

    internal sealed class RawWriter : BaseWriter
    {
        protected override void WriteToFileStream(FileStream outFileStream, BlockQueue outputQueue)
        {
            while (!_cancelled)
            {
                Block rawBlock = outputQueue.DequeueBlock();
                outFileStream.Write(rawBlock.Content, 0, rawBlock.Content.Length);

                _blocksWritten++;
                _blockWrittenEvent.Set();
            }
        }
    }
}
