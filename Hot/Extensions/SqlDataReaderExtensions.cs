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
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
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
                for (int f = 0; f < nf; f++) {
                    s.Append(r[f].ToString());
                    if (f < nf - 1) { s.Append(column_delimiter); }
                }
                s.Append(line_delimiter);
            }
            r.Close();
            return s.ToString();
        }


        public class DataReaderCache : IDisposable {
            private IDataReader _reader;
            private int fieldCount = 0; // Número de colunas no IDataReader
            private List<object[]> _cachedRows;
            private List<string> _cachedColumns = new List<string>();
            private bool _readerFullyRead; // Indica se o IDataReader foi lido até o fim

            /// <summary>
            /// Inicializa uma nova instância da classe DataReaderCache.
            /// </summary>
            /// <param name="reader">O IDataReader a ser cacheado.</param>
            /// <exception cref="ArgumentNullException">Lançada se o reader for nulo.</exception>
            public DataReaderCache(IDataReader reader) {
                _reader = reader ?? throw new ArgumentNullException(nameof(reader));
                _cachedRows = new List<object[]>();
                _readerFullyRead = false;
                fieldCount = _reader.FieldCount; // Armazena o número de colunas
                for (int i = 0; i < fieldCount; i++) {
                    _cachedColumns.Add(_reader.GetName(i)); // Armazena os nomes das colunas
                }
            }

            /// <summary>
            /// Retorna uma faixa de registros do cache, lendo do IDataReader conforme necessário.
            /// </summary>
            /// <param name="startIndex">O índice inicial da faixa (base 0).</param>
            /// <param name="pageSize">O número de registros na faixa.</param>
            /// <returns>Uma lista de dicionários, onde cada dicionário representa uma linha e as chaves são os nomes das colunas.</returns>
            public IEnumerable<IDictionary<string, object>> GetPage(int startIndex, int pageSize) {
                if (startIndex < 0) {
                    throw new ArgumentOutOfRangeException(nameof(startIndex), "O índice inicial não pode ser negativo.");
                }
                if (pageSize <= 0) {
                    throw new ArgumentOutOfRangeException(nameof(pageSize), "O tamanho da página deve ser maior que zero.");
                }

                int endIndex = startIndex + pageSize;

                // Se a faixa solicitada excede o que já foi cacheado, lemos mais do DataReader
                if (endIndex > _cachedRows.Count && !_readerFullyRead) {
                    ReadMoreFromReader(endIndex);
                }

                // Retorna a faixa de registros do cache
                // Garante que não tentaremos acessar índices fora dos limites do cache
                int actualEndIndex = Math.Min(endIndex, _cachedRows.Count);
                if (startIndex >= actualEndIndex) {
                    return Enumerable.Empty<IDictionary<string, object>>(); // Faixa vazia se startIndex for maior ou igual ao número de itens cacheados
                }

                var resultPage = new List<IDictionary<string, object>>();
                for (int i = startIndex; i < actualEndIndex; i++) {
                    resultPage.Add(CreateRowDictionary(_cachedRows[i]));
                }

                return resultPage;
            }

            /// <summary>
            /// Lê mais registros do IDataReader até o índice especificado ou até o fim do reader.
            /// </summary>
            /// <param name="targetCount">O número alvo de registros a serem cacheado.</param>
            private void ReadMoreFromReader(int targetCount) {
                // Loop para ler do DataReader até atingir o targetCount ou o fim do reader
                while (_cachedRows.Count < targetCount) {
                    // Tenta ler o próximo registro
                    if (_reader.Read()) {
                        object[] row = new object[fieldCount];
                        _reader.GetValues(row);
                        _cachedRows.Add(row);
                    } else {
                        // Se Read() retornou false, significa que não há mais registros
                        _readerFullyRead = true;
                        break; // Sai do loop
                    }
                }
            }

            /// <summary>
            /// Converte um array de objetos em um dicionário, usando os nomes das colunas do IDataReader.
            /// </summary>
            /// <param name="rowValues">O array de objetos representando uma linha.</param>
            /// <returns>Um dicionário que mapeia nomes de colunas para seus valores.</returns>
            private IDictionary<string, object> CreateRowDictionary(object[] rowValues) {
                var rowDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++) {
                    // Pega o nome da coluna do IDataReader. Isso será constante após a primeira leitura.
                    rowDict[_cachedColumns[i]] = rowValues[i];
                }
                return rowDict;
            }

            /// <summary>
            /// Obtém o número total de registros cacheados até o momento.
            /// Se o IDataReader foi lido completamente, este será o total de registros.
            /// </summary>
            public int CachedRowCount => _cachedRows.Count;

            /// <summary>
            /// Indica se o IDataReader foi lido completamente.
            /// </summary>
            public bool IsFullyRead => _readerFullyRead;

            /// <summary>
            /// Obtém o número total de registros, lendo todos os registros para o cache.
            /// </summary>
            public int TotalRowCount {
                get {
                    if (!_readerFullyRead) {
                        ReadMoreFromReader(int.MaxValue); // Lê até o fim
                    }
                    return _cachedRows.Count;
                }
            }

            /// <summary>
            /// Descarta o IDataReader.
            /// </summary>
            public void Dispose() {
                if (_reader != null) {
                    _reader.Dispose();
                    _reader = null;
                }
            }
        }

    }
}
