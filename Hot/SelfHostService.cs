using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using static Hot.HotConfiguration;

namespace Hot {
    public abstract class SelfHostedService : IHostedService {

        public abstract Task StartAsync(CancellationToken cancellationToken);
        public abstract Task StopAsync(CancellationToken cancellationToken);

        public delegate void Analisa_Parametros(string[] args);

        /// <summary>
        /// Rotina 'main' padrão para serviços. Chamar com
        ///   MainDefault<xxx>( sub_inicia ); com xxx sendo o nome da classe do programa.
        /// </summary>
        /// <typeparam name="service"></typeparam>
        /// <param name="analisa_Parametros">Parâmetro opcional com a sub para analizar os parâmetros de linha de comando específicos</param>
        public static void MainDefault<service>(Analisa_Parametros? analisa_Parametros = null) where service : class, IHostedService {
            string[] args = Environment.GetCommandLineArgs();
            args[0] = "";  // primeiro elemento é o nome do executável

            var hostBuilder = new HostBuilder();

            HotConfiguration_Init();
            HotLog_Init();

            var host = hostBuilder
                .ConfigureServices(services => services
                    .AddSingleton<IConfiguration, HotConfiguration>()
                    .AddSingleton<ILogger, HotLog>()
                    .AddHostedService<service>()
                 )
                .UseSystemd()
                .UseWindowsService()
                .Build();

            bool execute = true;
            bool daemon = false;

            Log.LogInformation(()=>Log.Msg(Config.Infos()));

            //System.Resources.ResourceManager rm = new(typeof(Hot.Properties.Modelos));

            //var t1 = rm.GetString("Teste1");
            
            Properties.Modelos.Culture = CultureInfo.CurrentCulture;
            var s = Properties.Modelos.Teste1;

            Properties.Modelos.Culture = CultureInfo.CreateSpecificCulture("en-US");
            var s2 = Properties.Modelos.Teste1;

            Properties.Modelos.Culture = CultureInfo.CreateSpecificCulture("fr");
            var s3 = Properties.Modelos.Teste1;

            if (Environment.UserInteractive) {
                if (new[] { "/?", "-h", "-H", "-?", "--help", "/help" }.Any(args.Contains)) {
                    execute = false;
                    // caso não encontre, pesquisa como recurso incorporado na LIB
                    var help_stream = Config.GetAsmStream("Help_Parameters.txt") ?? Config.GetLibStream("Help_Parameters.txt");
                    if (help_stream != null) {
                        Console.WriteLine(
                            Path.GetFileNameWithoutExtension(Config[ConfigConstants.ExecutableFullName]) + " " + 
                            new StreamReader(help_stream).ReadToEnd());
                    }
                }
                else if (new[] { "/helpconfig", "--helpconfig" }.Any(args.Contains)) {
                    execute = false;
                    Console.WriteLine("Config search path:");
                    Console.WriteLine(HotConfiguration.configSearchPath);
                }
                else if (new[] { "/infos", "--infos" }.Any(args.Contains)) {
                    execute = false;
                    Console.WriteLine(Config.Infos()); ;
                }
                else if (new[] { "/install", "--install" }.Any(args.Contains)) {
                    #region Install // Faz a instalação do serviço
                    // faz a instalação como serviço
                    execute = false;

                    string service_name = Config[ConfigConstants.ServiceName];
                    string display_name = Config[ConfigConstants.ServiceDisplayName];
                    string descripton = Config[ConfigConstants.ServiceDescription];
                    string executable = Config[ConfigConstants.ExecutableFullName];

                    Console.WriteLine($"Install service_name: {service_name}");

                    Log.LogInformation($"Install\r\n service_name: {service_name}\r\n display_name: {display_name}\r\n descripton: {descripton}\r\n file: \"{executable}\"");

                    if (OperatingSystem.IsWindows()) {
                        // Instala o serviço através do comando sc.exe
                        RunProcess_SUDO("sc.exe", $"create {service_name} displayname=\"{display_name}\" start=auto binpath=\"{executable}\"");
                        if (descripton.Length > 0) {
                            RunProcess_SUDO("sc.exe", $"description {service_name} \"{descripton.Replace("\"", "^\"")}\"");
                        }
                    }
                    else if (OperatingSystem.IsLinux()) {
                        // checa se o sistema usa systemd verificando existência do diretório
                        if (Directory.Exists("/etc/systemd/system")) {
                            var servicefilestream = Config.GetLibStream("template.service");
                            if (servicefilestream!=null) {
                                string servicefile = new StreamReader(servicefilestream).ReadToEnd();
                                
                                // preenche template com configurações
                                servicefile = servicefile.ExpandConfig();

                                File.WriteAllText($"/etc/systemd/system/{service_name}.service", servicefile);

                                Console.WriteLine($"Serviço instalado. Use 'systemctl enable {service_name}' para ativar o serviço.");
                            }
                            else {
                                Console.Error.WriteLine("Template de serviço não encontrado em HotLib.");
                            }
                        } else {
                            Console.Error.WriteLine("/etc/systemd/system não encontrado. Suporte a apenas inicialização systemd.");
                        }
                    }
                    else {
                        Console.Error.WriteLine("unsupported Operating System.");
                    }
                    #endregion
                }
                else if (new[] { "/uninstall", "--uninstall" }.Any(args.Contains)) {
                    #region Uninstall // faz a desinstalação como serviço
                    execute = false;

                    string service_name = Config[ConfigConstants.ServiceName];
                    string display_name = Config[ConfigConstants.ServiceDisplayName];
                    string descripton = Config[ConfigConstants.ServiceDescription];
                    string executable = Config[ConfigConstants.ExecutableFullName];

                    Console.WriteLine($"Uninstall service_name: {service_name}");

                    if (OperatingSystem.IsWindows()) {
                        // Desinstala o servico através do comando sc.exe
                        RunProcess_SUDO("sc.exe", $"stop {service_name}");
                        RunProcess_SUDO("sc.exe", $"delete {service_name}");
                    }
                    else if (OperatingSystem.IsLinux()) {
                        Log.LogError("Não Implementado!");
                    }
                    else {
                        Console.Error.WriteLine("unsupported Operating System.");
                    }
                    #endregion
                }
                else if (new[] { "/autoupdate", "--autoupdate" }.Any(args.Contains)) {
                    execute = false;
                    //new Thread(() => AutoUpdate()).Start();
                    StartAutoUpdate();
                }
                else if (new[] { "-v", "--version", "/v", "/version" }.Any(args.Contains)) {
                    execute = false;
                    Console.WriteLine(Config["AppName"] + '\t' + Config["Version"]);
                }
            }

