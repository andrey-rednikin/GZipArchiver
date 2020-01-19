using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

using GZipArchiver.Readers;
using GZipArchiver.Writers;
using GZipArchiver.Workers;

namespace GZipArchiver
{
    class Program
    {
        private static volatile bool _canceled;
        private static BaseWriter _writer;

        public static bool Canceled
        {
            get
            {
                return _canceled;
            }
        }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPress;
            var stopwatch = new Stopwatch();

            try
            {
                Console.WriteLine();

                CompressionMode compressMode;
                FileInfo inFile, outFile;
                ParseCmdLineParams(args, out inFile, out outFile, out compressMode);

                WorkerPool workerPool = new WorkerPool();
                BaseReader reader = null;

                stopwatch.Start();

                Console.WriteLine("Start");
                Console.WriteLine();

                if (compressMode == CompressionMode.Compress)
                {
                    _writer = new CompressedWriter();
                    _writer.StartWrite(outFile, workerPool.OutQueue);
                    workerPool.Start<CompressWorker>();
                    reader = new RawReader();
                    reader.StartRead(inFile, workerPool.InQueue);
                }
                else if (compressMode == CompressionMode.Decompress)
                {
                    _writer = new RawWriter();
                    _writer.StartWrite(outFile, workerPool.OutQueue);
                    workerPool.Start<DecompressWorker>();
                    reader = new CompressedReader();
                    reader.StartRead(inFile, workerPool.InQueue);
                }

                while (!_canceled && !Logger.ErrorFixed)
                {
                    _writer.BlockWrittenEvent.WaitOne(TimeSpan.FromSeconds(1));

                    if (reader.Done && reader.BlocksRead == _writer.BlocksWritten)
                        break;
                    else if (_writer.BlockWrittenEvent.WaitOne(0))
                        _writer.BlockWrittenEvent.Reset();
                }
            }
            catch (Exception ex)
            {
                Logger.TraceError(string.Format("Global error: {0}", ex.ToString()));
            }

            if (_canceled || Logger.ErrorFixed)
            {
                Logger.DisableOutput();
                if (_writer != null)
                    _writer.Cancel();
            }

            PrintFooter(stopwatch);

            Environment.Exit(0);
        }

        private static void ParseCmdLineParams(string[] args, out FileInfo inFile, out FileInfo outFile, out CompressionMode compressMode)
        {
            if (args.Length == 0 || 3 < args.Length)
                throw new ArgumentException("Usage: GZipArchiver <compress/decompress> [input_file_path] [output_file_path]");

            string command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "compress":
                    compressMode = CompressionMode.Compress;
                    break;
                case "decompress":
                    compressMode = CompressionMode.Decompress;
                    break;
                default:
                    throw new ArgumentException(string.Format("Unknown command: {0}", args[0]));
            }

            if (string.IsNullOrWhiteSpace(args[1]))
                throw new Exception("Empty input file path");

            inFile = new FileInfo(args[1]);
            if (!inFile.Exists)
                throw new ArgumentException("Input file not found");
            else if (inFile.Length < 1)
                throw new ArgumentException("Input file is empty");

            if (string.IsNullOrWhiteSpace(args[2]))
                throw new ArgumentException("Empty output file path");

            outFile = new FileInfo(args[2]);
            if (!outFile.Directory.Exists)
                throw new ArgumentException("Output file directory does not exist");
            else if (outFile.Exists)
                throw new ArgumentException("Output file already exists");

            if (compressMode == CompressionMode.Compress
                && string.Compare(outFile.Extension, Constants.ArchivedFileExt, StringComparison.OrdinalIgnoreCase) != 0)
            {
                throw new ArgumentException(string.Format("When compressing, the output file should have '{0}' extension", Constants.ArchivedFileExt));
            }
            else if (compressMode == CompressionMode.Decompress
                && string.Compare(inFile.Extension, Constants.ArchivedFileExt, StringComparison.OrdinalIgnoreCase) != 0)
            {
                throw new ArgumentException(string.Format("When decompressing, the input file should have '{0}' extension", Constants.ArchivedFileExt));
            }

            if (string.Compare(inFile.FullName, outFile.FullName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new ArgumentException("Input and oputput file paths should be different");
            }
        }

        private static void PrintFooter(Stopwatch sw)
        {
            string duration = string.Empty;
            if (sw != null && sw.IsRunning)
            {
                sw.Stop();
                duration = string.Format(" Duration: {0} sec", sw.Elapsed.TotalSeconds);
            }

            Console.WriteLine();
            Console.WriteLine(string.Format("Finish. {0}", duration));
            //Console.ReadLine();
        }

        #region Event handlers

        static void CancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            if (args.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                args.Cancel = true;
                _canceled = true;

                Console.WriteLine();
                Console.WriteLine("Cancelling...");
            }
        }

        #endregion
    }

    internal static class ProgressIndicator
    {
        public static void ShowPercentProgress(string message, long currElementIndex, long totalElementCount)
        {
            if (Program.Canceled || Logger.ErrorFixed)
                return;

            if (currElementIndex < 0 || currElementIndex >= totalElementCount)
                throw new InvalidOperationException("Element out of range");

            int percent = (int)(100 * ((currElementIndex + 1) / (double)totalElementCount));
            Console.Write("\r{0}{1}% complete", message, percent);
            if (currElementIndex == totalElementCount - 1)
                Console.WriteLine(Environment.NewLine);
        }
    }
}
