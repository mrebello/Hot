namespace Hot.Extensions {
    /// <summary>
    /// Extensões para System.Net.HttpListenerResponse
    /// </summary>
    public static class HttpListenerResponseExtensions {
        /// <summary>
        /// Envia byte[] como resposta e fecha a requisição
        /// </summary>
        /// <param name="response"></param>
        /// <param name="postBytes"></param>
        public static void Send(this HttpListenerResponse response, byte[] postBytes) {
            response.ContentLength64 = postBytes.Length;
            try {
                response.Close(postBytes, false);
            }
            catch (HttpListenerException e) when (e.ErrorCode == 1229) {  // conexão foi extinta pelo cliente
            }
            //using (Stream requestStream = response.OutputStream) {
            //    requestStream.Write(postBytes, 0, postBytes.Length);
            //    requestStream.Close();
            //}
        }

        /// <summary>
        /// Envia a string como resposta, definindo o tipo de conteúdo e a codificação da resposta.
        /// A string é codificada com a codificação definida.
        /// Fecha a requisição após o enviar.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="s"></param>
        /// String a ser enviada
        /// <param name="ContentType"></param>
        /// Tipo de conteúdo para o cabeçalho 'contenttype'
        /// <param name="e"></param>
        /// Codificação a ser usada. Se null, UTF8 é utilizado
        public static void Send(this HttpListenerResponse response, string s, string ContentType = "text/html", Encoding? e = null) {
            if (e == null) e = Encoding.UTF8;
            response.ContentType = ContentType + "; charset=" + e.HeaderName;
            response.Send(e.GetBytes(s));
        }

        /// <summary>
        /// Envia o conteúdo da string definindo o contenttype como application/json, usando UTF8, e fecha a requisição após o envio.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="json"></param>
        public static void Send_json(this HttpListenerResponse response, string json) {
            response.Send(json, "application/json");
        }


        /// <summary>
        /// Envia um erro como resposta. Caso WithLog = true, gera log com a mensagem do erro
        /// </summary>
        /// <param name="response"></param>
        /// <param name="s"></param>
        /// <param name="ContentType"></param>
        /// <param name="e"></param>
        public static void SendError(this HttpListenerResponse response, string StatusDescription, HttpStatusCode StatusCode = HttpStatusCode.InternalServerError, bool WithLog = false) {
            if (WithLog) Log.LogError(StatusDescription);
            response.StatusDescription = StatusDescription;
            response.StatusCode = (int)StatusCode;
            response.Close();
        }

    }
}
