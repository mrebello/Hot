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
            if (v == null)
                return null;
            if (v is DBNull)
                return null;
            if (v is string)
                return (string)v;
            return v.ToString();
        }

        /// <summary>
        /// Retorna objeto convertido para int, checando se é DBNull também. Caso seja null ou DBNull, retorna null;
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static int? ToInt(this object v) {
            if (v == null)
                return null;
            if (v is DBNull)
                return null;
            return (int)v;
        }

        /// <summary>
        /// Return a string with valid filename caracters (ASCII 7 bits). If NoPath, '\'and '/' is translated to '_'
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static String To_FileName(this String filename, Boolean NoPath = false) {
            String f = filename.Tira_Acentos().Translate("*&$@!?|'\"", "_________");
            if (NoPath)
                f = f.Translate("/\\", "__");
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
            if (s == null)
                throw new ArgumentNullException("s");
            if (s.Length > 1000000000)
                throw new NotImplementedException("Tratamento para arquivos grandes não implementado!");
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


        /// <summary>
        /// Transforma o objeto em uma string para visualização, listando todas as propriedades. (Usar para depuração)
        /// </summary>
        /// <param name="value">Objeto a ser visualizado</param>
        /// <param name="name">Nome do nível a ser usado</param>
        /// <param name="depth">profundidade</param>
        /// <returns></returns>
        public static string Dump(this object value, string name = "", int depth = 0) {
            if (depth > 3)
                return "...";

            StringBuilder result = new StringBuilder();

            string spaces = "|   ";
            string indent = new StringBuilder().Insert(0, spaces, depth).ToString();
            string displayValue = String.Empty;

            try {
                if (value != null) {
                    Type type = value.GetType();
                    displayValue = value.ToString() ?? String.Empty;

                    if (value is Boolean) {
                        displayValue = String.Format("{0}", value.Equals(true) ? "True" : "False");

                    } else if (value is Int16 || value is UInt16 ||
                               value is Int32 || value is UInt32 ||
                               value is Int64 || value is UInt64 ||
                               value is Single || value is Double ||
                               value is Decimal) {
                        displayValue = String.Format("{0}", value);

                    } else if (value is Char) {
                        displayValue = String.Format("{0} \"{1}\"", type.FullName, displayValue);

                    } else if (value is Char[]) {
                        displayValue = String.Format("{0}(#{1}) \"{2}\"", type.FullName, (value as Char[])!.Length,
                            displayValue);

                    } else if (value is String) {
                        displayValue = String.Format("\"{0}\"", displayValue);

                    } else if (value is Byte || value is SByte) {
                        displayValue = String.Format("{0} 0x{1:X}", type.FullName, value);

                    } else if (value is Byte[] || value is SByte[]) {
                        displayValue = String.Format("{0}(#{1}) 0x{2}", type.FullName, (value as Byte[])!.Length, BitConverter.ToString(value as Byte[]));

                    } else if (value is String[]) {
                        displayValue = String.Empty;
                        foreach(var s in (value as String[])!) {
                            displayValue += String.Format("\"{0}\", ", s);
                        }
                        if (displayValue.Length>0) displayValue = displayValue.Substring(0, displayValue.Length-2); // retira última ", "

                    } else if (value is ICollection) {
                        var i = 0;
                        var displayValues = String.Empty;
                        var collection = value as ICollection;
                        foreach (object element in collection) {
                            displayValues = String.Concat(displayValues, Dump(element, i.ToString(), depth + 1));
                            i++;
                        }
                        displayValue = String.Format("{0}(#{1}) {{\n{2}{3}}}", type.FullName, collection.Count, displayValues, indent);

                    } else if (value is IEnumerable) {
                        var i = 0;
                        var displayValues = String.Empty;

                        var collection = value as IEnumerable;
                        foreach (object element in collection!) {
                            displayValues = String.Concat(displayValues, Dump(element, i.ToString(), depth + 1));
                            i++;
                        }

                    } else {
                        var displayValues = String.Empty;

                        PropertyInfo[] properties = type.GetProperties();
                        foreach (PropertyInfo property in properties) {
                            displayValues = String.Concat(displayValues,
                                Dump(property.GetValue(value, null), property.Name, depth + 1));
                        }

                        displayValue = String.Format("{0}(#{1}) {{\n{2}{3}}}\n", type.Name, properties.Length, displayValues, indent);
                    }

                } else {
                    displayValue = "null";
                }

                if (name != String.Empty) {
                    result.Append(indent + ' ' + name + " => " + displayValue);
                } else {
                    result.Append(indent + displayValue);
                }
            } catch (Exception e) {
                result.Append("** exception: " + e.Message);
            }
            return result.ToString() + "\n";
        }

    }
}
