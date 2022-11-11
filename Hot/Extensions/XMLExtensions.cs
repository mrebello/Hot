namespace Hot.Extensions {
    public class XMLExtensions {
        /// <summary>
        /// Checa assinatura do XML se é válida.
        /// *** acertar tipo de retorno e incluir certificado ICP hardcode
        /// </summary>
        /// <param name="XMLfile"></param>
        /// <returns></returns>
        static public int CheckSignature(string XMLfile) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            xmlDoc.Load(XMLfile);
            SignedXml signedXml = new SignedXml(xmlDoc);
            XmlNodeList nodeList = xmlDoc.GetElementsByTagName("Signature");
            XmlNodeList certificates = xmlDoc.GetElementsByTagName("X509Certificate");
            if (nodeList.Count == 0) {
                return 1;  // nenhuma assinatura encontrada no XML
            } else if (certificates.Count == 0) {
                return 2;  // nenhum certificado encontrado no XML
            } else {
#pragma warning disable CS8602 // Desreferência de uma referência possivelmente nula.
                X509Certificate2 dcert2 = new X509Certificate2(Convert.FromBase64String(certificates[0].InnerText));
#pragma warning restore CS8602 // Desreferência de uma referência possivelmente nula.
                int r = 0; // assinaturas válidas
                foreach (XmlElement element in nodeList) {
                    signedXml.LoadXml(element);
                    if (!signedXml.CheckSignature(dcert2, true)) {
                        r = 3; // alguma assinatura não é válida
                        break;
                    }
                }
                return r;
            }
        }
    }
}
