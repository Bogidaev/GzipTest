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
        public int SizeBlock = 1024 * 1024*250;

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

        //private Queue QueueRead { get; }  =  new Queue(Environment.ProcessorCount*2);
        //private Queue QueueWrite { get; }  =  new Queue(Environment.ProcessorCount*2);


        private CustomThreadPool.CustomThreadPool CustomThreadPool { get; set; } =
            new CustomThreadPool.CustomThreadPool(10);
        private CustomThreadPool.CustomThreadPool CustomThreadPool1 { get; set; } =
            new CustomThreadPool.CustomThreadPool(5);

        private int DestBlockIndex { get; set; }

        public Gzip(CompressionMode mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// Выполнить
        /// </summary>
        /// <param name="from">Поток чтения</param>
        /// <param name="writeStream">Поток записи</param>
        public void Execute(string from, Stream writeStream)
        {
            switch (Mode)
            {
                case CompressionMode.Compress:
                    Compress(from, writeStream);
                    break;

                //case CompressionMode.Decompress:
                //    Decompress(readingStream, writeStream);
                //    break;
            }
        }

        private void Compress(string from, Stream writeStream)
        {

            var blocksCount = 0;
            using (FileStream readingStream = File.Open(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                blocksCount = (int) (readingStream.Length / SizeBlock + (readingStream.Length % SizeBlock > 0 ? 1 : 0));
            }


            DestBlockIndex = 0;
            this._waitingSignal = new AutoResetEvent(false);

            writeStream.Seek(0, SeekOrigin.Begin);

            for (int i = 0; i < blocksCount; i++)
            {
                long number = i;
                CustomThreadPool.QueueUserWorkItem(() =>
                {
                    this.CompressThread(from, writeStream, number, blocksCount);
                });
            }

            this._waitingSignal.WaitOne();
            //this.QueueRead.Dispose();
            //QueueWrite.Dispose();

            (CustomThreadPool as IDisposable).Dispose();
            (CustomThreadPool1 as IDisposable).Dispose();
        }

        //private void Decompress(Stream readingStream, Stream writeStream)
        //{
        //    var blockList = new List<Block>();
        //    this._waitingSignal = new AutoResetEvent(false);
        //    DestBlockIndex = 0;
        //    var binaryReader = new BinaryReader(readingStream);
        //    var number = 0;

        //    while (readingStream.Position < readingStream.Length)
        //    {
        //        var position = binaryReader.ReadInt64();
        //        var blockSize = binaryReader.ReadInt32();

        //        readingStream.Seek(blockSize, SeekOrigin.Current);

        //        blockList.Add(new Block
        //        {
        //            Number = number,
        //            Size = blockSize + _header,
        //            Position = position
        //        });
        //        number++;
        //    }

        //    readingStream.Seek(0, SeekOrigin.Begin);

        //    foreach (var block in blockList)
        //    {
        //        QueueRead.QueueTask(() =>
        //        {
        //            this.DecompressThread(readingStream, writeStream, block, blockList);
        //        });
        //    }
        //    this._waitingSignal.WaitOne();
        //    QueueRead.Dispose();
        //    QueueWrite.Dispose();

        //}

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

        private void DecompressThread(Stream readingStream, Stream writeStream, Block block, List<Block> blockList)
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

                if (++DestBlockIndex == blockList.Count)
                {
                    this._waitingSignal.Set();
                }

                this.WriteProgress(DestBlockIndex, blockList.Count);
            }
        }

        private void CompressThread(string from, Stream writeStream, long number, int blocksCount)
        {
            var buffer = new byte[SizeBlock];
            int bytesRead;
            var position = SizeBlock * number;

            using (FileStream readingStream = File.Open(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                readingStream.Seek(position, SeekOrigin.Begin);
                bytesRead = readingStream.Read(buffer, 0, SizeBlock);
            }

            var siz = this.Compression(buffer, bytesRead);

            CustomThreadPool1.QueueUserWorkItem(() =>
            {
                lock (WriteLocker)
                {
                    var binaryWriter = new BinaryWriter(writeStream);
                    binaryWriter.Write(position);
                    binaryWriter.Write(siz.Length);
                    writeStream.Write(siz, 0, siz.Length);

                    
                    if (++DestBlockIndex == blocksCount)
                    {
                        this._waitingSignal.Set();
                    }
                    this.WriteProgress(DestBlockIndex, blocksCount);
                }




            });
        }

        private void CompressWriteThread(string from, Stream writeStream, long number, int blocksCount,
            ref int destBlockIndex)
        {
        }
    }
}