namespace Hot {
    /// <summary>
    /// Implementa controle de conexão ADO.NET sobre uma conexão
    /// </summary>
    public class BD_simples : IDisposable {
        public SqlConnection sqlConnection { get; internal set; }
        ILogger L = Log.Create("Hot.BD");

        /// <summary>
        /// Cria instância baseada na connection string de nome <i>config_name</i> das configurações.
        /// Caso config_name seja nulo/vazio, usa 'DefaultConnection'
        /// </summary>
        /// <param name="config_name">Nome da seção ConectionStrings nas configurações</param>
        /// <param name="isSQLSERVER">Se true, adiciona MultipleActiveResultSets=True e Application Name=Config["AppName"] caso não existam.</param>
        /// <exception cref="ConfigurationErrorsException">Erro caso conection string não esteja configurada.</exception>
        public BD_simples(string? config_name, bool isSqlserver = true) {
            string name = config_name ?? "";
            if (name.Length == 0) name = "DefaultConnection";
            string connectionString = Config.GetConnectionString(name).Trim();
            if (String.IsNullOrEmpty(connectionString)) {
                throw new ConfigurationErrorsException($"ConnectionStrings '{name}' deve estar configurado em appsettings.json.");
            }
            
            if (connectionString.Contains("%(")) {    // se contém campo de configuração para senha em secrets, faz a troca
                (string antes, string depois) = connectionString.SplitIn2("%(");
                (string nome, depois) = depois.SplitIn2(")%");
                connectionString = antes + Config[nome] + depois;
            }

            if (isSqlserver) {
                if (!connectionString.Contains("MultipleActiveResultSets", StringComparison.OrdinalIgnoreCase)) {
                    connectionString += ";MultipleActiveResultSets=True";
                }
                if (!connectionString.Contains("Application Name", StringComparison.OrdinalIgnoreCase)) {
                    connectionString += ";Application Name=" + Config["AppName"];
                }
            }

            sqlConnection = new SqlConnection(connectionString);
        }

        /// <summary>
        /// Abre uma conexão, caso não esteja aberta e pronta (ConnectionState == Open).
        /// Gera exceção caso não consiga abrir.
        /// Caso erro não seja de permissão, tenta 5 vezes.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public bool OpenConnection() {
            int tentativas = 5;
            while (true) {
                try {
                    if (sqlConnection.State == ConnectionState.Open) {
                        return true;
                    }
                    if (sqlConnection.State == ConnectionState.Broken) sqlConnection.Close();
                    sqlConnection.Open();    // se não abrir, vai pro catch
                    break;
                }
                catch (Exception e) {
                    int er = (e as SqlException)?.Number ?? 0;
                    if (er == 4060 || er == 18456) tentativas = 1; // Gera o erro imediatamente para falhas de logon
                    if (--tentativas == 0) {
                        string m = String.Format("Erro ao tentar abrir conexão: {0}. connection string: {1} Exception: {2}", e.Message, sqlConnection.ConnectionString, e);
                        L.LogError(e, m);
                        throw new Exception(m, e);
                    }
                    else {
                        L.LogWarning(e.Message + " em: " + e.StackTrace);
                        Thread.Sleep(1000);
                    }
                }
            }
            return sqlConnection.State == ConnectionState.Open;
        }

