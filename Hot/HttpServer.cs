using Microsoft.Extensions.Hosting.WindowsServices;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using static Hot.HotConfiguration;

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
                else if (url == "/infos") {
                    string IPOrigem = context.Request.IP_Origem();
                    string acceptFrom = Config[ConfigConstants.Update.AcceptFrom];

                    if (acceptFrom.IsEmpty()) {
                        Log.LogError($"'Update:AcceptFrom' deve estar configurado em appsettings.json para infos. Tentativa do IP: {IPOrigem}");
                        context.Response.SendError("Falha na configuração.");
                    }
                    else {
                        if (!IP_IsInList(IPOrigem, acceptFrom)) {
                            Log.LogError($"Tentativa de pegar informações de IP não autorizado. IP: {IPOrigem}");
                            context.Response.SendError("Não autorizado.", HttpStatusCode.Unauthorized);
                        }
                        else {
                        }
                        context.Response.Send(Config.Infos().ReplaceLineEndings("<br>"));
                    }
                }
                else if (url == "/autoupdate") {
                    string IPOrigem = context.Request.IP_Origem();
                    string acceptFrom = Config[ConfigConstants.Update.AcceptFrom];

                    if (acceptFrom.IsEmpty()) {
                        Log.LogError($"'Update:AcceptFrom' deve estar configurado em appsettings.json para autoupdate. Tentativa do IP: {IPOrigem}");
                        context.Response.SendError("Falha na configuração.");

                    }
                    else {
                        if (!IP_IsInList(IPOrigem, acceptFrom)) {
                            Log.LogError($"Tentativa de atualização de IP não autorizado. IP: {IPOrigem}");
                            context.Response.SendError("Não autorizado.", HttpStatusCode.Unauthorized);
                        }
                        else {
                            string configsecret = Config[ConfigConstants.Update.Secret];
                            string secret = context.Request.Headers["UpdateSecret"] ?? "";
                            if (configsecret != secret) {
                                Log.LogError($"UpdateSecret inválido. IP: {IPOrigem}");
                                context.Response.SendError("Não autorizado.", HttpStatusCode.Unauthorized);

                            }
                            else {

                                // Recebe arquivo atualizado e salva na pasta do executável (se não tiver permissão, não pode atualizar)
                                long size = 0;
                                string tmpfile = Path.GetDirectoryName(Config[ConfigConstants.ExecutableFullName]) + Path.DirectorySeparatorChar + Path.GetRandomFileName();
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
                                    context.Response.Send("Atualização recebida.");
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
            context.Response.Send(Config[ConfigConstants.AppName] + '\t' + Config[ConfigConstants.Version]);
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

        static string[] Prefixes_from_config() => HotConfiguration.configuration.GetSection(ConfigConstants.Prefixes).Get<string[]>();
        static string IgnorePrefix_from_config() => Config[ConfigConstants.IgnorePrefix] ?? "";
        async void Config_Changed(object state) {
            var new_Prefixes = HotConfiguration.configuration.GetSection(ConfigConstants.Prefixes).Get<string[]>();
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
                catch (HttpListenerException e) when (e.ErrorCode == 995     // Operação de E/S anulada
                                                   || e.ErrorCode == 500) {  // Listener closed 
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
                string batfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName() + ".bat";
                string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]);

                if (WindowsServiceHelpers.IsWindowsService()) {   // Rodando como serviço do windows
                    #region Atualiza Windows Service
                    Log.LogInformation($"Atualizando Windows Service.");

                    string servicename = Config.GetAsmResource.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";

                    string bat = "";
                    bat += $"cd {path}\r\n";
                    bat += $"sc stop {servicename}\r\n";
//                    bat += $"taskkill /im:\"{executablename}\"\r\n";
//                    bat += $"taskkill /F /im:\"{executablename}\"\r\n";
                    bat += $"ren \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\r\n";
                    bat += $"ren \"{tmpfile}\" \"{executablename}\"\r\n";
                    bat += $"sc start {servicename}\r\n";
                    bat += $"del \"{batfile}\"\r\n";

                    L.LogDebug($"Salvando arquivo {batfile} com " + bat);
                    File.WriteAllText(batfile, bat);
                    L.LogDebug($"Execuando arquivo {batfile}");
                    System.Diagnostics.Process.Start(batfile);
                    System.Environment.Exit(0);

                    #endregion
                }
                else {   // Se não é como serviço, assume que foi chamado por linha de comando
                    #region Atualiza Windows linha de comando
                    Log.LogInformation($"Atualizando Windows command line.");

                    string bat = "";
                    bat += $"cd {path}\r\n";
//                    bat += $"taskkill /im:\"{executablename}\"\r\n";
//                    bat += $"taskkill /F /im:\"{executablename}\"\r\n";
                    bat += $"ren \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\r\n";
                    bat += $"ren \"{tmpfile}\" \"{executablename}\"\r\n";
                    bat += String.Join(" ", Environment.GetCommandLineArgs()) + "\r\n";
                    bat += $"del \"{batfile}\"\r\n";

                    L.LogDebug($"Salvando arquivo {batfile} com " + bat);
                    File.WriteAllText(batfile, bat);
                    L.LogDebug($"Execuando arquivo {batfile}");
                    System.Diagnostics.Process.Start(batfile);
                    L.LogDebug($"Saindo do processo.");
                    System.Environment.Exit(0);

                    #endregion
                }
            }
            else if (OperatingSystem.IsLinux()) {
                void chmod(string file, string arguments) {
                    var p=System.Diagnostics.Process.Start("chmod", $"{arguments} \"{file}\"");
                    p.WaitForExit();
                }

                if (Microsoft.Extensions.Hosting.Systemd.SystemdHelpers.IsSystemdService()) {
                    #region Atualiza Linux Service
                    Log.LogInformation($"Atualizando serviço linux.");

                    string service_name = Config[ConfigConstants.ServiceName];

                    string path = Path.GetDirectoryName(tmpfile) ?? "";
                    string bashfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                    string bashfile2 = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                    string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]);

                    string bat = "#!/bin/bash\n";
                    bat += $"cd \"{path}\"\n";
                    bat += $"mv \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\n";
                    bat += $"mv \"{tmpfile}\" \"{executablename}\"\n";
                    bat += $"chmod u+x \"{executablename}\"\n";
                    bat += $"( rm \"{bashfile}\"  ; systemctl restart \"{service_name}\" )\n";

                    Log.LogDebug($"Salvando arquivo {bashfile} com " + bat);
                    File.WriteAllText(bashfile, bat);
                    chmod(bashfile, "u+x");

                    Log.LogDebug($"Execuando arquivo {bashfile}");
                    System.Diagnostics.Process.Start(bashfile);
                    //System.Environment.Exit(0);
                    
                    #endregion
                }
                else {
                    #region Atualiza Linux cmd line
                    Log.LogInformation($"Atualizando linux command line.");

                    string path = Path.GetDirectoryName(tmpfile) ?? "";
                    string bashfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                    string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]);
                    string servicename = Config.GetAsmResource.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";

                    string bat = "#!/bin/sh\n";
                    bat += $"cd \"{path}\"\n";
                    bat += $"sleep 0.3\n";
                    bat += $"mv \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\n";
                    bat += $"mv \"{tmpfile}\" \"{executablename}\"\n";
                    bat += $"chmod u+x \"{executablename}\"\n";
                    bat += $"sleep 0.3\n";
                    bat += String.Join(" ", Environment.GetCommandLineArgs()) + "\n";
                    bat += $"rm \"{bashfile}\"\n";

                    Log.LogDebug($"Salvando arquivo {bashfile} com " + bat);
                    File.WriteAllText(bashfile, bat);
                    chmod(bashfile, "u+x");
                    Log.LogDebug($"Execuando arquivo {bashfile}");
                    System.Diagnostics.Process.Start(bashfile);
                    Log.LogDebug($"Saindo do processo.");
                    System.Environment.Exit(0);

                    #endregion
                }
            }
            else {
                throw new NotImplementedException("AutoUpdate Só implementado para Linux e Windows.");
            }
        }
    }
}
