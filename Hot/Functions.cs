using Hot.Extensions;

namespace Hot;

/// <summary>
/// Funções estáticas genéricas de uso geral
/// </summary>
public static class Functions {
    /// <summary>
    /// Calcula módulo 11 de um número (long, para tratar até 18 dígitos)
    /// <code>
    /// CPF_check = Modulo11( CPFn(cpf_string), 2, 12)            // parâmetros para o CPF
    /// CNPJ_check = Modulo11( cnpj_sem_check, 2, 9)           // parâmetros para CNPJ
    /// PIS_ContaCorrente_Agencia_etc = Modulo11( dado, 1, 9)  // forma mais comum com 1 dígito
    /// </code>
    /// </summary>
    /// <param name="Dado">Valor sobre o qual o módulo 11 deve ser calculado.</param>
    /// <param name="NumDig">Números de dígitos de módulo 11 a calcular encadeados</param>
    /// <param name="LimMult">Limite para a multiplicação do módulo 11</param>
    /// <returns></returns>
    public static int Modulo11(long Dado, int NumDig, int LimMult) {
        int Soma, Mult, n = 1;
        long d1;
        while (n <= NumDig) {
            Soma = 0;
            Mult = 2;
            d1 = Dado;
            while (d1 > 0) {
                Soma += Mult * (int)(d1 % 10);
                Mult++;
                if (Mult > LimMult)
                    Mult = 2;
                d1 = d1 / 10;
            }
            d1 = 11 - (Soma % 11);
            if (d1 >= 10)
                d1 = 0;
            Dado = (Dado * 10) + d1;
            n++;
        }
        return (int)(Dado % (int)Math.Pow(10, NumDig));
    }

    /// <summary>
    /// Dígitos de verificação do CPF
    /// </summary>
    /// <param name="CPF"></param>
    /// <returns></returns>
    public static int CPF_DC(int CPF) {
        return Modulo11(CPF, 2, 12);
    }


    /// <summary>
    /// Retorna valor inteiro (32 bits) para o CPF, excluindo os dígitos significativos.
    /// Retorna 0 caso string não seja numérica
    /// </summary>
    /// <param name="CPF">String com CPF, com ou sem '.' e '-', com o dígito check.</param>
    /// <returns></returns>
    public static Int32 CPFn(string CPF) {
        try {
            return (Int32)(decimal.Parse(CPF.Replace(".", "").Replace("-", "")) / 100);
        } catch (Exception) {
        }
        return 0;
    }