        /// <summary>
        /// Fecha uma conexão, se aberta.
        /// </summary>
        public void CloseConnection() {
            if (sqlConnection != null) {
                int tentativas = 5;
                while (true) {
                    try {
                        sqlConnection.Close();
                        break;
                    }
                    catch (Exception e) {
                        if (--tentativas == 0) {
                            string m = String.Format("Erro ao tentar fechar a conexão: {0}. Stack: {1} Exception: {2}", sqlConnection.ConnectionString, e.StackTrace, e.Message);
                            L.LogError(m);
                            throw new Exception(m);
                        }
                        else {
                            L.LogWarning(e.Message + " em: " + e.StackTrace);
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
        }

        public void Dispose() {
            CloseConnection();
            // L.Dispose
        }

        ~BD_simples() {
            Dispose();
        }



        /// <summary>
        /// Monta um SqlCommand a partir do SQL e parâmetros. Os parâmetros são numerados a partir de 1.
        /// <code>
        /// SqlCmd( connection, "SELECT @1 + @2", 1, 2 );
        /// </code>
        /// </summary>
        /// <param name="sqlConnection"></param>
        /// <param name="SQL"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public SqlCommand SQLCmd(SqlTransaction? transaction, string SQL, params object[] obj) {
            var cmd = new SqlCommand(SQL, sqlConnection, transaction);
            int c = 1;
            foreach (var o in obj) {
                SqlParameter p = new SqlParameter(c.ToString(), o);
                cmd.Parameters.Add(p);
                c++;
            }
            return cmd;
        }


        /// <summary>
        /// Devolve informações de log para um SqlCommand
        /// </summary>
        /// <param name="c">SqlCommand</param>
        /// <returns></returns>
        public static string LogInfo(SqlCommand c) {
            string i = "";
            i += c.Connection.Database;
            i += ": " + c.CommandText;
            if (c.Parameters.Count > 0) {
                i += " {";
                foreach (SqlParameter p in c.Parameters) {
                    i += " " + p.ParameterName + "=" + p.Value + ",";
                }
                i = i.TrimEnd(',') + " }";
            }
            return i;
        }


        /// <summary>
        /// Monta um SqlCommand a partir do SQL e parâmetros. Os parâmetros são numerados a partir de 1.
        /// <code>
        /// SqlCmd( connection, "SELECT @1 + @2", 1, 2 );
        /// </code>
        /// </summary>
        /// <param name="SQL"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public SqlDataReader SQL(string SQL, params object[] obj) {
            using (SqlCommand c = SQLCmd(null, SQL, obj)) {
                OpenConnection();
                L.LogInformation(() => Log.Msg(LogInfo(c)));
                return c.ExecuteReader();
            }
        }

        /// <summary>
        /// Monta um SqlCommand a partir do SQL e parâmetros. Os parâmetros são numerados a partir de 1.
        /// <code>
        /// SqlCmd( connection, "SELECT @1 + @2", 1, 2 );
        /// </code>
        /// </summary>
        /// <param name="SQL"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public object SQLScalar(string SQL, params object[] obj) {
            using (SqlCommand c = SQLCmd(null, SQL, obj)) {
                OpenConnection();
                L.LogInformation(() => Log.Msg(LogInfo(c)));
                return c.ExecuteScalar();
            }
        }

        /// <summary>
        /// Monta um SqlCommand a partir do SQL e parâmetros. Os parâmetros são numerados a partir de 1.
        /// <code>
        /// SqlCmd( connection, "SELECT @1 + @2", 1, 2 );
        /// </code>
        /// </summary>
        /// <param name="SQL"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int SQLCmd(string SQL, params object[] obj) {
            using (SqlCommand c = SQLCmd(null, SQL, obj)) {
                OpenConnection();
                L.LogInformation(() => Log.Msg(LogInfo(c)));
                return c.ExecuteNonQuery();
            }
        }

        

        public BDTransaction Transaction() {
            return new BDTransaction() {
                sqlTransaction = sqlConnection.BeginTransaction(),
                bd = this
            };
        }


        public class BDTransaction : IDisposable {
            public SqlTransaction? sqlTransaction { set; get; }
            public BD_simples? bd { set; get; }

            public SqlDataReader SQL(string SQL, params object[] obj) {
                ArgumentNullException.ThrowIfNull(bd, "BD nulo em transação.");
                using (SqlCommand c = bd.SQLCmd(sqlTransaction, SQL, obj)) {
                    bd.OpenConnection();
                    bd.L.LogInformation(() => Log.Msg(LogInfo(c)));
                    return c.ExecuteReader();
                }
            }

            public object SQLScalar(string SQL, params object[] obj) {
                ArgumentNullException.ThrowIfNull(bd, "BD nulo em transação.");
                using (SqlCommand c = bd.SQLCmd(null, SQL, obj)) {
                    bd.OpenConnection();
                    bd.L.LogInformation(() => Log.Msg(LogInfo(c)));
                    return c.ExecuteScalar();
                }
            }

            public int SQLCmd(string SQL, params object[] obj) {
                ArgumentNullException.ThrowIfNull(bd, "BD nulo em transação.");
                using (SqlCommand c = bd.SQLCmd(null, SQL, obj)) {
                    bd.OpenConnection();
                    bd.L.LogInformation(() => Log.Msg(LogInfo(c)));
                    return c.ExecuteNonQuery();
                }
            }


            public void Dispose() {
                sqlTransaction?.Commit();
                sqlTransaction?.Dispose();
                bd = null;
            }
        }
    }
}
