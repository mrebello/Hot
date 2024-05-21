using Microsoft.Extensions.Hosting;

namespace Hot;

/// <summary>
/// Define um IConfiguration global, lendo as seguintes configurações:
/// - Ambiente de desenvolvimento "Development" definido via variável de ambiente nas propriedades de depuração (DOTNET_ENVIRONMENT=Development)
///     Se não definido na variável de ambiente, checa parâmetros da linha de comando e appsettings.json incorporado na aplicação
/// - Arquivo AppSettings.JSON está incorporado no APP para definir as configurações padrões
/// - Procura configuração nos "Include" do appsettings incorporado na aplicação
/// (no Linux, o provedor de log Debug é distribution-dependent e pode ser: /var/log/message or /var/log/syslog)
/// EXPANDE os valores de configuração na leitura do valor (com Config e Environment)
/// </summary>
public class HotConfiguration : IConfiguration {
    /// <summary>
    /// Classe pública para acesso às configurações diretamente com
    /// <code>using static HotConiguration.conf.Config</code>
    /// </summary>
    public class config {
        public readonly static HotConfiguration Config = new HotConfiguration();
        public static void HotConfiguration_Init() { }  // Provoca chamada do construtor
    }

    /// <summary>
    /// Variável com o serviceprovider configurado no HostBuilder/WebApplication.
    /// </summary>
    private static IServiceProvider? _serviceprovider;

    /// <summary>
    /// Seta service provider devolvido pela configuração. A ser usado nos HostBuilder da HotAPI internamente.
    /// NÃO deve ser chamado pelo usuário da biblioteca. Possui atributo para não aparecer no IntelliSense.
    /// </summary>
    /// <param name="serviceProvider">Servico a ser ajustado</param>
    /// <param name="passwd">Número de senha interno a ser passado - para garantir que não será chamado pelo usuário.</param>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void __Set_ServiceProvider(IServiceProvider serviceProvider, int passwd) {
        if (passwd != 675272)
            throw new Exception("ESTE MÉTODO NÃO DEVE SER USADO PELO USUÁRIO DA BIBLIOTECA.");
        _serviceprovider = serviceProvider;
    }

    private static ILogger? LogHC = null;
    /// <summary>
    /// Flag para indicar se já ativou o Log (Primeiro inicializa as confs, depois inicializa o Log).
    /// NÃO deve ser chamado pelo usuário da biblioteca. Possui atributo para não aparecer no IntelliSense.
    /// </summary>
    /// <param name="passwd">Número de senha interno a ser passado - para garantir que não será chamado pelo usuário.</param>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static void __Set_LogOk(int passwd) {
        if (passwd != 675272)
            throw new Exception("ESTE MÉTODO NÃO DEVE SER USADO PELO USUÁRIO DA BIBLIOTECA.");
        try {
            LogHC = Log.Create("HotConfig");
        } catch (Exception) {
        }
    }

    /// <summary>
    /// Devolve o ServiceProvider da aplicação (definido no AppBuild ou, caso não exista, o genérico do sistema)
    /// </summary>
    public IServiceProvider ServiceProvider {
        get {
            if (_serviceprovider == null) {
                HotConfiguration_Init();
                HotLog_Init();
                var host = new HostBuilder()
                    .ConfigureServices(services => services
                        .AddSingleton<IConfiguration, HotConfiguration>()
                        .AddSingleton<ILogger, HotLog>()
                     ).Build();
                _serviceprovider = host.Services;
            }
            return _serviceprovider;
        }
    }

    /// <summary>
    /// Variável com provedor de configurações online para permitir disparar OnReload()
    /// </summary>
    private static OnlineProvider onlineProvider = new OnlineProvider();


#pragma warning disable CS8618 // Tratado. O campo não anulável precisa conter um valor não nulo ao sair do construtor.
    // Implementação com variável local static para 'singletron'
    private static IConfiguration _configuration;

    /// <summary>
    /// Variável com o assembly que contém os recursos embutidos da aplicação
    /// </summary>
    public static Assembly asm_resource;

    /// <summary>
    /// Variável com o assembly que contém os recursos embutidos da HotLib
    /// </summary>
    public static Assembly asmHot_resource;
