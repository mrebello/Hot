namespace Hot.Extensions {
    // Extensões para System.Net.HttpWebRequest
    public static class HttpWebRequestExtensions {
        /// <summary>
        /// Envia byte[] usando método POST.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="postBytes"></param>
        public static void POST_byte(this HttpWebRequest request, byte[] postBytes) {
            request.Method = "POST";
            request.ContentLength = postBytes.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(postBytes, 0, postBytes.Length);
            requestStream.Close();
        }

        /// <summary>
        /// Envia a string usando método POST, codificando a string com o encoding definido.
        /// Se o encoding não for definido (null), usa UFT8.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="s"></param>
        /// <param name="e"></param>
        public static void POST_String(this HttpWebRequest request, string s, string ContentType = "text/html", Encoding? e = null) {
            if (e == null) e = Encoding.UTF8;
            request.ContentType = ContentType + "; charset=" + e.HeaderName;
            request.POST_byte(e.GetBytes(s));
        }

        /// <summary>
        /// Envia a string usando método POST, codigicando em UTF-8, e setando contenttype para 'application/json'
        /// </summary>
        /// <param name="request"></param>
        /// <param name="s"></param>
        public static void POST_json(this HttpWebRequest request, string s) {
                                                      //request.Accept = "application/json";
            request.POST_String(s, "application/json");
        }

        /// <summary>
        /// Devolve a resposta da requisição como string.
        /// Gera exceção em caso de erro, adicionando o conteúdo retornado na mensagem da exceção. (404, ou qualquer outro)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GetResponseString(this HttpWebRequest request) {
            HttpWebResponse response;
            try {
                response = (HttpWebResponse)request.GetResponse();
            } catch (WebException e) {
                if (e.Response != null) {
                    var rs = e.Response.GetResponseStream();
                    string r = new StreamReader(rs).ReadToEnd();
                    rs.Close();
                    if (r.Length > 0) {
                        throw new Exception("Data received: " + r, e);
                    } else {
                        throw e;
                    }
                } else throw e;
            }
            if (response.StatusCode == HttpStatusCode.OK) {
                return new StreamReader(response.GetResponseStream()).ReadToEnd();
            } else {
                throw new Exception("Error on GetResponse." + response);
            }
        }
    }
}
