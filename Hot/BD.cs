namespace Hot;

/// <summary>
/// Implementa controle de conexão ADO.NET sobre uma conexão
/// </summary>
public class BD_simples : IDisposable {
    public SqlConnection sqlConnection { get; internal set; }
    ILogger Log = LogCreate("Hot.BD");

    private IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    /// <summary>
    /// Cria instância baseada na connection string de nome <i>config_name</i> das configurações.
    /// Caso config_name seja nulo/vazio, usa 'DefaultConnection'
    /// Troca @user@ e @pass@ por user e password, se fornecidos na string de conexão.
    /// </summary>
    /// <param name="config_name">Nome da seção ConectionStrings nas configurações</param>
    /// <param name="isSQLSERVER">Se true, adiciona MultipleActiveResultSets=True e Application Name=Config["AppName"] caso não existam.</param>
    /// <exception cref="ConfigurationErrorsException">Erro caso conection string não esteja configurada.</exception>
    public BD_simples(string? config_name = null, bool isSqlserver = true, string user = "", string password = "") {
        string name = config_name ?? "";
        if (name.Length == 0) name = "DefaultConnection";
        string connectionString = Config.GetConnectionString(name)?.Trim() ?? "";
        if (String.IsNullOrEmpty(connectionString)) {
            throw new ConfigurationErrorsException($"ConnectionStrings '{name}' deve estar configurado em appsettings.json.");
        }

        connectionString = connectionString.ExpandConfig().Replace("@user@", user).Replace("@pass@", password);

        if (isSqlserver) {
            if (!connectionString.Contains("MultipleActiveResultSets", StringComparison.OrdinalIgnoreCase)) {
                connectionString += ";MultipleActiveResultSets=True";
            }
            if (!connectionString.Contains("Application Name", StringComparison.OrdinalIgnoreCase)) {
                connectionString += ";Application Name=" + Config[ConfigConstants.AppName];
            }
        }

        sqlConnection = new SqlConnection(connectionString);
    }

    public SqlConnection sqlConnectionOpened {
        get {
            OpenConnection();
            return sqlConnection;
        }
    }


