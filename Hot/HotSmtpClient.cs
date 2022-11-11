namespace Hot {
    public class HotSmtpClient : SmtpClient {
        public HotSmtpClient() : base() {
            LeConf();
        }

        public void LeConf() {
            string server = Config["Smtp:Host"];
            if (String.IsNullOrWhiteSpace(server)) throw new ConfigurationErrorsException("'Smtp' deve estar configurado em appsettings.json.");
            Host = server;

            int port = Config["Smtp:Port"].ToInt();
            if (port > 0) Port = port;

            string? user = Config["Smtp:Username"];
            string? pass = Config["Smtp:Password"];
            if (!String.IsNullOrWhiteSpace(user)) {
                Credentials = new NetworkCredential(user, pass);
            }

            string? SSL = Config["Smtp:SSL"];
            if (!String.IsNullOrWhiteSpace(SSL)) {
                EnableSsl = SSL.ToBool();
            }

            ((IConfiguration)Config).GetReloadToken().RegisterChangeCallback(state => LeConf(), default);
        }


        public void SendMessage(MailMessage m) {
            Send(m);
        }

        public void SendHTML(string html, MailMessage m) {
            m.Body = html;
            m.IsBodyHtml = true;
            SendMessage(m);
        }

        public void SendHTML(string html, string recipients, string? subject, string? from = null) {
            string f = from ?? Config["Smtp:From"];
            SendHTML(html, new MailMessage(f, recipients, subject, null));
        }
    }
}
