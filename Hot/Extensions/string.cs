using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hot.Extensions {
    public static class stringExtension {
        /// <summary>
        /// Remove all the trailing occurrences of the sequence from the current string.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="sequence">string with the sequence</param>
        /// <returns></returns>
        public static string TrimEnd(this string s, string sequence = " ") {
            if (s == null) return "";
            if (sequence == null) sequence = "";
            while (s.EndsWith(sequence)) s = s.Remove(s.Length - sequence.Length);
            return s;
        }

        /// <summary>
        /// Remove all the leading occurrences of the sequence from the current string.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="sequence">string with the sequence</param>
        /// <returns></returns>
        public static string TrimStart(this string s, string sequence = " ") {
            if (s == null) return "";
            if (sequence == null) sequence = "";
            while (s.StartsWith(sequence)) s = s.Substring(sequence.Length);
            return s;
        }

        /// <summary>
        /// Return the first 'lenght' caracters of string. Return "" on null.
        /// </summary>
        public static string Left(this string s, int lenght) {
            if (s == null) return "";
            if (s.Length <= lenght) return s;
            return s.Substring(0, lenght);
        }

        /// <summary>
        /// Return the last 'lenght' caracters of string. Return "" on null.
        /// </summary>
        public static string Right(this string s, int lenght) {
            if (s == null) return "";
            if (s.Length <= lenght) return s;
            return s.Substring(s.Length-lenght, lenght);
        }

        /// <summary>
        /// Return the string after the delimiter. If not delimiter found, return ""
        /// </summary>
        public static string After(this string s, string delimiter) {
            if (s == null || delimiter == null) return "";
            int x = s.IndexOf(delimiter);
            if (x >= 0) return s.Substring(x + delimiter.Length); else return "";
        }

        /// <summary>
        /// Return the string before the delimiter. If not delimiter, return the entire string
        /// </summary>
        public static string Before(this string s, string delimiter) {
            if (s == null) return "";
            if (delimiter == null) return s;
            int x = s.IndexOf(delimiter);
            if (x >= 0) return s.Substring(0, x); else return s;
        }

        /// <summary>
        /// Return the string splitted in before the delimiter and after delimiter. If not delimiter, return the entire string in before.
        /// </summary>
        public static (string before, string after) SplitIn2(this string s, string delimiter) {
            if (s == null) return ("", "");
            if (delimiter == null) return (s, "");
            int x = s.IndexOf(delimiter);
            if (x >= 0) {
                return (s.Substring(0, x), s.Substring(x + delimiter.Length));
            }
            else return (s, "");
        }

        /// <summary>
        /// Devolve o index (base 1) item da string com separados delimiter.
        /// Caso index seja negativo, conta de traz para frente (-1=último item,-2=penúltimo...)
        /// Caso index esteja fora da faixa, devolve ""
        /// </summary>
        /// <param name="s"></param>
        /// <param name="index"></param>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public static string Item(this string s, int index, string delimiter = ";") {
            if (s == null) return "";
            if (delimiter == null) return index == 1 ? s : "";
            var i = s.Split(delimiter, StringSplitOptions.None);
            if (index < 0) {
                return (-index) <= i.Length ? i[i.Length + index] : "";
            }
            else {
                return index >= 1 && index <= i.Length ? i[index - 1] : "";
            }
        }

        /// <summary>
        /// Remove repetições do carcter c (deixando apenas um) ao longo de toda a string.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static string? RemoveRepeated(this string s, char c) {
            if (s == null) return null;
            int p = s.IndexOf(c);
            int l = s.Length;
            if (p == -1) return s;
            p++;
            var sb = new StringBuilder(s.Substring(0, p));
            while (p >= 0) {
                while (p < l && s[p] == c) p++;
                if (p < l - 1) {
                    int p2 = s.IndexOf(c, p + 1);
                    sb.Append(s.Substring(p, (p2 == -1 ? l : p2) - p + 1));
                    p = p2;
                }
                else {
                    p = -1;
                }
                if (p == -1 && s[l - 1] != c) sb.Append(s[l - 1]);
            }
            return sb.ToString();
        }


        /// <summary>
        /// Translate each char from s that match from[x] by to[x].
        /// "ação*".Translate("çã*","ca") = "acao"
        /// </summary>
        /// <param name="s"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static string Translate(this string s, string from, string to) {
            if (s == null) return "";
            if (from == null) return s;
            if (to == null) to = "";
            StringBuilder sb = new();
            int p;
            foreach (char c in s) {
                p = from.IndexOf(c);
                if (p < 0) sb.Append(c); else sb.Append(to[p]);
            }
            return sb.ToString();
        }


        /// <summary>
        /// Tira acentos e devolve string ASCII 7 bits. Caso caracter sem acento seja >=127, coloca '?' no lugar.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string Tira_Acentos(this string s) {
            if (s == null) return "";
            var normalizedString = s.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++) {
                char c = normalizedString[i];
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark) {
                    stringBuilder.Append(((int)c < 127) ? c : '?');
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            //return Translate(s, "ÁÉÍÓÚÀÈÌÒÙÂÊÎÔÛÄËÏÖÜÃÕÑÝÆÇáéíóúàèìòùâêîôûäëïöüãõñçýæ",
            //                    "AEIOUAEIOUAEIOUAEIOUAONYACaeiouaeiouaeiouaeiouaoncya");
        }

        /// <summary>
        /// Gera texto para pesquisa de <i>nomes</i> (troca sequências/letras para comparação).
        /// Mantém " " e "%" na string para poder usar com coringa '%' na hora de pesquisar em SQL.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string IndexText(this String s) {
            if (s == null) return "";
            // Versão 2019-10
            s = s.Tira_Acentos() + " ";
            s = s.Replace("S ", " ");
            s = s.Replace(" DE ", " ");
            s = s.Replace(" DA ", " ");
            s = s.Replace(" DO ", " ");
            s = s.Replace("S%", "%");
            s = s.Replace("%DE%", "%");
            s = s.Replace("%DA%", "%");
            s = s.Replace("%DO%", "%");
            s = s.Replace("CE", "SE");
            s = s.Replace("CI", "SI");
            s = s.Replace("RR", "R");
            s = s.Replace("SS", "S");
            s = s.Replace("LL", "L");
            s = s.Replace("QU", "K");
            s = s.Replace("GN", "N");
            s = s.Replace("CH", "X");
            s = Translate(s, "WCNZYH-.+\"@\\ \'",
                             "UKMSI,,,,,,,,,");   // não troca espaço para gerar coringas
            s = s.Replace(",", "");

            return s;
        }

        /// <summary>
        /// Converte de UTF8 para unicode (string .net)
        /// </summary>
        /// <param name="utf8text"></param>
        /// <returns></returns>
        public static string FromUTF8(this string utf8text) {
            //            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            return Encoding.UTF8.GetString(Encoding.Default.GetBytes(utf8text));
        }

        /// <summary>
        /// Convert s to int. Return 0 if error in conversion.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static int ToInt(this String s) {
            int r = 0;
            int.TryParse(s, out r);
            return r;
        }

        /// <summary>
        /// Convert s to boolean. Return false if error in conversion.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool ToBool(this String s) {
            bool r = false;
            bool.TryParse(s.Trim(), out r);
            return r;
        }

        /// <summary>
        /// Converte string no formato dd/mm/aaaa para DateTime.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static DateTime ToDateTime_DD_MM_YYYY(this String s) {
            return DateTime.ParseExact(s, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Retira espaços e newlines repetidos no comando SQL.
        /// *** Não está fazendo o parser de strings!!!!!!
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string SQL_Reduce(this string s) {
            return s.Replace('\r', ' ').Replace('\n', ' ').RemoveRepeated(' ') ?? "";
        }

        /// <summary>
        /// Coloca string em formato de valor SQL.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToSQLValue(this string s) {
            return "'" + s.Replace("'", "''") + "'";
        }

        /// <summary>
        /// Return String.IsNullOrWhiteSpace(s)
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsEmpty(this string? s) => String.IsNullOrWhiteSpace(s);


        /// <summary>
        /// Return string with all '%(xxxxx)%' replaced with Conf["xxxxx"]
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ReplaceConf(this string s) {
            while (s.Contains("%("))   {    // se contém campo de configuração, faz a troca
                (string antes, string depois) = s.SplitIn2("%(");
                (string nome, depois) = depois.SplitIn2(")%");
                s = antes + Config[nome] + depois;
            }
            return s;
        }


}
}
