namespace Hot;

/// <summary>
/// Classe para implementação de servidor de http 'simples'.
/// Na inicialização do sistema, deve ser chamado o MainDefault com a classe especializada.
/// Método Initialize pode ser sobrescrito para validar parâmetros de configuração.
/// <code>
/// public static void Main(string[] args) {
///    MainDefault<MeuHttpServer>();
/// }
/// </code>
/// </summary>
abstract public class HttpServer : SelfHostedService {

    string[] prefixes = Prefixes_from_config();   // reloaded in Config_Changed
    HttpListener? listener = null;
    ILogger L = Log.Create("Hot.HttpServer");

    ~HttpServer() {
        L.LogInformation("~HttpServer: Fechando listener");
        listener?.Close();
    }

    /// <summary>
    /// Método que processa os pedidos http. Recebe o contexto da conexão.
    /// </summary>
    /// <param name="context"></param>
    public abstract void Process(HttpListenerContext context);

    /// <summary>
    /// Pré-processamento do pedido. Analisa /version e /update
    /// </summary>
    /// <param name="context"></param>
    public void PreProcess(HttpListenerContext context) {
        try {
            string url = RemoveIgnorePrefix(context.Request.RawUrl);
            if (url == "/version") {
                context.Response.Send(Hot.AutoUpdate.Version());
            } else if (url == "/infos") {
                if (AutoUpdate.Authorized(context)) context.Response.Send(AutoUpdate.Infos());
            } else if (url == "/autoupdate") {
                if (AutoUpdate.Authorized(context)) AutoUpdate.ReceiveFile(context);
            } else {

                Process(context);
                context?.Response?.Close();
            }
        } catch (Exception e) {
            string msg = "Erro ao processar o pedido.";
            Log.LogError(10000, e, msg);
            context.Response.SendError(msg);
        }
    }

    /// <summary>
    /// Método para inicialização do HttpServer.
    /// Usar para validar parâmetros de configuração, por exemplo.
    /// base() = valida parâmetros Prefixes como array de prefixos a ouvir.
    /// </summary>
    public virtual void Initialize() {
        if ((prefixes?.Length ?? 0) == 0) throw new ConfigurationErrorsException("'Prefixes' deve estar configurado em appsettings.json.");
        ((IConfiguration)Config).GetReloadToken().RegisterChangeCallback(Config_Changed, default);
    }

    static string[] Prefixes_from_config() => Config[ConfigConstants.URLs].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    static string IgnorePrefix_from_config() => Config[ConfigConstants.IgnorePrefix] ?? "";
    async void Config_Changed(object state) {
        var new_Prefixes = Prefixes_from_config();
        if (!prefixes.SequenceEqual(new_Prefixes)) {
            Log.LogWarning("Configuração de Prefixes foi alterada. Reiniciando Listener.");
            prefixes = new_Prefixes;
            await StopAsync(default);
            await StartAsync(default);
        }

        var new_IgnorePrefix = IgnorePrefix_from_config();
        if (new_IgnorePrefix != ignorePrefix) {
            Log.LogWarning("ignorePrefix foi alterado.");
            ignorePrefix = new_IgnorePrefix;
        }

        ((IConfiguration)Config).GetReloadToken().RegisterChangeCallback(Config_Changed, default);
    }


    public override Task StartAsync(CancellationToken cancellationToken) {
        L.LogDebug("Chamando Initialize()");
        Initialize();

        L.LogDebug("Inicializando listener");
        listener = new HttpListener();
        foreach (var s in prefixes) {
            listener.Prefixes.Add(s);
            L.LogInformation($"Prefixo de escuta adicionado: {s}");
        }
        L.LogDebug("Iniciando escuta");
        try {
            listener.Start();
        } catch (HttpListenerException ex) {
            if (ex.ErrorCode == 5) {   // Acesso negado
                if (OperatingSystem.IsWindows()) {
                    string msg = "Erro de acesso negado. Use os comandos abaixo para liberar o acesso no netsh:" + Environment.NewLine;
                    foreach (var s in prefixes) {
                        msg += $"netsh http add urlacl url={s} user=xxxxxx" + Environment.NewLine;
                    }
                    L.LogError(msg);
                } else {
                    L.LogError("Erro ao iniciar a escuta na porta.");
                }
            }
            throw;
        }

        // método BeginGetContext não está liberando os recursos utilizados, então, feito o GetContext de modo síncrono
        // Dispara uma thread com o loop de execução
        new Thread(() => listenerLoop(listener)).Start();

        return Task.CompletedTask;
    }

    void listenerLoop(HttpListener listener) {
        while (listener.IsListening) {
            try {
                HttpListenerContext ct = listener.GetContext();
                Task t = Task.Run(() => PreProcess(ct));               // usa task ao invés de thread para acelerar o processamento de respostas rápidas
            } catch (HttpListenerException e) when (e.ErrorCode == 995     // Operação de E/S anulada
                                                 || e.ErrorCode == 500) {  // Listener closed 
            } catch (Exception e) {
                Log.LogError(e, "Erro ao receber conexão.");
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken) {
        L.LogDebug("Fechando listener");
        listener?.Close();

        return Task.CompletedTask;
    }


    static string ignorePrefix = IgnorePrefix_from_config();   // reloaded in Config_Changed
    /// <summary>
    /// Remove o prefixo definido em IgnorePrefix nas configurações. Caso RawUrl seja nulo, devolve ""
    /// </summary>
    /// <param name="RawUrl"></param>
    /// context.Request.RawUrl
    /// <returns></returns>
    public static string RemoveIgnorePrefix(string? RawUrl) {
        string url = RawUrl ?? "";
        if (url.StartsWith(ignorePrefix)) url = url.Substring(ignorePrefix.Length);
        return url;
    }

}