#pragma warning restore CS8618

    /// <summary>
    /// Variável com o assembly que contém os recursos embutidos da HotAPI, caso esteja disponível
    /// </summary>
    public static Assembly? asmHotAPI_resource;

    /// <summary>
    /// Devolve o Assembly da aplicação (trata publicação em arquivo único)
    /// </summary>
    public Assembly GetAsmResource { get => asm_resource; }

    /// <summary>
    /// Retorna com stream incorporado dentro do assembler, adicionando o pré-nome do assembler.
    /// </summary>
    /// <param name="sub_name"></param>
    /// Nome do do stream a pegar (sem o assemblyname)
    /// <returns></returns>
    public Stream? GetAsmStream(string sub_name) => asm_resource.GetAsmStream(sub_name);

    /// <summary>
    /// Retorna com stream incorporado dentro da lib Hot, adicionando o pré-nome do assembler.
    /// </summary>
    /// <param name="sub_name"></param>
    /// Nome do do stream a pegar (sem o assemblyname)
    /// <returns></returns>
    public Stream? GetLibStream(string sub_name) => Assembly.GetAssembly(typeof(SelfHostedService))?.GetManifestResourceStream("Hot." + sub_name);

    /// <summary>
    /// Retorna com informações sobre a aplicação e o ambiente de execução atual
    /// </summary>
    /// <returns></returns>
    public string Infos() => $@"Infos:
        Plataform = {Environment.OSVersion.Platform}
        IsWindows = {OperatingSystem.IsWindows()}
        IsLinux = {OperatingSystem.IsLinux()}
        Debug = {Debugger.IsAttached}
        Configuração = {Config[ConfigConstants.Configuration]}
        Ambiente = {Config[ConfigConstants.Environment]}
        Culture = {System.Globalization.CultureInfo.CurrentCulture}
        AppName = {Config[ConfigConstants.AppName]}
        Executable = {Config[ConfigConstants.ExecutableFullName]}
        Service name = {Config[ConfigConstants.ServiceName]}
        Service Display name = {Config[ConfigConstants.ServiceDisplayName]}
        Service description = {Config[ConfigConstants.ServiceDescription]}
        Config search path = {HotConfiguration.configSearchPath}";


    /// <summary>
    /// Faz com que _configuration seja inicializado ao ler configuration 
    /// </summary>
    public static IConfiguration configuration { get => config.Config; }
    static object InicializaLock = new object();

    public static string configSearchPath = "";
    /// <summary>
    /// Lê/grava valor de configuração. Na leitura, expande valores de configuração %(xxx)% e ambiente %envvar%.
    /// </summary>
    /// <param name="key">Nome da chave a ler/gravar o valor</param>
    /// <returns>Valor da chave lida, expandida.</returns>
    public string? this[string key] {
        get {
            LogHC?.LogTrace(() => Log.Msg("get Config[\"" + key + "\"] = " + _configuration[key]));
            return _configuration[key]?.ExpandConfig(_configuration);
        }
        set {
            LogHC?.LogTrace(() => Log.Msg("set Config[\"" + key + "\"] = " + value));
            onlineProvider.Set(key, value ?? ""); // _configuration[key] = value;
        }
    }
    public void OnReload() => onlineProvider.OnReload();
    public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetChildren();
    public IChangeToken GetReloadToken() => _configuration.GetReloadToken();
    public IConfigurationSection GetSection(string key) => _configuration.GetSection(key);

