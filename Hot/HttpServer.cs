using Microsoft.Extensions.Hosting.WindowsServices;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Hot {
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
                    Version(context);
                }
                else if (url == "/autoupdate") {
                    string IPOrigem = context.Request.IP_Origem();
                    string acceptFrom = Config["Update:AcceptFrom"];

                    if (acceptFrom.IsEmpty()) {
                        Log.LogError($"'Update:AcceptFrom' deve estar configurado em appsettings.json para autoupdate. Tentativa do IP: {IPOrigem}");
                        context.Response.SendError("Falha na configuração.");
                    
                    } else {
                        if (!IP_IsInList(IPOrigem, acceptFrom)) {
                            Log.LogError($"Tentativa de atualização de IP não autorizado. IP: {IPOrigem}");
                            context.Response.SendError("Não autorizado.", HttpStatusCode.Unauthorized);
                        } else {
                            string configsecret = Config["Update:Secret"];
                            string secret = context.Request.Headers["UpdateSecret"] ?? "";
                            if (configsecret != secret) {
                                Log.LogError($"UpdateSecret inválido. IP: {IPOrigem}");
                                context.Response.SendError("Não autorizado.", HttpStatusCode.Unauthorized);

                            }
                            else {

                                // Recebe arquivo atualizado e salva na pasta do executável (se não tiver permissão, não pode atualizar)
                                long size = 0;
                                string tmpfile = Path.GetDirectoryName(Config["ExecutableFullName"]) + "\\" + Path.GetRandomFileName();
                                try {
                                    var f = File.Create(tmpfile);
                                    context.Request.InputStream.CopyTo(f);
                                    size = f.Length;
                                    f.Close();
                                }
                                catch (Exception e) {
                                    Log.LogError("Erro ao salvar arquivo da atualização.", e);
                                }

                                // Se salvou o arquivo corretamente
                                if (size > 0) {
                                    context.Response.Send("Atualizado.");
                                    AutoUpdate_Process(tmpfile);
                                }
                                else {
                                    context.Response.SendError("Erro ao processar atualização.");
                                }
                            }
                        }
                    }
                    //                    MemoryStream ms = new MemoryStream();
                    //                    context.Request.InputStream.CopyTo(ms);
                    //                    byte[] bb = ms.ToArray();
                }
                else {
                    Process(context);
                    context?.Response?.Close();
                }
            }
            catch (Exception e) {
                string msg = "Erro ao processar o pedido.";
                Log.LogError(10000, e, msg);
                context.Response.SendError(msg);
            }
        }

        public virtual void Version(HttpListenerContext context) {
            context.Response.Send(Config["AppName"] + '\t' + Config["Version"]);
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

        static string[] Prefixes_from_config() => HotConfiguration.configuration.GetSection("Prefixes").Get<string[]>();
        static string IgnorePrefix_from_config() => Config["IgnorePrefix"] ?? "";
        async void Config_Changed(object state) {
            var new_Prefixes = HotConfiguration.configuration.GetSection("Prefixes").Get<string[]>();
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
            }
            catch (HttpListenerException ex) {
                if (ex.ErrorCode == 5) {   // Acesso negado
                    if (Config["IsWindows"] == "True") {
                        string msg = "Erro de acesso negado. Use os comandos abaixo para liberar o acesso no netsh:" + Environment.NewLine;
                        foreach (var s in prefixes) {
                            msg += $"netsh http add urlacl url={s} user=xxxxxx" + Environment.NewLine;
                        }
                        L.LogError(msg);
                    }
                }
                throw ex;
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
                }
                catch (HttpListenerException e) when (e.ErrorCode == 995) {   // Operação de E/S anulada
                }
                catch (Exception e) {
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


        /// <summary>
        /// Faz a atualização do próprio aplicativo. Recebe o nome do arquivo temporário com o novo executável.
        /// </summary>
        /// <param name=""></param>
        public virtual void AutoUpdate_Process(string tmpfile) {
            if (OperatingSystem.IsWindows()) {
                string path = Path.GetDirectoryName(tmpfile) ?? "";
                string batfile = path + "\\" + Path.GetRandomFileName() + ".bat";
                string executablename = Path.GetFileName(Config["ExecutableFullName"]);

                if (WindowsServiceHelpers.IsWindowsService()) {   // Rodando como serviço do windows
                    #region Atualiza Windows Service

                    string servicename = Config.GetAsmRessource.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";

                    string bat = "";
                    bat += $"cd {path}\r\n";
                    bat += $"sc stop {servicename}\r\n";
//                    bat += $"taskkill /im:\"{executablename}\"\r\n";
//                    bat += $"taskkill /F /im:\"{executablename}\"\r\n";
                    bat += $"ren \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\r\n";
                    bat += $"ren \"{tmpfile}\" \"{executablename}\"\r\n";
                    bat += $"sc start {servicename}\r\n";
                    bat += $"del \"{batfile}\"\r\n";

                    File.WriteAllText(batfile, bat);
                    System.Diagnostics.Process.Start(batfile);
                    System.Environment.Exit(0);

                    #endregion
                }
                else {   // Se não é como serviço, assume que foi chamado por linha de comando
                    #region Atualiza Windows linha de comando

                    string bat = "";
                    bat += $"cd {path}\r\n";
//                    bat += $"taskkill /im:\"{executablename}\"\r\n";
//                    bat += $"taskkill /F /im:\"{executablename}\"\r\n";
                    bat += $"ren \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\r\n";
                    bat += $"ren \"{tmpfile}\" \"{executablename}\"\r\n";
                    bat += String.Join(" ", Environment.GetCommandLineArgs()) + "\r\n";
                    bat += $"del \"{batfile}\"\r\n";

                    File.WriteAllText(batfile, bat);
                    System.Diagnostics.Process.Start(batfile);
                    System.Environment.Exit(0);

                    #endregion
                }
            }
            else if (OperatingSystem.IsLinux()) {
                #region Atualiza Linux

                throw new NotImplementedException("Falta implementar Autoupdate no Linux.");

                #endregion
            }
            else {
                throw new NotImplementedException("AutoUpdate Só implementado para Linux e Windows.");
            }
        }

    }
}
