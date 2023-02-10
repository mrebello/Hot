using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Runtime.Versioning;
using static Hot.HotConfiguration;

namespace Hot.Loggers {
    public class FileLoggerConfiguration {
        string _filename = "";

        public string filename {
            get => _filename;
            set {
                _filename = value;
                if (_filename.StartsWith('\\')
                 || _filename.StartsWith('/')
                 || (_filename.Length >= 2 && _filename[1] == ':')) {

                }
                else {
                    _filename = Path.GetDirectoryName(Config[ConfigConstants.ExecutableFullName]) + Path.DirectorySeparatorChar + _filename;
                }
                _filename = _filename.ExpandConfig();   // faz expansões de configuração
            }
        }
        public LogLevel Level { get; set; }
    }

    public sealed class FileLogger : ILogger {
        private readonly string _name;
        private readonly Func<FileLoggerConfiguration> _getCurrentConfig;
        private const int EventIdErro_ao_gravar_arquivo = 10998;

        public FileLogger(string name, Func<FileLoggerConfiguration> getCurrentConfig) {
            _name = name;
            _getCurrentConfig = getCurrentConfig;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _getCurrentConfig().Level;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (!this.IsEnabled(logLevel)) return;

            if (eventId == EventIdErro_ao_gravar_arquivo) return;   // Evita recursividade na chamada.

            var config = _getCurrentConfig();
            if (String.IsNullOrEmpty(config.filename)) return;

            try {
                string corpo =
@$"Message: {formatter(state, exception)}
Exception: {exception?.ToString()}
EventId: {eventId.ToString()}
State: {state?.ToString()}";

                File.AppendAllText(config.filename, corpo);
            }
            catch (Exception e) {
                Log.LogError(EventIdErro_ao_gravar_arquivo, e, "Erro ao tentar gravar arquivo de log.");
            }
            //Console.WriteLine($"Estou logando: {formatter(state, exception)}");

        }
    }


    [UnsupportedOSPlatform("browser")]
    [ProviderAlias("file")]
    public class FileLoggerProvider : ILoggerProvider {
        private readonly IDisposable _onChangeToken;
        private FileLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

        public FileLoggerProvider(IOptionsMonitor<FileLoggerConfiguration> config) {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => {
                // mudado nome do arquivo, mais nada a fazer
                _currentConfig = updatedConfig;
            });
        }

        private FileLoggerConfiguration GetCurrentConfig() => _currentConfig;
        ILogger ILoggerProvider.CreateLogger(string categoryName) {
            return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, GetCurrentConfig));
        }

        public void Dispose() {
            _loggers.Clear();
        }
    }


    public static class FileLoggerExtensions {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder) {
            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<FileLoggerConfiguration, FileLoggerProvider>(builder.Services);
            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<FileLoggerConfiguration> configure) {
            builder.AddFileLogger();
            builder.Services.Configure(configure);
            return builder;
        }
    }
}
