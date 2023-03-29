namespace Hot;

/// <summary>
/// Define um IConfiguration global, lendo as seguintes configurações:
/// - Ambiente de desenvolvimento "Development" definido via variável de ambiente nas propriedades de depuração (DOTNET_ENVIRONMENT=Development)
///     Se não definido na variável de ambiente, checa nome da máquina (StartsWith("RS-DS") = Development
///     e checa parâmetros
/// - Arquivo AppSettings.JSON está incorporado no APP para definir as configurações padrões
/// - Procura configuração também em {exename}.conf e em /etc/{assemblername}.conf antes da linha de comando
/// - Linha de comando = appupdate.exe /MySetting:SomeValue=123
/// - Environment = set Logging__LogLevel__Microsoft=Information      (__ ao invés de : na variável de ambiente)
/// (no Linux, o provedor de log Debug é distribution-dependent e pode ser: /var/log/message or /var/log/syslog)
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
        if (passwd != 675272) throw new Exception("ESTE MÉTODO NÃO DEVE SER USADO PELO USUÁRIO DA BIBLIOTECA.");
        _serviceprovider = serviceProvider;
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
    private static Assembly asm_resource;
#pragma warning restore CS8618

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
    public Stream? GetAsmStream(string sub_name) => asm_resource.GetManifestResourceStream(asm_resource.GetName().Name + "." + sub_name);

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
    public string this[string key] { 
        get => _configuration[key];
        set => onlineProvider.Set(key, value); // _configuration[key] = value;
    }
    public void OnReload() => onlineProvider.OnReload();
    IEnumerable<IConfigurationSection> IConfiguration.GetChildren() => _configuration.GetChildren();
    IChangeToken IConfiguration.GetReloadToken() => _configuration.GetReloadToken();
    IConfigurationSection IConfiguration.GetSection(string key) => _configuration.GetSection(key);

#pragma warning disable CS8618 // O campo não anulável precisa conter um valor não nulo ao sair do construtor. Considere declará-lo como anulável.
    public HotConfiguration() {
        lock (InicializaLock) {
            if (_configuration == null) {
                //var executable_fullname = System.Environment.GetCommandLineArgs()[0];  // devolve o nome da DLL para aplicativos empacotados em arquivo único
                var executable_fullname = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
                var executable_name = Path.GetFileNameWithoutExtension(executable_fullname);
                var executable_path = Path.GetDirectoryName(System.Environment.GetCommandLineArgs()[0]) + Path.DirectorySeparatorChar;
                //Assembly asm_executing = System.Reflection.Assembly.GetExecutingAssembly();
                // Ao invés do acima, pega o frame mais alto do stackFrame atual
                var stackFrame = new System.Diagnostics.StackTrace(1);
                asm_resource = stackFrame.GetFrame(stackFrame.FrameCount - 1)?.GetMethod()?.ReflectedType?.Assembly ??
                    System.Reflection.Assembly.GetExecutingAssembly();

                var asm_name = asm_resource.GetName().Name;
                // pega environment sem usar 'host'. Prioridades:
                // 1 - nome da máquina (se inicia com RS-DS é Development)
                // 2 - variável de ambiente
                // 3 - linha de comando
                string env = Environments.Production;

                //// **** Linha abaixo criada com configuração 'HARDCODDED'. Deve ser excluída.
                //env = Environment.MachineName.StartsWith("RS-DS") ? Environments.Development : env;
                //// ****

                // Ambiente definido ou em variáveis de ambiente ou na linha de comando
                env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? env;
                var ee = Environment.GetCommandLineArgs().Where((s) => s.ToUpper().StartsWith("ENVIRONMENT"));
                if (ee.Count() > 0) {
                    env = ee.Last().After("=");
                }

                var confBuilder = new ConfigurationBuilder();
                confBuilder.SetBasePath(Directory.GetCurrentDirectory());

                configSearchPath += "- default values embedded in executable" + Environment.NewLine;
                var appsttings_embedded = asm_resource.GetManifestResourceStream(asm_name + ".appsettings.json");
                if (appsttings_embedded == null) throw new ConfigurationErrorsException("appsettings.json deve ser recurso inserido.");
                confBuilder.AddJsonStream(appsttings_embedded);

                configSearchPath += "- environment variables started with DOTNET_" + Environment.NewLine;
                confBuilder.AddEnvironmentVariables(prefix: "DOTNET_");

                configSearchPath += "- environment variables started with ASPNETCORE_" + Environment.NewLine;
                confBuilder.AddEnvironmentVariables(prefix: "ASPNETCORE_");
                
                void add_file(string f) {
                    configSearchPath += "- " + f + Environment.NewLine;
                    confBuilder.AddJsonFile(f, true, true);
                }

                add_file($"{executable_path}appsettings.json");
                add_file($"/etc/{asm_name}.conf");
                add_file($"{executable_path}{executable_name}.conf");
                add_file($"{executable_path}appsettings.{env}.json");

                configSearchPath += "- command line parameters" + Environment.NewLine;
                confBuilder.AddCommandLine(Environment.GetCommandLineArgs());

                if (env == Environments.Development) {
                    configSearchPath += "AddSecrets adicionado.";
                    confBuilder.AddUserSecrets(asm_resource, true);
                }

                configSearchPath += "- runtime defined values" + Environment.NewLine;
                confBuilder.Add(onlineProvider);

                // -- Build configuration --

                _configuration = confBuilder.Build();

                // Após ler configurações, analisa ambiente onde está e coloca valores em configurações 
#if (DEBUG)
                _configuration[ConfigConstants.Configuration] = "Debug";
#elif (RELEASE)
                _configuration[ConfigConstants.Configuration] = "Release";
#endif
                _configuration[ConfigConstants.Environment] = env;

                if (_configuration[ConfigConstants.AppName] == null) {
                    _configuration[ConfigConstants.AppName] = (executable_name ?? asm_name ?? "").TrimEnd(".exe");
                }
                _configuration[ConfigConstants.Version] = asm_resource.GetName().Version?.ToString();
                _configuration[ConfigConstants.ExecutableFullName] = executable_fullname;

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
            if (!String.IsNullOrEmpty(key)) Data[key] = value;
            if (reload) base.OnReload();
        }

        public new void OnReload() => OnReload();
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

    }
}
