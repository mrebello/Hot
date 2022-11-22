namespace Hot {
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
        public Assembly GetAsmRessource { get => asm_resource; }
        
        /// <summary>
        /// Retorna com stream incorporado dentro do assembler, adicionando o pré-nome do assembler.
        /// </summary>
        /// <param name="sub_name"></param>
        /// Nome do do stream a pegar (assemblyname + "." + sub_name)
        /// <returns></returns>
        public Stream? GetAsmStream(string sub_name) => asm_resource.GetManifestResourceStream(asm_resource.GetName().Name + "." + sub_name);

        /// <summary>
        /// Faz com que _configuration seja inicializado ao ler configuration 
        /// </summary>
        public static IConfiguration configuration { get => config.Config; }
        static object InicializaLock = new object();

        public static string configSearchPath = "";
        public string this[string key] { get => _configuration[key]; set => _configuration[key] = value; }
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

                    // **** Linha abaixo criada com configuração 'HARDCODDED'. Deve ser excluída.
                    env = Environment.MachineName.StartsWith("RS-DS") ? Environments.Development : env;
                    // ****

                    env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? env;
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

                    configSearchPath += "- appsettings.json  (current directory)" + Environment.NewLine;
                    confBuilder.AddJsonFile("appsettings.json", true, true);

                    configSearchPath += "- /etc/" + asm_name + ".conf" + Environment.NewLine;
                    confBuilder.AddJsonFile("/etc/" + asm_name + ".conf", true, true);  // em /etc pega pelo nome 'formal' do assembler

                    configSearchPath += "- " + executable_name + ".conf  (current directory)" + Environment.NewLine;
                    confBuilder.AddJsonFile(executable_name + ".conf", true, true);

                    configSearchPath += "- appsettings." + env + ".json  (current directory)" + Environment.NewLine;
                    confBuilder.AddJsonFile($"appsettings.{env}.json", true, true);

                    configSearchPath += "- command line parameters" + Environment.NewLine;
                    confBuilder.AddCommandLine(Environment.GetCommandLineArgs());

                    if (env == Environments.Development) {
                        configSearchPath += "AddSecrets adicionado.";
                        confBuilder.AddUserSecrets(asm_resource, true);
                    }
                    _configuration = confBuilder.Build();

                    // Após ler configurações, analisa ambiente onde está
#if (DEBUG)
                    _configuration["Configuration"] = "Debug";
#elif (RELEASE)
                    _configuration["Configuration"] = "Release";
#endif
                    _configuration["Environment"] = env;

                    if (_configuration["AppName"] == null) {
                        _configuration["AppName"] = (executable_name ?? asm_name ?? "").TrimEnd(".exe");
                    }
                    _configuration["Version"] = asm_resource.GetName().Version?.ToString();
                    _configuration["ExecutableFullName"] = executable_fullname;

#if NETCOREAPP1_0_OR_GREATER
                    _configuration["IsWindows"] = System.OperatingSystem.IsWindows().ToString();
                    _configuration["IsLinux"] = System.OperatingSystem.IsLinux().ToString();
#else
                    _configuration["IsWindows"] = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows).ToString();
                    _configuration["IsLinux"] = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux).ToString();
#endif
                }
            }
        }
    }




    // Constantes de parâmetros:
    //public partial class Parametros {
    //    public static readonly string ERP_ConnectionString = "ConnectionString:ERP";
    //}

    //    #region Parametro

    //    static Dictionary<string, string> _Parametros = new Dictionary<string, string>();

    //    /// <summary>
    //    /// Devolve o parâmetro de configuração para a aplicação dentro do sistema.
    //    /// 
    //    /// </summary>
    //    /// <param name="Nome_Parametro"></param>
    //    /// <returns></returns>
    //    public static string Parametro(string Nome_Parametro) {
    //        string r;
    //        if (!_Parametros.TryGetValue(Nome_Parametro, out r)) {
    //            var o = BD.SQLScalar(BD.TBFG_connection(),
    //                        "exec Le_Parametro @1, @2",
    //                        Nome_Parametro,
    //                        (int)Get_Tipo_Ambiente());
    //            r = (string)o;
    //            _Parametros.Add(Nome_Parametro, r);

    //            Log.Warn("Parâmetro lido \"{0}\" = {1}", Nome_Parametro, r);
    //        }
    //        return r;
    //    }

    //    #endregion Parametro
}