    private readonly object OpenConnection_lock = new object();
    /// <summary>
    /// Abre uma conexão, caso não esteja aberta e pronta (ConnectionState == Open).
    /// Gera exceção caso não consiga abrir.
    /// Caso erro não seja de permissão, tenta 5 vezes.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public bool OpenConnection() {
        int tentativas = 5;
        lock (OpenConnection_lock) {
            while (true) {
                try {
                    if (sqlConnection.State == ConnectionState.Open) {
                        return true;
                    }
                    if (sqlConnection.State == ConnectionState.Broken) sqlConnection.Close();
                    sqlConnection.Open();    // se não abrir, vai pro catch
                    break;
                } catch (Exception e) {
                    int er = (e as SqlException)?.Number ?? 0;
                    if (er == 4060 || er == 18456) tentativas = 1; // Gera o erro imediatamente para falhas de logon
                    if (er == -2146893019) tentativas = 1; // Erro de certificado do servidor
                    if (--tentativas == 0) {
                        string m = String.Format("Erro ao tentar abrir conexão: {0}. connection string: {1} Exception: {2}", e.Message, sqlConnection.ConnectionString, e);
                        Log.LogError(e, m);
                        throw new Exception(m, e);
                    } else {
                        Log.LogWarning(e.Message + " em: " + e.StackTrace);
                        Thread.Sleep(1000);
                    }
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
                } catch (Exception e) {
                    if (--tentativas == 0) {
                        string m = String.Format("Erro ao tentar fechar a conexão: {0}. Stack: {1} Exception: {2}", sqlConnection.ConnectionString, e.StackTrace, e.Message);
                        Log.LogError(e, m);
                        throw new Exception(m);
                    } else {
                        Log.LogWarning(e.Message + " em: " + e.StackTrace);
                        Thread.Sleep(1000);
                    }
                }
            }
        }
    }

    public void Dispose() {
        CloseConnection();
        //Log.Dispose();
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
    public SqlCommand SQLCommand(SqlTransaction? transaction, string SQL, params object?[] obj) {
        var cmd = new SqlCommand(SQL, sqlConnectionOpened, transaction);
        int c = 1;
        foreach (var o in obj) {
            SqlParameter p = new SqlParameter(c.ToString(), o ?? DBNull.Value);
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
    /// Monta um SqlCommand a partir do SQL e parâmetros, com timeout setado no comando. Os parâmetros são numerados a partir de 1.
    /// <code>
    /// SqlCmd(   connection, "SELECT @1 + @2", 1, 2 );
    /// </code>
    /// </summary>
    /// <param name="SQL"></param>
    /// <param name="obj"></param>
    /// <returns></returns>
    public SqlDataReader SQL(int timeout_seg, string SQL, params object?[] obj) {
        using (SqlCommand c = SQLCommand(null, SQL, obj)) {
            Log.LogInformation(() => HotLog.log.Log.Msg(LogInfo(c)));
            c.CommandTimeout = timeout_seg;
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
    public SqlDataReader SQL(string SQL, params object?[] obj) {
        using (SqlCommand c = SQLCommand(null, SQL, obj)) {
            Log.LogInformation(() => HotLog.log.Log.Msg(LogInfo(c)));
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
    public object SQLScalar(string SQL, params object?[] obj) {
        using (SqlCommand c = SQLCommand(null, SQL, obj)) {
            Log.LogInformation(() => HotLog.log.Log.Msg(LogInfo(c)));
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
    public int SQLCmd(string SQL, params object?[] obj) {
        using (SqlCommand c = SQLCommand(null, SQL, obj)) {
            Log.LogInformation(() => HotLog.log.Log.Msg(LogInfo(c)));
            return c.ExecuteNonQuery();
        }
    }



    public BDTransaction Transaction() {
        OpenConnection();
        return new BDTransaction(this);
    }


    public class BDTransaction : IDisposable {
        public BDTransaction(BD_simples bd) {
            this.bd = bd;
            _sqlTransaction = bd.sqlConnection.BeginTransaction();
        }

        SqlTransaction _sqlTransaction;
        public SqlTransaction sqlTransaction { get => _sqlTransaction; }

        public BD_simples bd { set; get; }

        public SqlDataReader SQL(string SQL, params object?[] obj) {
            ArgumentNullException.ThrowIfNull(bd, "BD nulo em transação.");
            using (SqlCommand c = bd.SQLCommand(sqlTransaction, SQL, obj)) {
                bd.Log.LogInformation(() => HotLog.log.Log.Msg(LogInfo(c)));
                return c.ExecuteReader();
            }
        }

        public object SQLScalar(string SQL, params object?[] obj) {
            ArgumentNullException.ThrowIfNull(bd, "BD nulo em transação.");
            using (SqlCommand c = bd.SQLCommand(sqlTransaction, SQL, obj)) {
                bd.Log.LogInformation(() => HotLog.log.Log.Msg(LogInfo(c)));
                return c.ExecuteScalar();
            }
        }

        public int SQLCmd(string SQL, params object?[] obj) {
            ArgumentNullException.ThrowIfNull(bd, "BD nulo em transação.");
            using (SqlCommand c = bd.SQLCommand(sqlTransaction, SQL, obj)) {
                bd.Log.LogInformation(() => HotLog.log.Log.Msg(LogInfo(c)));
                return c.ExecuteNonQuery();
            }
        }

        public void Commit() => sqlTransaction!.Commit();

        public void Rollback() => sqlTransaction!.Rollback();

        public void Dispose() {
            sqlTransaction?.Commit();
            sqlTransaction?.Dispose();
            bd.Dispose();
        }
    }


    string CacheKey(string sql, object?[] parameters) {
        return sql + string.Join(",", parameters);
    }

    public object SQLScalar(MemoryCacheEntryOptions cacheOptions, string SQL, params object?[] obj) {
        string cacheKey = CacheKey(SQL, obj);
        return _cache.GetOrCreate(cacheKey, e => {
            e.SetOptions(cacheOptions);
            return SQLScalar(SQL, obj);
        })!;
    }


    public DataTableReader SQL(MemoryCacheEntryOptions cacheOptions, int timeout_seg, string SQL_, params object?[] obj) {
        string cacheKey = CacheKey(SQL_, obj);
        return _cache.GetOrCreate(cacheKey, e => {
            e.SetOptions(cacheOptions);
            var dt = new DataTable();
            dt.Load(SQL(timeout_seg, SQL_, obj));
            return dt;
        })!.CreateDataReader();
    }

    public DataTableReader SQL(MemoryCacheEntryOptions cacheOptions, string SQL_, params object?[] obj) {
        string cacheKey = CacheKey(SQL_, obj);
        return _cache.GetOrCreate(cacheKey, e => {
            e.SetOptions(cacheOptions);
            var dt = new DataTable();
            dt.Load(SQL(SQL_, obj));
            return dt;
        })!.CreateDataReader();
    }

}