    /// <summary>
    /// Formata um valor In32 de CPF (sem dígito check) para uma string com '.', '-', dígitos check, e zeros à esquerda (14 caracteres)
    /// </summary>
    /// <param name="CPF"></param>
    /// <returns></returns>
    public static string CPF_Completo(int CPF) {
        return CPF.ToString("000,000,000", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.') + "-" + CPF_DC(CPF).ToString("00");
    }

    /// <summary>
    /// Formata um CPF com '.', '-', dígitos check, e zeros à esquerda, recalculando os 2 dígitos verificadores
    /// </summary>
    /// <param name="CPF"></param>
    /// <returns></returns>
    public static string CPF_Formatado(string CPF) {
        if (String.IsNullOrEmpty(CPF)) {
            return "      -       ";
        } else {
            int C = CPFn(CPF);
            return CPF_Completo(C);
        }
    }


    /// <summary>
    /// Retorna com o conteúdo de um arquivo UTF-8 como string. Se arquivo não existir, retorna String.Empty
    /// Retorna string vazia caso o arquivo não exista.
    /// <b>Obs:</b> Gera exceção em arquivos maiores de 128MB - arquivos grandes devem ser tratados de forma especial
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static String Load_UTF8(String filename) {
        String r = String.Empty;
        var info = new System.IO.FileInfo(filename);
        if (info.Exists) {
            if (info.Length > int.MaxValue / 16)
                throw new Exception("Arquivo muito grande: " + filename);
            using (System.IO.StreamReader reader = new System.IO.StreamReader(filename, System.Text.Encoding.UTF8)) {
                r = reader.ReadToEnd();
                reader.Close();
            }
        }
        return r;
    }


    /// <summary>
    /// Salva uma string em um arquivo com codificação UTF-8. Tenta criar o arquivo caso não exista.
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="content"></param>
    public static void Save_UTF8(String filename, String content, bool append = false) {
        using (System.IO.StreamWriter s = new System.IO.StreamWriter(filename, append, new System.Text.UTF8Encoding(false))) {
            s.Write(content);
            s.Close();
        }
    }


    /// <summary>
    /// Encript a string usando codificação UTF8 e devolve resultado em Base64
    /// </summary>
    /// <param name="plaintext"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static string AesEncryptBase64(string plaintext, string password) {
        byte[] bytes = Encoding.UTF8.GetBytes(plaintext);
        SymmetricAlgorithm crypt = Aes.Create();
        HashAlgorithm hash = MD5.Create();
        crypt.BlockSize = 128;
        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(password));
        byte[] IV = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        return Convert.ToBase64String(crypt.EncryptCfb(bytes, IV));
    }


    /// <summary>
    /// Desencripta a string da Base64 para string de volta
    /// </summary>
    /// <param name="cryptedBase64"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static string AesDecryptBase64(string cryptedBase64, string password) {
        byte[] bytes = Convert.FromBase64String(cryptedBase64);
        SymmetricAlgorithm crypt = Aes.Create();
        HashAlgorithm hash = MD5.Create();
        crypt.BlockSize = 128;
        crypt.Key = hash.ComputeHash(Encoding.UTF8.GetBytes(password));
        byte[] IV = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        return Encoding.UTF8.GetString(crypt.DecryptCfb(bytes, IV));
    }

    /// <summary>
    /// Compara duas strings alinhadas à direita, retirando espaços e 'enter', preenchendo com '0' para que fiquem do mesmo tamanho
    /// </summary>
    /// <param name="s1"></param>
    /// <param name="s2"></param>
    /// <returns>s1&lt;s2 = -1; s1=s2 = 0; s1&gt;s2 = 1</returns>
    public static int Compare_String_RightAlign(string s1, string s2) {
        var delimiters = new char[] { ' ', '\t', '\r', '\n' };
        if (s1 == null)
            s1 = "";
        if (s2 == null)
            s2 = "";
        s1 = s1.TrimEnd(delimiters);
        s1 = s1.TrimStart(delimiters);
        s2 = s2.TrimEnd(delimiters);
        s2 = s2.TrimStart(delimiters);
        if (s1.Length > s2.Length)
            s2 = s2.PadLeft(s1.Length, '0');
        if (s2.Length > s1.Length)
            s1 = s1.PadLeft(s1.Length, '0');
        return s1.CompareTo(s2);
    }

    /// <summary>
    /// Compare two strings in 'version' format - nn.nnn.nnnn.nn
    /// </summary>
    /// <param name="version1"></param>
    /// <param name="version2"></param>
    /// <returns>-1 - version1 > version2
    /// 0  - version identical (or both is null)
    /// 1  - version2 > version 1</returns>
    public static int Compare_Versions(string version1, string version2) {
        if (version1 == null && version2 == null)
            return 0;
        if (version1 == null)
            return 1;
        if (version2 == null)
            return -1;
        int r = 0;
        while (version1.IndexOf('.') >= 0) {
            var p1 = version1.Before(".");
            var p2 = version2.Before(".");
            r = p1.CompareTo(p2);
            if (r != 0)
                return r;
            version1 = version1.After(".");
            version2 = version2.After(".");
        }
        return Compare_String_RightAlign(version1, version2);
    }


    /// <summary>
    /// Verifica se o IP está dentro do IP/Mask. Aceita IPv4 e IPv6.
    /// </summary>
    /// <param name="ipAddress">endereço a verificar</param>
    /// <param name="subnetMask">ip/mask</param>
    /// <returns></returns>
    public static bool IP_IsInSubnet(string ipAddress, string subnetMask) {
        IPAddress address = System.Net.IPAddress.Parse(ipAddress);

        var slashIdx = subnetMask.IndexOf("/");

        // 'First parse the address of the netmask before the prefix length.
        var maskAddress = System.Net.IPAddress.Parse(subnetMask.Before("/"));

        if (maskAddress.AddressFamily != address.AddressFamily)  // 'We got something like an IPV4-Address for an IPv6-Mask. This is not valid.
            return false;

        // 'Now find out how long the prefix is.
        int maskLength;
        if (slashIdx == -1) {
            maskLength = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128; // only IPv4 and IPv6 accept
        } else {
            maskLength = int.Parse(subnetMask.Substring(slashIdx + 1));
        }

        if (maskAddress.AddressFamily == AddressFamily.InterNetwork) {
            // 'Convert the mask address to an unsigned integer.
            var maskAddressArray = maskAddress.GetAddressBytes();
            Array.Reverse(maskAddressArray);
            var maskAddressBits = BitConverter.ToUInt32(maskAddressArray, 0);

            // 'And convert the IpAddress to an unsigned integer.
            var ipAddressArray = address.GetAddressBytes();
            Array.Reverse(ipAddressArray);
            var ipAddressBits = BitConverter.ToUInt32(ipAddressArray, 0);

            // 'Get the mask/network address as unsigned integer.
            uint mask = maskLength > 0 ? uint.MaxValue << (32 - maskLength) : 0;

            // 'https//stackoverflow.com/a/1499284/3085985
            // 'Bitwise And mask And MaskAddress, this should be the same as mask And IpAddress
            // 'as the end of the mask Is 0000 which leads to both addresses to end with 0000
            // 'And to start with the prefix.
            return (maskAddressBits & mask) == (ipAddressBits & mask);
        }

        if (maskAddress.AddressFamily == AddressFamily.InterNetworkV6) {
            // ' Convert the mask address to a BitArray.
            var maskAddressBits = new BitArray(maskAddress.GetAddressBytes());

            // 'And convert the IpAddress to a BitArray.
            var ipAddressBits = new BitArray(address.GetAddressBytes());

            if (maskAddressBits.Length != ipAddressBits.Length)
                return false; // throw new ArgumentException("Length of IP Address and Subnet Mask do not match.");

            // 'Compare the prefix bits.
            for (int maskIndex = 0; maskIndex <= maskLength - 1; maskIndex++) {
                if (ipAddressBits[maskIndex] != maskAddressBits[maskIndex])
                    return false;
            }

            return true;
        }

        return false;  // throw new NotSupportedException("Only InterNetworkV6 or InterNetwork address families are supported.");
    }


    /// <summary>
    /// Verifica se o IP está dentro de uma lista de IPs/Mask. Aceita IPv4 e IPv6.
    /// </summary>
    /// <param name="ipAddress">endereço a verificar</param>
    /// <param name="subnetMaskList">ip/mask;ip/mask;...</param>
    /// <returns></returns>
    public static bool IP_IsInList(string ipAddress, string subnetMaskList) {
        foreach (var s in subnetMaskList.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
            if (IP_IsInSubnet(ipAddress, s.Trim()))
                return true;
        }
        return false;
    }


    /// <summary>
    /// Start default browser with url's
    /// </summary>
    /// <param name="url">url to open</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static Process? StartURL(string url) {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            url = url.Replace("&", "^&");
            return Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            return Process.Start("xdg-open", url);
        } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
            return Process.Start("open", url);
        } else {
            throw new NotImplementedException("StartURL não implementada para a plataforma.");
        }
    }


    /// <summary>
    /// Cronometra o tempo de execução de A.
    /// Mostra com Log.LogInformation() o tempo da execução, e retorna o número de milissegundos
    /// <code>Cron( ()=> { codigo();... } );</code>
    /// </summary>
    /// <param name="A">Ação a ser cronometrada.</param>
    /// <returns></returns>
    public static long Cron(Action A) {
        System.Diagnostics.Stopwatch sw = new();
        Log.LogTrace("Iniciando cronometro sobre " + A.Method.Name);
        sw.Start();
        A();
        sw.Stop();
        var t = sw.ElapsedMilliseconds;
        Log.LogInformation(A.Method.Name + " levou " + t + "ms.");
        return t;
    }

    /// <summary>
    /// Cronometra o tempo de execução de A, com identificação da ação.
    /// Mostra com Log.LogInformation() o tempo da execução, e retorna o número de milissegundos
    /// <code>Cron( ()=> { codigo();... } );</code>
    /// </summary>
    /// <param name="A">Ação a ser cronometrada.</param>
    /// <returns></returns>
    public static long Cron(string ActionName, Action A) {
        System.Diagnostics.Stopwatch sw = new();
        Log.LogTrace("Iniciando cronometro sobre " + ActionName);
        sw.Start();
        A();
        sw.Stop();
        var t = sw.ElapsedMilliseconds;
        Log.LogInformation(ActionName + " levou " + t + "ms.");
        return t;
    }

}