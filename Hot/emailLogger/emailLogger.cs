using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Runtime.Versioning;

namespace Hot.emailLogger {
    public class emailLoggerConfiguration {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool? SSL { get; set; }
        public string To { get; set; } = "";
        public string From { get; set; } = "";
        public LogLevel Level { get; set; }

        public SmtpClient? SmtpClient = null;
    }

    public sealed class emailLogger : ILogger {
        private readonly string _name;
        private readonly Func<emailLoggerConfiguration> _getCurrentConfig;
        private const int EventIdErro_ao_enviar_email = 10999;

        public emailLogger(string name, Func<emailLoggerConfiguration> getCurrentConfig) {
            _name = name;
            _getCurrentConfig = getCurrentConfig;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _getCurrentConfig().Level;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (!this.IsEnabled(logLevel)) return;

            if (eventId == EventIdErro_ao_enviar_email) return;   // Evita redundância na chamada.

            var config = _getCurrentConfig();
            try {
                if (config.SmtpClient == null) {
                    config.SmtpClient = new SmtpClient() {
                        Host = config.Host,
                        Port = config.Port,
                    };
                    if (config.Username != null) config.SmtpClient.Credentials = new NetworkCredential(config.Username, config.Password);
                    if (config.SSL != null) config.SmtpClient.EnableSsl = (bool)config.SSL;
                }

                //var stringBuilder = new StringBuilder();
                //var httpContext = httpContextAccessor.HttpContext;
                //if (httpContext != null) {
                //    var request = httpContext.Request;
                //    stringBuilder.Append("User: ").Append(_userManager.GetUserName(httpContext.User)).Append("<br/>")
                //        .Append("Address: ").Append($"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}").Append("<br/>")
                //        .Append("Local IP address: ").Append(httpContext.Connection.LocalIpAddress).Append("<br/>")
                //        .Append("Remote IP address: ").Append(httpContext.Connection.RemoteIpAddress).Append("<br/>");
                //}

                if (!String.IsNullOrEmpty(config.To)) {
                    string subject = Config["AppName"] + ": " + logLevel.ToString() + " em " + DateTime.Now.ToString();
                    string corpo =
@$"Message: {formatter(state, exception)}
Exception: {exception?.ToString()}
EventId: {eventId.ToString()}
State: {state?.ToString()}";
                    using (MailMessage m = new MailMessage(config.From, config.To, subject, corpo)) {
                        config.SmtpClient.Send(m);
                    };
                }
            }
            catch (Exception e) {
                Log.LogError(EventIdErro_ao_enviar_email, e, "Erro ao tentar enviar email de erro.");
            }
            Console.WriteLine($"Estou logando: {formatter(state, exception)}");

        }
    }


    [UnsupportedOSPlatform("browser")]
    [ProviderAlias("email")]
    public class emailLoggerProvider : ILoggerProvider {
        private readonly IDisposable _onChangeToken;
        private emailLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, emailLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

        public emailLoggerProvider(IOptionsMonitor<emailLoggerConfiguration> config) {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => {
                _currentConfig.SmtpClient?.Dispose();   // força criação de novo smtpclient
                _currentConfig = updatedConfig;
            });
        }

        private emailLoggerConfiguration GetCurrentConfig() => _currentConfig;
        ILogger ILoggerProvider.CreateLogger(string categoryName) {
            return _loggers.GetOrAdd(categoryName, name => new emailLogger(name, GetCurrentConfig));
        }

        public void Dispose() {
            _loggers.Clear();
        }
    }


    public static class emailLoggerExtensions {
        public static ILoggingBuilder AddemailLogger(this ILoggingBuilder builder) {
            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, emailLoggerProvider>());
            LoggerProviderOptions.RegisterProviderOptions<emailLoggerConfiguration, emailLoggerProvider>(builder.Services);
            return builder;
        }

        public static ILoggingBuilder AddemailLogger(this ILoggingBuilder builder, Action<emailLoggerConfiguration> configure) {
            builder.AddemailLogger();
            builder.Services.Configure(configure);
            return builder;
        }
    }
}
