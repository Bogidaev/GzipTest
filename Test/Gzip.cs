using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Test.Extension;
using Test.Model;

namespace Test
{
    /// <summary>
    /// Архиватор
    /// </summary>
    public class Gzip
    {  
        /// <summary>
        /// Блокировка основного потока
        /// </summary>
        private AutoResetEvent _waitingSignal;

        /// <summary>
        /// Мод для работы
        /// </summary>
        private CompressionMode Mode { get; }

        /// <summary>
        /// Размер блока
        /// </summary>
        public int SizeBlock = 1024 * 1024 * 25;

        /// <summary>
        /// Размер шапки
        /// </summary>
        private int _header = sizeof(int) + sizeof(long);

        /// <summary>
        /// Объект для блокировки чтения 
        /// </summary>
        private static readonly object ReadLocker = new object();

        /// <summary>
        /// Объект для блокировки записи 
        /// </summary>
        private static readonly object WriteLocker = new object();

        public Gzip(CompressionMode mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// Выполнить
        /// </summary>
        /// <param name="readingStream">Поток чтения</param>
        /// <param name="writeStream">Поток записи</param>
        public void Execute(Stream readingStream, Stream writeStream)
        {
            switch (Mode)
            {
                case CompressionMode.Compress:
                    Compress(readingStream, writeStream);
                    break;

                case CompressionMode.Decompress:
                    Decompress(readingStream, writeStream);
                    break;
            }
        }

        private void Compress(Stream readingStream, Stream writeStream)
        {
            var blocksCount = (int) (readingStream.Length / SizeBlock + (readingStream.Length % SizeBlock > 0 ? 1 : 0));
            var destBlockIndex = 0;
            this._waitingSignal = new AutoResetEvent(false);

            readingStream.Seek(0, SeekOrigin.Begin);
            writeStream.Seek(0, SeekOrigin.Begin);

            using (var queue = new Queue())
            {
                for (int i = 0; i < blocksCount; i++)
                {
                    long number = i;
                    queue.QueueTask(() =>
                    {
                        this.CompressThread(readingStream, writeStream, number, blocksCount, ref destBlockIndex);
                    });
                }

                this._waitingSignal.WaitOne();
            }
        }

        private void Decompress(Stream readingStream, Stream writeStream)
        {
            var blockList = new List<Block>();
            this._waitingSignal = new AutoResetEvent(false);
            var destBlockIndex = 0;
            var binaryReader = new BinaryReader(readingStream);
            var number = 0;

            while (readingStream.Position < readingStream.Length)
            {
                var position = binaryReader.ReadInt64();
                var blockSize = binaryReader.ReadInt32();
   
                readingStream.Seek(blockSize, SeekOrigin.Current);

                blockList.Add(new Block
                {
                    Number = number,
                    Size = blockSize + _header, 
                    Position = position
                });
                number++;
            }

            readingStream.Seek(0, SeekOrigin.Begin);

            using (var queue = new Queue())
            {
                foreach (var block in blockList)
                {
                    queue.QueueTask(() =>
                    {
                        this.DecompressThread(readingStream, writeStream, block, blockList, ref destBlockIndex);
                    });
                }
                this._waitingSignal.WaitOne();
            }
        }

        private byte[] Compression(byte[] date, int length)
        {
            using (var result = new MemoryStream())
            {
                using (var compressionStream = new GZipStream(result, Mode))
                {
                    compressionStream.Write(date, 0, length);
                }

                return result.ToArray();
            }
        }

        private byte[] DecompressBuffer(byte[] from, int length)
        {
            using (var source = new MemoryStream(from, 0, length))
            {
                using (var dest = new MemoryStream())
                {
                    using (var compressionStream = new GZipStream(source, CompressionMode.Decompress))
                    {
                        compressionStream.CopyTo(dest);
                        return dest.ToArray();
                    }
                }
            }
        }

        private void WriteProgress(int number, int count)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"Завершено: {100 * number / count}%");
        }

        private void DecompressThread(Stream readingStream, Stream writeStream, Block block, List<Block> blockList, ref int destBlockIndex)
        {
            var buffer = new byte[10];
            Array.Resize(ref buffer, block.Size);
            int readBlockLength;
            lock (ReadLocker)
            {
                readingStream.Seek(blockList.Where(x => x.Number < block.Number).Sum(x => (long)x.Size) + _header,
                    SeekOrigin.Begin);
                readBlockLength = readingStream.Read(buffer, 0, block.Size);
            }

            var arr = this.DecompressBuffer(buffer, readBlockLength);

            lock (WriteLocker)
            {
                writeStream.Seek(block.Position, SeekOrigin.Begin);
                writeStream.Write(arr, 0, arr.Length);

                if (++destBlockIndex == blockList.Count)
                {
                    this._waitingSignal.Set();
                }

                this.WriteProgress(destBlockIndex, blockList.Count);
            }
        }

        private void CompressThread(Stream readingStream, Stream writeStream, long number, int blocksCount, ref int destBlockIndex)
        {
            var buffer = new byte[SizeBlock];
            int bytesRead;
            var position = SizeBlock * number;

            lock (ReadLocker)
            {
                readingStream.Seek(position, SeekOrigin.Begin);
                bytesRead = readingStream.Read(buffer, 0, SizeBlock);
            }

            var siz = this.Compression(buffer, bytesRead);

            lock (WriteLocker)
            {
                var binaryWriter = new BinaryWriter(writeStream);
                binaryWriter.Write(position);
                binaryWriter.Write(siz.Length);
                writeStream.Write(siz, 0, siz.Length);

                if (++destBlockIndex == blocksCount)
                {
                    this._waitingSignal.Set();
                }

                this.WriteProgress(destBlockIndex, blocksCount);
            }

        }
    }
}