using static System.Net.Mime.MediaTypeNames;

namespace Hot.Extensions {
    /// <summary>
    /// Extensões para o tipo DateTime
    /// </summary>
    public static class DateTimeExtension {
        /// <summary>
        /// Converte para o formato dd/MM/yyyy.
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static string ToString_DD_MM_YYYY(this DateTime dt) {
            return dt.ToString("dd/MM/yyyy");
        }
    }
}
