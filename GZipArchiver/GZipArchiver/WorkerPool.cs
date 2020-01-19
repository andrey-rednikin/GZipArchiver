using System;
using System.Collections.Generic;
using System.Threading;

using GZipArchiver.Workers;

namespace GZipArchiver
{
    internal class WorkerPool
    {
        private readonly int WorkersCount;
        private volatile BlockQueue _inQueue;
        private volatile BlockQueue _outQueue;
        private IList<Thread> _threadList;

        public BlockQueue InQueue
        {
            get
            {
                return _inQueue;
            }
        }

        public BlockQueue OutQueue
        {
            get
            {
                return _outQueue;
            }
        }

        public WorkerPool()
        {
            WorkersCount = 2 < Environment.ProcessorCount ? Environment.ProcessorCount - 1 : Environment.ProcessorCount;
            InitializeQueries();
        }

        public void Start<T>() where T : BaseWorker
        {
            if (_threadList != null)
                throw new InvalidOperationException("WorkerPool already started");

            _threadList = new List<Thread>();

            for (int i = 0; i < WorkersCount; i ++)
            {
                BaseWorker newWorker = (BaseWorker)Activator.CreateInstance(typeof(T));
                Thread workerThread = new Thread(new ParameterizedThreadStart(newWorker.DoWork));
                workerThread.IsBackground = true;
                _threadList.Add(workerThread);
            }

            foreach (Thread workerThread in _threadList)
                workerThread.Start(Tuple.Create<BlockQueue, BlockQueue>(_inQueue, _outQueue));
        }

        private void InitializeQueries()
        {
            long roughRamAvailable = (long)(new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory / 4);
            long roughRamDesired = 50 * WorkersCount * Constants.BlockSizeBytes;
            long maxQueueSizeBytes = Math.Min(roughRamAvailable, roughRamDesired);

            _inQueue = new BlockQueue(maxQueueSizeBytes);
            _outQueue = new BlockQueue(maxQueueSizeBytes);
        }
    }
}
