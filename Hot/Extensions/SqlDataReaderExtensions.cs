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

    }
}
