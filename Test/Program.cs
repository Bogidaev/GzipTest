using System;
using System.IO;
using System.IO.Compression;
using Test.Exceptions;
using Test.Model;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var arg = ReadProcessing(args);

                Run(arg);
            }
            catch (IncorrectParametersException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine(e.Message);
            }
        }

        private static void Run(Arguments arg)
        {
            var gzip = new Gzip(arg.Mode);
            using (FileStream sourceStream = File.Open(arg.From, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (FileStream targetStream = File.Create(arg.To))
                {
                    gzip.Execute(sourceStream, targetStream);
                }
            }
        }

        private static Arguments ReadProcessing(string[] args)
        {
            if (args.Length < 3)
            {
                throw new IncorrectParametersException("Некорректное количество аргументов");
            }

            var arguments =  new Arguments
            {
                From = args[1],
                To = args[2]
            };

            switch (args[0].ToLower())
            {
                case "compress":
                    arguments.Mode = CompressionMode.Compress;
                    break;
                case "decompress":
                    arguments.Mode = CompressionMode.Decompress;
                    break;
                default: throw new ArgumentOutOfRangeException("mode", "Некорректный режим работы");
            }

            return arguments;
        }
}

}
