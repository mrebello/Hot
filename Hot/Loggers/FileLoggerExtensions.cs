using Hot.Loggers;

namespace Hot;

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
