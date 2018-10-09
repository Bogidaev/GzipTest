using System.IO.Compression;

namespace Test.Model
{
    /// <summary>
    /// Аргументы
    /// </summary>
    public class Arguments
    {
        /// <summary>
        /// Мод
        /// </summary>
        public CompressionMode Mode { get; set; }

        /// <summary>
        /// Путь читаемого файла
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// Путь создаваемого файла
        /// </summary>
        public string To { get; set; }
    }
}
