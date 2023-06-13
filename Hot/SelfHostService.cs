namespace Hot;

public abstract class SelfHostedService : IHostedService {

    public abstract Task StartAsync(CancellationToken cancellationToken);
    public abstract Task StopAsync(CancellationToken cancellationToken);

    public delegate void Analisa_Parametros(string[] args);

    /// <summary>
    /// Rotina 'main' padrão para serviços. Chamar com
    ///   MainDefault<xxx>( sub_inicia ); com xxx sendo o nome da classe do programa.
    /// </summary>
    /// <typeparam name="Service"></typeparam>
    /// <param name="analisa_Parametros">Parâmetro opcional com a sub para analizar os parâmetros de linha de comando específicos</param>
    public static void MainDefault<Service>(Analisa_Parametros? analisa_Parametros = null) where Service : class, IHostedService {
        string[] args = Environment.GetCommandLineArgs();
        args[0] = "";  // primeiro elemento é o nome do executável

        var hostBuilder = new HostBuilder();

        HotConfiguration_Init();
        HotLog_Init();

        var host = hostBuilder
            .ConfigureServices(services => services
                .AddSingleton<IConfiguration, HotConfiguration>()
                .AddSingleton<ILogger, HotLog>()
                .AddHostedService<Service>()
             )
            .UseSystemd()
            .UseWindowsService()
            .Build();

        bool execute = true;
        bool daemon = false;

        Log.LogInformation(()=>Log.Msg(Config.Infos()));

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

                string service_name = Config[ConfigConstants.ServiceName]!;
                string display_name = Config[ConfigConstants.ServiceDisplayName]!;
                string descripton = Config[ConfigConstants.ServiceDescription]!;
                string executable = Config[ConfigConstants.ExecutableFullName]!;

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

                string service_name = Config[ConfigConstants.ServiceName]!;
                string display_name = Config[ConfigConstants.ServiceDisplayName]!;
                string descripton = Config[ConfigConstants.ServiceDescription]!;
                string executable = Config[ConfigConstants.ExecutableFullName]!;

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
                AutoUpdate.StartAutoUpdate();
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
}
