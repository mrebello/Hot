using static System.Net.Mime.MediaTypeNames;

namespace Hot.Extensions {
    /// <summary>
    /// Extensões para o tipo Stream
    /// </summary>
    public static partial class StreamExtensions {
        /// <summary>
        /// Le o stream para um byte[]
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this Stream s) {
            if (s == null)
                throw new ArgumentNullException("s");
            if (s.Length > 1000000000)
                throw new NotImplementedException("Tratamento para arquivos grandes não implementado!");
            byte[] b = new byte[s.Length];
            s.Read(b, 0, b.Length);
            return b;
        }
    }
}
