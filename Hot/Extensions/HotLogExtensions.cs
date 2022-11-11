namespace Hot.Extensions.HotLogExtensions {
    /// <summary>
    /// Extensão para HotLog provendo funções de log com LAMBDA.
    /// <b>Objetivo:</b> Usar funções com lambda para evitar perda de desempenho em chamadas longas caso o log esteja desativado
    /// </summary>
    public static class HotLogExtensions {
        /// <summary>
        /// Grava uma mensagem de log, gerada apenas quando o nível do log é >= <b>Trace</b>.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("valor: {0}", Função_demorada());"
        /// </code>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="lambda_message">Gerador da mensagem do log</param>
        public static void LogTrace(this ILogger logger, Func<TLogMsg> lambda_message) {
            if (logger.IsEnabled(LogLevel.Trace)) logger.Log(LogLevel.Trace, lambda_message().Get());
        }
        /// <summary>
        /// Grava uma mensagem de log, gerada apenas quando o nível do log é >= <b>Trace</b>.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("valor: {0}", Função_demorada());"
        /// </code>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="lambda_message">Gerador da mensagem do log</param>
        public static void LogDebug(this ILogger logger, Func<TLogMsg> lambda_message) {
            if (logger.IsEnabled(LogLevel.Debug)) logger.Log(LogLevel.Debug, lambda_message().Get());
        }
        /// <summary>
        /// Grava uma mensagem de log, gerada apenas quando o nível do log é >= <b>Trace</b>.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("valor: {0}", Função_demorada());"
        /// </code>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="lambda_message">Gerador da mensagem do log</param>
        public static void LogInformation(this ILogger logger, Func<TLogMsg> lambda_message) {
            if (logger.IsEnabled(LogLevel.Information)) logger.Log(LogLevel.Information, lambda_message().Get());
        }
        /// <summary>
        /// Grava uma mensagem de log, gerada apenas quando o nível do log é >= <b>Trace</b>.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("valor: {0}", Função_demorada());"
        /// </code>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="lambda_message">Gerador da mensagem do log</param>
        public static void LogWarning(this ILogger logger, Func<TLogMsg> lambda_message) {
            if (logger.IsEnabled(LogLevel.Warning)) logger.Log(LogLevel.Warning, lambda_message().Get());
        }
        /// <summary>
        /// Grava uma mensagem de log, gerada apenas quando o nível do log é >= <b>Trace</b>.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("valor: {0}", Função_demorada());"
        /// </code>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="lambda_message">Gerador da mensagem do log</param>
        public static void LogError(this ILogger logger, Func<TLogMsg> lambda_message) {
            if (logger.IsEnabled(LogLevel.Error)) logger.Log(LogLevel.Error, lambda_message().Get());
        }
        /// <summary>
        /// Grava uma mensagem de log, gerada apenas quando o nível do log é >= <b>Trace</b>.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("valor: {0}", Função_demorada());"
        /// </code>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="lambda_message">Gerador da mensagem do log</param>
        public static void LogCritical(this ILogger logger, Func<TLogMsg> lambda_message) {
            if (logger.IsEnabled(LogLevel.Critical)) logger.Log(LogLevel.Critical, lambda_message().Get());
        }



        /// <summary>
        /// Gera uma mensagem de log com String.Format, para ser usada na função lambda.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("valor: {0}", Função_demorada());"
        /// </summary>
        /// <param name="log"></param>
        /// <param name="msg"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static TLogMsg Msg(this HotLog _1, string msg, params object[] obj) => new(String.Format(msg, obj));

        /// <summary>
        /// Gera uma mensagem de log sem String.Format, para ser usada na função lambda.
        /// <code>
        /// Log.LogTrace(()=> Log.Msg("nome_com_{: " + Função_demorada());"
        /// </summary>
        /// <param name="log"></param>
        /// <param name="msg"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static TLogMsg Msg(this HotLog _1, string msg) => new(msg);


        /// <summary>
        /// Cronometra o tempo de execução de uma rotina.
        /// Loga mensagem com o tempo de execução.
        /// </summary>
        /// <param name="log"></param>
        /// <param name="f"></param>
        /// <param name="message"></param>
        /// <param name="level"></param>
        /// <returns>milisegundos da execução</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static long Cron(this ILogger log, Action f, string message = "Cron:", LogLevel level = LogLevel.Information) {
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (f == null) throw new ArgumentNullException(nameof(f));
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            f();
            stopWatch.Stop();
            long t = stopWatch.ElapsedMilliseconds;
            TimeSpan ts = stopWatch.Elapsed;
            log.Log(level, message + " {0:00}:{1:00}:{2:00}.{3:000}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            return t;
        }

    }

    /// <summary>
    /// Classe (tipo) para lambda de Log.info
    /// </summary>
    public class TLogMsg {
        readonly string m;
        public TLogMsg(string m) {
            this.m = m;
        }
        public string Get() => m;
    }
}
