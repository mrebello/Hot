namespace Hot.Extensions {
    /// <summary>
    /// Extensões para System.Net.HttpListenerRequest
    /// </summary>
    public static class HttpListenerRequestExtensions {
        /// <summary>
        /// Devolve o IP de origem da requisição, considerendo o X-Forwarded-For quando existir.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string IP_Origem(this HttpListenerRequest p) {
            return p.Headers["X-Forwarded-For"] ?? p.RemoteEndPoint.Address.ToString();
        }
    }
}
