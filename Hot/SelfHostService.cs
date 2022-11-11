using System.Net.Http;
using System.Net.Http.Headers;

namespace Hot {
    public abstract class SelfHostedService : IHostedService {

        public abstract Task StartAsync(CancellationToken cancellationToken);
        public abstract Task StopAsync(CancellationToken cancellationToken);

        public delegate void Analisa_Parametros(string[] args);

        public static void MainDefault<service>(Analisa_Parametros? analisa_Parametros = null) where service : class, IHostedService {
            string[] args = Environment.GetCommandLineArgs();

            //var executable_name = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule!.FileName);
            var asm_name = Config.GetAsmRessource.GetName().Name;

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

            var assembly = Config.GetAsmRessource;
            //string companyName = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";
            string service_name = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";
            string display_name = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";
            string descripton = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";
            string filename = Config["AppName"];

            string infos = $@"Infos:
    Plataform = {Environment.OSVersion.Platform}
    IsWindows = {Config["IsWindows"]}
    IsLinux = {Config["IsLinux"]}
    Debug = {Debugger.IsAttached}
    Configuração = {Config["Configuration"]}
    Ambiente = {Config["Environment"]}
    AppName = {Config["AppName"]}
    Executable = {Config["ExecutableFullName"]}
    Service name = {service_name}
    Display name = {display_name}
    Service description = {descripton}
    Arquivo do serviço = {filename}
    Config search path = {HotConfiguration.configSearchPath}
";
            Log.LogInformation(infos);

            if (Environment.UserInteractive) {
                if (new[] { "/?", "-h", "-H", "-?", "--help", "/help" }.Any(args.Contains)) {
                    execute = false;
                    var help_stream = Config.GetAsmRessource.GetManifestResourceStream(asm_name + ".Help_Parameters.txt");
                    help_stream ??= Assembly.GetAssembly(typeof(SelfHostedService))?.GetManifestResourceStream("Hot.Help_Parameters.txt");
                    if (help_stream != null) Console.WriteLine(filename + " " + new StreamReader(help_stream).ReadToEnd());
                }
                else if (new[] { "/helpconfig", "--helpconfig" }.Any(args.Contains)) {
                    execute = false;
                    Console.WriteLine("Config search path:");
                    Console.WriteLine(HotConfiguration.configSearchPath);
                }
                else if (new[] { "/install", "--install" }.Any(args.Contains)) {
                    #region Install // Faz a instalação do serviço
                    // faz a instalação como serviço
                    execute = false;
                    
                    Console.WriteLine($"Install service_name: {service_name}");
                    
                    Log.LogInformation($"Install\r\n service_name: {service_name}\r\n display_name: {display_name}\r\n descripton: {descripton}\r\n file: \"{Config["ExecutableFullName"]}\"");

                    if (OperatingSystem.IsWindows()) {
                        //                    Exec(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory() + "installutil.exe", strPathLong);
                        RunProcess_SUDO("sc.exe", $"create {service_name} displayname=\"{display_name}\" start=auto binpath=\"{Config["ExecutableFullName"]}\"");
                        if (descripton.Length > 0) {
                            RunProcess_SUDO("sc.exe", $"description {service_name} \"{descripton.Replace("\"", "^\"")}\"");
                        }
                    }
                    else if (OperatingSystem.IsLinux()) {

                    }
                    else {
                        Console.Error.WriteLine("unsupported Operating System.");
                    }
                    #endregion
                }
                else if (new[] { "/uninstall", "--uninstall" }.Any(args.Contains)) {
                    #region Uninstall // faz a desinstalação como serviço
                    execute = false;

                    Console.WriteLine($"Uninstall service_name: {service_name}");

                    if (OperatingSystem.IsWindows()) {
                        //                    Exec(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory() + "installutil.exe", "/u " + strPathLong);
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
                    AutoUpdate();
                }
            }

            //if (analisa_Parametros != null) analisa_Parametros(args);
            analisa_Parametros?.Invoke(args);

            if (execute) {
                if (Environment.UserInteractive) {
                    host.RunAsync();
                    Console.WriteLine("De [Enter] para encerrar.");
                    Console.ReadLine();
                    host.StopAsync();
                }
                else {
                    host.Run();
                }
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

        public static void AutoUpdate() {
            //Thread.Sleep(1000);
            try {
                string url = Config["Update:URL"];
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

                string app_me = Config["AppName"];
                if (app_me != app_destination) {
                    throw new Exception($"App destino ({app_destination} diferente de app origem {app_me}. Não atualizado.");
                }

                string version_me = Config["Version"];
                var c = Compare_Versions(version_me, version_destination);
                if (c <= 0) {
                    string msg = c == 0 ? "Versões são iguais." : "Versão instalada é maior.";
                    throw new Exception(msg + " Não atualizado.");
                }

                Log.LogInformation($"Iniciando atualização da versão {version_destination} para a {version_me}.");

                var executable = File.OpenRead(Config["ExecutableFullName"]);
                // if (executable.Length < 4_500_000) throw new Exception("Arquivo não parece ser pacote publicado. Abortando.");

                using var fileStreamContent = new StreamContent(executable);
                // Considera possível erro de não receber resposta completa devido a shutdown do app server
                try {
                    hc.DefaultRequestHeaders.Add("UpdateSecret", Config["Update:Secret"]);
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
                    if (v.Item(2,"\t") == version_me) {
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