            if (new[] { "/d", "/daemon", "-d", "--daemon" }.Any(args.Contains)) {
                daemon = true;
            }

            try {
                //if (analisa_Parametros != null) analisa_Parametros(args);
                analisa_Parametros?.Invoke(args);
            } catch (Exception e) {
                Log.LogCritical( e, "Erro analisando parâmetros.");
            }

            try {
                if (execute) {
                    if (!daemon && Environment.UserInteractive && !Console.IsInputRedirected) {
                        host.RunAsync();
                        try {
                            Console.WriteLine("De [Enter] para encerrar.");
                            Console.ReadLine();
                            host.StopAsync();
                        } catch (Exception e) {
                            Log.LogError(e, "Erro ao pedir enter.");
                        }
                    } else {
                        host.Run();
                    }
                }
            } catch (Exception e) {
                Log.LogCritical(e, "Erro ao executar o hosting.");
            }
        }

        public static void RunProcess_SUDO(string filename, string arguments) {
            if (OperatingSystem.IsWindows()) {
                var startInfo = new ProcessStartInfo() {
                    FileName = filename,
                    Arguments = arguments,
                    UseShellExecute = false,
                    Verb = "runas"
                };
                var p = Process.Start(startInfo);
                p!.WaitForExit();
            }
            else if (OperatingSystem.IsLinux()) {
                var startInfo = new ProcessStartInfo() {
                    FileName = filename,
                    Arguments = arguments,
                    UseShellExecute = false,
                    Verb = "runas"
                };
                var p = Process.Start(startInfo);
                p!.WaitForExit();
            }
            else {
                Log.LogError("unsupported Operating System.");
            }
        }

        public static void StartAutoUpdate() {
            //Thread.Sleep(1000);
            try {
                string url = Config[ConfigConstants.Update.URL];
                if (url.IsEmpty()) throw new ConfigurationErrorsException("'Update:URL' deve estar configurado em appsettings.json para autoupdate.");

                string destination = "";

                using var hc = new HttpClient();
                try {
                    destination = hc.GetStringAsync(url + "version").Result;
                }
                catch (Exception) {
                }
                if (destination.IsEmpty()) throw new Exception("Erro ao pegar a versão atual.");

                string app_destination = destination.Item(1, "\t");
                string version_destination = destination.Item(2, "\t");

                string app_me = Config[ConfigConstants.AppName];
                if (app_me != app_destination) {
                    throw new Exception($"App destino ({app_destination} diferente de app origem {app_me}. Não atualizado.");
                }

                string version_me = Config[ConfigConstants.Version];
                //var c = Compare_Versions(version_me, version_destination);
                //if (c <= 0) {
                //    string msg = c == 0 ? "Versões são iguais." : "Versão instalada é maior.";
                //    throw new Exception(msg + " Não atualizado.");
                //}

                //Log.LogInformation($"Iniciando atualização da versão {version_destination} para a {version_me}.");

                var executable = File.OpenRead(Config[ConfigConstants.ExecutableFullName]);
                // if (executable.Length < 4_500_000) throw new Exception("Arquivo não parece ser pacote publicado. Abortando.");

                using var fileStreamContent = new StreamContent(executable);
                // Considera possível erro de não receber resposta completa devido a shutdown do app server
                try {
                    hc.DefaultRequestHeaders.Add("UpdateSecret", Config[ConfigConstants.Update.Secret]);
                    var r = hc.PutAsync(url + "autoupdate", fileStreamContent).Result;
                }
                catch (Exception) {
                }

                // Teste para verificar se foi atualizado
                int tentativas = 10;
                bool ok = false;
                string v = "";
                while (!ok && tentativas >= 0) {
                    Thread.Sleep(500); // aguarda processar
                    try {
                        v = hc.GetStringAsync(url + "version").Result;
                    }
                    catch (Exception) {
                    }
                    if (v.Item(2, "\t") == version_me) {
                        ok = true;
                        break;
                    }
                    tentativas--;
                }

                if (ok) {
                    Log.LogInformation("Atualização processada com sucesso.");
                    Console.WriteLine("Atualização processada com sucesso.");
                }
                else {
                    Log.LogError("Não houve erro, porém não foi detectada a nova versão instalada.");
                }
            }
            catch (Exception e) {
                Log.LogError(e, "Erro.");
                throw;
            }
        }
    }
}