#pragma warning disable CS8618 // O campo não anulável precisa conter um valor não nulo ao sair do construtor. Considere declará-lo como anulável.
    public HotConfiguration() {
        lock (InicializaLock) {
            if (_configuration == null) {
                string[] cmdline_args = Environment.GetCommandLineArgs();
                // Primeiro parâmetro é o nome do executável, então, definir como "". Se for "dotnet xxx", zerar os 2 primeiros
                if (IsDotNET(cmdline_args[0])) cmdline_args[1] = "";
                cmdline_args[0] = "";
                //var executable_fullname = System.Environment.GetCommandLineArgs()[0];  // devolve o nome da DLL para aplicativos empacotados em arquivo único
                var executable_fullname = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
                if (IsDotNET(executable_fullname) || IsIIS(executable_fullname))
                    executable_fullname = System.Environment.GetCommandLineArgs()[0];
                var executable_path = Path.GetDirectoryName(System.Environment.GetCommandLineArgs()[0]) + Path.DirectorySeparatorChar;
                bool IsDotnetCmd = false;
                if (IsDotNET(executable_fullname)) {  // se chamado explicitamente com "dotnet xxx"
                    IsDotnetCmd = true;
                    executable_fullname = System.Environment.GetCommandLineArgs()[1];
                    executable_path = Path.GetDirectoryName(executable_fullname);
                    if (executable_path.IsEmpty()) {
                        executable_path = Directory.GetCurrentDirectory();
                        executable_fullname = executable_path + Path.DirectorySeparatorChar + executable_fullname;
                    }
                    executable_path += Path.DirectorySeparatorChar;
                }
                var executable_name = Path.GetFileNameWithoutExtension(executable_fullname);
                //Assembly asm_executing = System.Reflection.Assembly.GetExecutingAssembly();
                // Ao invés do acima, pega o frame mais alto do stackFrame atual
                var stackFrame = new System.Diagnostics.StackTrace(1);
                asm_resource = stackFrame.GetFrame(stackFrame.FrameCount - 1)?.GetMethod()?.ReflectedType?.Assembly
                    ?? System.Reflection.Assembly.GetExecutingAssembly();
                asmHot_resource = typeof(HotConfiguration).Assembly;
                asmHotAPI_resource = AppDomain.CurrentDomain.GetAssemblies().Where((i) => i.FullName?.StartsWith("HotAPI,") ?? false)?.FirstOrDefault();
                var asm_name = asm_resource.GetName().Name;

                var appsttings_embeddedHot = asmHot_resource.GetManifestResourceStream("Hot.appsettings.json")
                    ?? throw new ConfigurationErrorsException("appsettings.json da HotLib não encontrado!");
                Stream? appsttings_embeddedHotAPI = null;
                if (asmHotAPI_resource is not null) {
                    appsttings_embeddedHotAPI = asmHotAPI_resource.GetManifestResourceStream("HotAPI.appsettings.json")
                        ?? throw new ConfigurationErrorsException("appsettings.json da HotAPI não encontrado!");
                }
                var appsttings_embedded = asm_resource.GetAsmStream("appsettings.json")
                    ?? throw new ConfigurationErrorsException("appsettings.json deve ser recurso inserido.");


                #region Processamento da Pré-configuração (appsettings embutidos, variáveis de ambiente e linha de comando apenas)

                var preconfBuilder = new ConfigurationBuilder();
                preconfBuilder.SetBasePath(Directory.GetCurrentDirectory());
                preconfBuilder.AddJsonStream(asmHot_resource.GetManifestResourceStream("Hot.appsettings.json")!);  // outro stream para não dar conflito
                if (asmHotAPI_resource is not null) preconfBuilder.AddJsonStream(asmHotAPI_resource.GetManifestResourceStream("HotAPI.appsettings.json")!);
                preconfBuilder.AddJsonStream(asm_resource.GetAsmStream("appsettings.json")!);
                preconfBuilder.AddEnvironmentVariables(prefix: "DOTNET_");
                preconfBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_");
                preconfBuilder.AddCommandLine(cmdline_args);
                var preconf = preconfBuilder.Build();

                // Ambiente definido na pré-configuração
                string env = preconf["Environment"] ?? Environments.Production;

                preconf[ConfigConstants.Environment] = env;
                preconf[ConfigConstants.IsDevelopment] = (env == Environments.Development).ToStr();
                preconf[ConfigConstants.AppName] ??= (executable_name ?? asm_name ?? "").TrimEnd(".exe");
                preconf[ConfigConstants.Version] = asm_resource.GetName().Version?.ToString();
                preconf[ConfigConstants.ExecutableFullName] = executable_fullname;
                preconf[ConfigConstants.ExecutablePath] = executable_path;
                preconf[ConfigConstants.ExecutableName] = executable_name;
                preconf[ConfigConstants.IsDotnetCmd] = IsDotnetCmd.ToStr();

                // Includes definido na pré-configuração
                var includeItems = preconf.GetSection("Includes").Get<List<string>>();

                #endregion


                var confBuilder = new ConfigurationBuilder();
                confBuilder.SetBasePath(Directory.GetCurrentDirectory());

                confBuilder.AddJsonStream(appsttings_embeddedHot); // Le appsettings da HotLib (que possui os valores defaults)
                if (asmHotAPI_resource is not null) confBuilder.AddJsonStream(appsttings_embeddedHotAPI!); // Le appsettings da HotAPI, se disponíveis (que possui os valores defaults)

                configSearchPath += "- default values embedded in executable" + Environment.NewLine;
                confBuilder.AddJsonStream(appsttings_embedded);

                configSearchPath += "- environment variables started with DOTNET_" + Environment.NewLine;
                confBuilder.AddEnvironmentVariables(prefix: "DOTNET_");

                configSearchPath += "- environment variables started with ASPNETCORE_" + Environment.NewLine;
                confBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_");

                #region Processa "Include" na configuração  ***** Implementação não em ordem  - deveria estar no arquivo de configuração
                {
                    if (includeItems != null) {
                        foreach (var item in includeItems) {
                            var i = item.ExpandConfig(preconf);
                            //var i = Environment.ExpandEnvironmentVariables(item);
                            configSearchPath += "- " + i + ": ";
                            if (Directory.Exists(i) || i.Contains('?') || i.Contains('*')) {
                                string[] files;
                                if (i.Contains('?') || i.Contains('*')) {
                                    configSearchPath += "wildcards" + Environment.NewLine;
                                    files = Directory.GetFiles(Path.GetDirectoryName(i)!, Path.GetFileName(i));
                                } else {
                                    configSearchPath += "directory" + Environment.NewLine;
                                    files = Directory.GetFiles(i, "*.conf", SearchOption.AllDirectories);
                                }
                                foreach (var f in files) {
                                    configSearchPath += "     " + f + Environment.NewLine;
                                    confBuilder.AddJsonFile(f, true, true);
                                }
                            } else if (File.Exists(i)) {
                                confBuilder.AddJsonFile(i, true, true);
                                configSearchPath += "included" + Environment.NewLine;
                            } else
                                configSearchPath += "not exist" + Environment.NewLine;
                        }
                    }
                }
                #endregion

                configSearchPath += "- command line parameters" + Environment.NewLine;
                confBuilder.AddCommandLine(cmdline_args);
                if (env == Environments.Development) {
                    configSearchPath += "- UserSecrets (Development)" + Environment.NewLine; ;
                    confBuilder.AddUserSecrets(asm_resource, true);
                }

                configSearchPath += "- runtime defined values" + Environment.NewLine;
                confBuilder.Add(onlineProvider);

                // -- Build configuration --

                _configuration = confBuilder.Build();

#if (DEBUG)
                _configuration[ConfigConstants.Configuration] = "Debug";
#elif (RELEASE)
                _configuration[ConfigConstants.Configuration] = "Release";
#endif
                _configuration[ConfigConstants.Environment] = preconf[ConfigConstants.Environment];
                _configuration[ConfigConstants.IsDevelopment] = preconf[ConfigConstants.IsDevelopment];
                _configuration[ConfigConstants.AppName] = preconf[ConfigConstants.AppName];
                _configuration[ConfigConstants.Version] = preconf[ConfigConstants.Version];
                _configuration[ConfigConstants.ExecutableFullName] = preconf[ConfigConstants.ExecutableFullName];
                _configuration[ConfigConstants.ExecutablePath] = preconf[ConfigConstants.ExecutablePath];
                _configuration[ConfigConstants.ExecutableName] = preconf[ConfigConstants.ExecutableName];
                _configuration[ConfigConstants.IsDotnetCmd] = preconf[ConfigConstants.IsDotnetCmd];

                _configuration[ConfigConstants.ServiceName] = asm_resource.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";
                _configuration[ConfigConstants.ServiceDisplayName] = asm_resource.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";
                _configuration[ConfigConstants.ServiceDescription] = asm_resource.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";
            }
        }
    }

    public class OnlineProvider : ConfigurationProvider, IConfigurationSource {
        IConfigurationProvider IConfigurationSource.Build(IConfigurationBuilder builder) => this;
        /// <summary>
        /// Set a configuration key, with flag for reload configurations
        /// </summary>
        /// <param name="key">Name of key.</param>
        /// <example>Logging:Console:LogLevel:Default</example>
        /// <param name="value">Value</param>
        /// <example>Information</example>
        /// <param name="reload">True if is to reload configurations</param>
        public void Set(string key, string value, bool reload = false) {
            if (!String.IsNullOrEmpty(key))
                Data[key] = value;
            if (reload)
                base.OnReload();
        }

        public new void OnReload() => OnReload();
    }

    /// <summary>
    /// True if -d or --daemon in command line
    /// </summary>
    public bool IsDaemon { get; set; }

    private static bool IsDotNET(string? name) {
        if (name is null) return false;
        return Path.GetFileName(name).StartsWith("dotnet", StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool IsIIS(string? name) {
        if (name is null) return false;
        var n = Path.GetFileName(name);
        return n.StartsWith("iis.exe", StringComparison.InvariantCultureIgnoreCase) || n.StartsWith("iisexpress.exe", StringComparison.InvariantCultureIgnoreCase);
    }

}

public static partial class ConfigConstants {
    /// <summary>
    /// Devolve a configuração do sistema (Debug ou Release), definida em tempo de compilação
    /// </summary>
    public const string Configuration = "Configuration";

    /// <summary>
    /// Devolve o ambiente do sistema (Development ou Production)
    /// </summary>
    public const string Environment = "Environment";

    /// <summary>
    /// True se ambiente é Development, senão False
    /// </summary>
    public const string IsDevelopment = "IsDevelopment";

    /// <summary>
    /// Devolve o nome da aplicação (nome do executável, nome do assembler ou definido nas configurações)
    /// </summary>
    public const string AppName = "AppName";

    /// <summary>
    /// Devolve a versão da aplicação (vem do assembler)
    /// </summary>
    public const string Version = "Version";

    /// <summary>
    /// Devolve o nome completo do executável (do assembler) da aplicação
    /// </summary>
    public const string ExecutableFullName = "ExecutableFullName";

    /// <summary>
    /// Devolve o caminho completo do executável (do assembler) da aplicação
    /// </summary>
    public const string ExecutablePath = "ExecutablePath";

    /// <summary>
    /// Devolve o nome do executável sem caminho e sem extensão
    /// </summary>
    public const string ExecutableName = "ExecutableName";

    /// <summary>
    /// Configurações relacionadas ao autoupdate
    /// </summary>
    public static class Update {
        /// <summary>
        /// URL para onde será feita a conexão para o autoupdate.
        /// </summary>
        public const string URL = "Update:URL";

        /// <summary>
        /// Senha a ser verificada no autoupdate
        /// </summary>
        public const string Secret = "Update:Secret";

        /// <summary>
        /// Lista de IPs/Máscara de onde aceita autoupdate e infos
        /// </summary>
        public const string AcceptFrom = "Update:AcceptFrom";
    }

    /// <summary>
    /// Nome do serviço (vem do título no assembler da aplicação)
    /// </summary>
    public const string ServiceName = "ServiceName";

    /// <summary>
    /// Nome do serviço a ser mostrado para o usuário (vem do nome do produto no assembler da aplicação)
    /// </summary>
    public const string ServiceDisplayName = "ServiceDisplayName";

    /// <summary>
    /// Descrição do serviço (vem da descrição no assembler da aplicação)
    /// </summary>
    public const string ServiceDescription = "ServiceDescription";

    /// <summary>
    /// Lista de prefixos a serem ouvidos, separada por ';'
    /// </summary>
    public const string URLs = "urls";

    /// <summary>
    /// Prefixo a ser ignorado no início da url. (proxypass, fastcgi, etc..) usado para ignorar início do path na url no pré-processamento de /version e /update
    /// </summary>
    public const string IgnorePrefix = "IgnorePrefix";

    /// <summary>
    /// Devolve se foi executado com o wrapper de linha de comando "dotnet" (linux, normalmente)
    /// </summary>
    public const string IsDotnetCmd = "IsDotnetCmd";
}

