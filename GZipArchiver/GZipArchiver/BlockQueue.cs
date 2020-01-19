using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipArchiver
{
    internal class Block
    {
        private byte[] _content;
        private int _id;
        private int _rawSize;
        private int _compressedSize;

        public int Id
        {
            get
            {
                return _id;
            }
        }

        public int RawSize
        {
            get
            {
                return _rawSize;
            }
        }

        public int CompressedSize {
            get
            {
                return _compressedSize;
            }
        }

        public byte[] Content
        {
            get
            {
                return _content;
            }
        }

        public Block(int id, byte[] content, int rawSize, int compressedSize)
        {
            if (content == null)
                throw new ArgumentNullException("content");

            _id = id;
            _content = content;
            _rawSize = rawSize;
            _compressedSize = compressedSize;
        }
    }

    internal class BlockQueue
    {
        private readonly long MaxSize;
        private object _queueLock = new object();
        private Queue<Block> _innerQueue = new Queue<Block>();
        private long _expectedBlockId = 0;
        private long _size = 0;

        public BlockQueue(long maxSize)
        {
            MaxSize = maxSize;
        }

        public void EnqueueBlock(Block newBlock)
        {
            lock(_queueLock) 
            {
                while (newBlock.Id != _expectedBlockId || MaxSize < _size + newBlock.Content.Length)
                    Monitor.Wait(_queueLock);

                _innerQueue.Enqueue(newBlock);
                _expectedBlockId++;
                _size += newBlock.Content.Length;

                Monitor.PulseAll(_queueLock);
            }
        }

        public Block DequeueBlock()
        {
            lock(_queueLock)
            {
                while (_innerQueue.Count == 0)
                    Monitor.Wait(_queueLock);

                Block b = _innerQueue.Dequeue();
                _size -= b.Content.Length;

                Monitor.PulseAll(_queueLock);

                return b;
            }
        }
    }
}
