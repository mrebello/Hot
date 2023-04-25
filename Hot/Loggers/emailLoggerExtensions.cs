using Hot.Loggers;

namespace Hot;

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

