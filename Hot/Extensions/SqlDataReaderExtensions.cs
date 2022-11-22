using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hot.Extensions {
    public static class SqlDataReaderExtensions {
        public static DataTable ToDataTable(this SqlDataReader r) {
            DataTable dt = new();
            dt.Load(r);
            return dt;
        }

        public static DataRow? ToFirstDataRow(this SqlDataReader r) {
            DataTable dt = new();
            dt.Load(r);
            return dt.Rows.Count>0 ? dt.Rows[0] : null;
        }

        /// <summary>
        /// Devolve todos os dados em uma string. Devolve "" caso não haja dados.
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public static string ToText(this SqlDataReader r, string column_delimiter = "\t", string line_delimiter = "\r\n") {
            StringBuilder s = new StringBuilder();
            int nf = r.FieldCount;
            while (r.Read()) {
                for(int f = 0; f<nf; f++) {
                    s.Append(r[f].ToString());
                    if (f<nf-1) { s.Append(column_delimiter); }
                }
                s.Append(line_delimiter);
            }
            r.Close();
            return s.ToString();
        }

    }
}
