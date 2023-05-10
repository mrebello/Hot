namespace Hot;

public class HotLog : ILogger {
    /// <summary>
    /// Classe pública para acesso ao diretamente com
    /// <code>using static HotLog.log.Log</code>
    /// </summary>public class config {
    public class log {
        public readonly static HotLog Log = new HotLog();
        
        /// <summary>
        /// Cria um novo log com as configurações do sistema e novo nome de categoria
        /// </summary>
        /// <param name="Category"></param>
        /// <returns></returns>
        public static ILogger LogCreate(string Category) => Log.Create(Category);

        /// <summary>
        /// Método definido apenas para provocar a chamada à inicialização do Log.
        /// NÃO deve ser chamado pelo usuário da biblioteca.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void HotLog_Init() {
            HotConfiguration.__Set_LogOk(675272);
        }  // Provoca chamada do construtor
    }

    // Implementação com variável local static para 'singletron'
#pragma warning disable CS8618 // Tratado. O campo não anulável precisa conter um valor não nulo ao sair do construtor.
    private static ILogger _logger;
    private static ILoggerFactory _loggerFactory;
#pragma warning restore CS8618
    public ILoggerFactory loggerFactory { get => _loggerFactory; }
    public static ILogger logger { get => _logger; }
    static object InicializaLock = new object();
    /// <summary>
    /// Cria um novo log com as configurações do sistema e novo nome de categoria
    /// </summary>
    /// <param name="Category"></param>
    /// <returns></returns>
    public ILogger Create(string Category) => _loggerFactory.CreateLogger(Category);
    /// <summary>
    /// Cria um novo log com as configurações do sistema e nome de categoria pego do método que invovou a criação no novo log. (<0,0001s para criar)
    /// </summary>
    /// <returns></returns>
    public ILogger Create() {
        string nome;
        // m recebe o nome do método que chamou o Create()
        System.Reflection.MethodBase? m = new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod();
        if (m != null) {
            nome = m.ReflectedType?.ToString() + "." + m.Name;
        } else {
            nome = System.Reflection.Assembly.GetExecutingAssembly().GetName().FullName;
        }
        return _loggerFactory.CreateLogger(nome);
    }

    IDisposable? ILogger.BeginScope<TState>(TState state) => _logger.BeginScope<TState>(state);
    bool ILogger.IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _logger.Log<TState>(logLevel, eventId, state, exception, formatter);

    static public void LoggingCreate(ILoggingBuilder logging) {
        logging.AddConsole();
        if (OperatingSystem.IsWindows()) {
            logging.AddFilter<EventLogLoggerProvider>((LogLevel level) => level >= LogLevel.Warning);
        }
        logging.AddConfiguration(HotConfiguration.configuration.GetSection("Logging"));
        logging.AddConsole();
        logging.AddDebug();
        logging.AddEventSourceLogger();
        if (OperatingSystem.IsWindows()) {
            logging.AddEventLog(new EventLogSettings() { SourceName = Config["AppName"] });
        }
        logging.AddFileLogger();
        logging.AddemailLogger();
        logging.Configure((LoggerFactoryOptions options) => options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId | ActivityTrackingOptions.ParentId);
    }

    public HotLog() {
        lock (InicializaLock) {
            if (_logger == null) {
                // HotConf deve ser inicializado antes do LOG, pois opções de log estão nas configurações
                HotConfiguration.config.HotConfiguration_Init();

                _loggerFactory = LoggerFactory.Create(LoggingCreate);
                _logger = _loggerFactory.CreateLogger(System.Reflection.Assembly.GetExecutingAssembly().GetName().FullName);
            }
        }
    }
}
