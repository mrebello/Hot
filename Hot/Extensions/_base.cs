using static System.Net.Mime.MediaTypeNames;

namespace Hot.Extensions {
    /// <summary>
    /// Extensões para os tipos básicos do C#: object, string, int, ...
    /// </summary>
    public static class _base {
        /// <summary>
        /// Retorna ".ToString()" do objeto, checando se é DBNull também. Caso seja null ou DBNull, retorna null;
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static String? ToStr(this object v) {
            if (v == null) return null;
            if (v is DBNull) return null;
            if (v is string) return (string)v;
            return v.ToString();
        }

        /// <summary>
        /// Retorna objeto convertido para int, checando se é DBNull também. Caso seja null ou DBNull, retorna null;
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static int? ToInt(this object v) {
            if (v == null) return null;
            if (v is DBNull) return null;
            return (int) v;
        }

        /// <summary>
        /// Return a string with valid filename caracters (ASCII 7 bits). If NoPath, '\'and '/' is translated to '_'
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static String To_FileName(this String filename, Boolean NoPath = false) {
            String f = filename.Tira_Acentos().Translate("*&$@!?|'\"","_________");
            if (NoPath) f = f.Translate("/\\", "__");
            return f;
        }


        /// <summary>
        /// Converte para o formato dd/MM/yyyy.
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static string ToString_DD_MM_YYYY(this DateTime dt) {
            return dt.ToString("dd/MM/yyyy");
        }


        /// <summary>
        /// Le o stream para um byte[]
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this Stream s) {
            if (s==null) throw new ArgumentNullException("s");
            if (s.Length > 1000000000) throw new NotImplementedException("Tratamento para arquivos grandes não implementado!");
            byte[] b = new byte[s.Length];
            s.Read(b, 0, b.Length);
            return b;
        }


        /// <summary>
        /// Valida se o valor do enum está dentro dos itens do enum
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static bool IsEnumValueDefined(this Enum e, object v) => e.GetType().IsEnumDefined(v);


        /// <summary>
        /// Valida se o valor atual do enum está dentro dos itens do enum
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static bool IsEnumValueDefined(this Enum e) => e.GetType().IsEnumDefined(e);

    }
}
