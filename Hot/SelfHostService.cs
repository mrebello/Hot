namespace Hot;

public abstract class SelfHostedService : IHostedService {

    public virtual Task StartAsync(CancellationToken cancellationToken) {
        ((IConfiguration)Config).GetReloadToken().RegisterChangeCallback(Config_Changed_trap, default);

        return Task.CompletedTask;
    }

    public abstract Task StopAsync(CancellationToken cancellationToken);

    public delegate void Analisa_Parametros(string[] args);

    /// <summary>
    /// Rotina 'main' padrão para serviços. Chamar com
    ///   MainService<xxx>( sub_inicia ); com xxx sendo o nome da classe do programa.
    /// </summary>
    /// <typeparam name="Service"></typeparam>
    /// <param name="analisa_Parametros">Parâmetro opcional com a sub para analizar os parâmetros de linha de comando específicos</param>
    /// <returns>0 - Rodou normalmente (instalou, desinstalou, ou executou e terminou normalmente)
    /// 1 - Erro</returns>
    public static int MainService<Service>(Analisa_Parametros? analisa_Parametros = null) where Service : class, IHostedService {
        string[] args = Environment.GetCommandLineArgs();
        args[0] = "";  // primeiro elemento é o nome do executável

        int exitCode = 0;

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
                            exitCode = 10;
                        }
                    } else {
                        Console.Error.WriteLine("/etc/systemd/system não encontrado. Suporte a apenas inicialização systemd.");
                        exitCode = 11;
                    }
                }
                else {
                    Console.Error.WriteLine("unsupported Operating System.");
                    exitCode = 12;
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
                    exitCode= 13;
                }
                else {
                    Console.Error.WriteLine("unsupported Operating System.");
                    exitCode = 14;
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
            Config.IsDaemon = daemon;
        }

        try {
            //if (analisa_Parametros != null) analisa_Parametros(args);
            analisa_Parametros?.Invoke(args);
        } catch (Exception e) {
            Log.LogCritical( e, "Erro analisando parâmetros.");
            exitCode = 15;
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
            exitCode = 16;
        }
        return exitCode;
    }


    public virtual void Config_Changed(object? state) {
        //var new_Prefixes = Prefixes_from_config();
        //if (!prefixes.SequenceEqual(new_Prefixes)) {
        //    Log.LogWarning("Configuração de Prefixes foi alterada. Reiniciando Listener.");
        //    prefixes = new_Prefixes;
        //    await StopAsync(default);
        //    await StartAsync(default);
        //}

        //var new_IgnorePrefix = IgnorePrefix_from_config();
        //if (new_IgnorePrefix != ignorePrefix) {
        //    Log.LogWarning("ignorePrefix foi alterado.");
        //    ignorePrefix = new_IgnorePrefix;
        //}
        HotLog.log.Log.LogInformation("RECONFIGURADO");
    }

#pragma warning disable CS1998 // O método assíncrono não possui operadores 'await' e será executado de forma síncrona
    async void Config_Changed_trap(object? state) {
        Config_Changed(state);
        ((IConfiguration)Config).GetReloadToken().RegisterChangeCallback(Config_Changed_trap, default);
    }
#pragma warning restore CS1998 // O método assíncrono não possui operadores 'await' e será executado de forma síncrona


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
