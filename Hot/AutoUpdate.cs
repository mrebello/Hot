namespace Hot;

public static class AutoUpdate {
    static ILogger Log = LogCreate("Hot.AutoUpdate");

    public static bool Authorized(HttpListenerContext context) {
        bool r = false;
        string IPOrigem = context.Request.IP_Origem();
        string acceptFrom = Config[ConfigConstants.Update.AcceptFrom]!;  // valor default em appsettings.json
        if (acceptFrom.IsEmpty()) {
            Log.LogError($"'Update:AcceptFrom' deve estar configurado em appsettings.json para infos. Tentativa do IP: {IPOrigem}");
            context.Response.SendError("Falha na configuração.", HttpStatusCode.InternalServerError);
        } else {
            if (!IP_IsInList(IPOrigem, acceptFrom)) {
                Log.LogError($"Tentativa de pegar informações de IP não autorizado. IP: {IPOrigem}");
                context.Response.SendError("Não autorizado.", HttpStatusCode.Unauthorized);
            } else {
                r = true;
            }
        }
        return r;
    }

    public static string Version() {
        string version = Config[ConfigConstants.AppName] + '\t' + Config[ConfigConstants.Version] + "\r\n" +
            HotConfiguration.asmHot_resource.GetName().Name + '\t' + HotConfiguration.asmHot_resource.GetName().Version?.ToString();
        if (HotConfiguration.asmHotAPI_resource != null) {
            version += "\r\n" + HotConfiguration.asmHotAPI_resource.GetName().Name + '\t' + HotConfiguration.asmHotAPI_resource.GetName().Version?.ToString();
        }
        return version;
    }

    public static string Infos() {
        return Config.Infos();
    }


    /// <summary>
    /// Faz a atualização do próprio aplicativo. Recebe o nome do arquivo temporário com o novo executável.
    /// </summary>
    /// <param name=""></param>
    public static void AutoUpdate_Process(string tmpfile) {
        if (OperatingSystem.IsWindows()) {
            string path = Path.GetDirectoryName(tmpfile) ?? "";
            string batfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName() + ".bat";
            string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]!);

            if (WindowsServiceHelpers.IsWindowsService()) {   // Rodando como serviço do windows
                #region Atualiza Windows Service
                Log.LogInformation($"Atualizando Windows Service.");

                string servicename = Config.GetAsmResource.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";

                string bat = "";
                bat += $"cd {path}\r\n";
                bat += $"sc stop {servicename}\r\n";
                //                    bat += $"taskkill /im:\"{executablename}\"\r\n";
                //                    bat += $"taskkill /F /im:\"{executablename}\"\r\n";
                bat += $"ren \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\r\n";
                bat += $"ren \"{tmpfile}\" \"{executablename}\"\r\n";
                bat += $"sc start {servicename}\r\n";
                bat += $"del \"{batfile}\"\r\n";

                Log.LogDebug($"Salvando arquivo {batfile} com " + bat);
                File.WriteAllText(batfile, bat);
                Log.LogDebug($"Execuando arquivo {batfile}");
                System.Diagnostics.Process.Start(batfile);
                System.Environment.Exit(0);

                #endregion
            } else {   // Se não é como serviço, assume que foi chamado por linha de comando
                #region Atualiza Windows linha de comando
                Log.LogInformation($"Atualizando Windows command line.");

                string bat = "";
                bat += $"cd {path}\r\n";
                //                    bat += $"taskkill /im:\"{executablename}\"\r\n";
                //                    bat += $"taskkill /F /im:\"{executablename}\"\r\n";
                bat += $"ren \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\r\n";
                bat += $"ren \"{tmpfile}\" \"{executablename}\"\r\n";
                bat += String.Join(" ", Environment.GetCommandLineArgs()) + "\r\n";
                bat += $"del \"{batfile}\"\r\n";

                Log.LogDebug($"Salvando arquivo {batfile} com " + bat);
                File.WriteAllText(batfile, bat);
                Log.LogDebug($"Execuando arquivo {batfile}");
                System.Diagnostics.Process.Start(batfile);
                Log.LogDebug($"Saindo do processo.");
                System.Environment.Exit(0);

                #endregion
            }
        } else if (OperatingSystem.IsLinux()) {
            void chmod(string file, string arguments) {
                var p = System.Diagnostics.Process.Start("chmod", $"{arguments} \"{file}\"");
                p.WaitForExit();
            }

            if (Microsoft.Extensions.Hosting.Systemd.SystemdHelpers.IsSystemdService()) {
                #region Atualiza Linux Service
                Log.LogInformation($"Atualizando serviço linux.");

                string service_name = Config[ConfigConstants.ServiceName]!;

                string path = Path.GetDirectoryName(tmpfile) ?? "";
                string bashfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                string bashfile2 = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]!);

                string bat = "#!/bin/bash\n";
                bat += $"cd \"{path}\"\n";
                bat += $"mv \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\n";
                bat += $"mv \"{tmpfile}\" \"{executablename}\"\n";
                bat += $"chmod u+x \"{executablename}\"\n";
                bat += $"( rm \"{bashfile}\"  ; systemctl restart \"{service_name}\" )\n";

                Log.LogDebug($"Salvando arquivo {bashfile} com " + bat);
                File.WriteAllText(bashfile, bat);
                chmod(bashfile, "u+x");

                Log.LogDebug($"Execuando arquivo {bashfile}");
                System.Diagnostics.Process.Start(bashfile);
                //System.Environment.Exit(0);

                #endregion
            } else {
                #region Atualiza Linux cmd line
                Log.LogInformation($"Atualizando linux command line.");

                string path = Path.GetDirectoryName(tmpfile) ?? "";
                string bashfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]!);
                string servicename = Config.GetAsmResource.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "";

                string bat = "#!/bin/sh\n";
                bat += $"cd \"{path}\"\n";
                bat += $"sleep 0.3\n";
                bat += $"mv \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\n";
                bat += $"mv \"{tmpfile}\" \"{executablename}\"\n";
                bat += $"chmod u+x \"{executablename}\"\n";
                bat += $"sleep 0.3\n";
                bat += String.Join(" ", Environment.GetCommandLineArgs()) + "\n";
                bat += $"rm \"{bashfile}\"\n";

                Log.LogDebug($"Salvando arquivo {bashfile} com " + bat);
                File.WriteAllText(bashfile, bat);
                chmod(bashfile, "u+x");
                Log.LogDebug($"Execuando arquivo {bashfile}");
                System.Diagnostics.Process.Start(bashfile);
                Log.LogDebug($"Saindo do processo.");
                System.Environment.Exit(0);

                #endregion
            }
        } else {
            throw new NotImplementedException("AutoUpdate Só implementado para Linux e Windows.");
        }
    }


    /// <summary>
    /// Pega o arquivo a atualizar do HttpListenerContext
    /// Para HotAPI, ver HotAPIServer.AutoUpdate_ReceiveFile
    /// </summary>
    /// <param name="context"></param>
    public static void ReceiveFile(HttpListenerContext context) {
        string configsecret = Config[ConfigConstants.Update.Secret]!;   // default em appsettings.json
        string secret = context.Request.Headers["UpdateSecret"] ?? "";

        if (configsecret != secret) {
            Log.LogError($"UpdateSecret inválido. IP: {context.Request.IP_Origem()}");
            context.Response.SendError("Não autorizado.", HttpStatusCode.Unauthorized);

        } else {   // Recebe arquivo atualizado e salva na pasta do executável (se não tiver permissão, não pode atualizar)

            long size = 0;
            string tmpfile = Path.GetDirectoryName(Config[ConfigConstants.ExecutableFullName]) + Path.DirectorySeparatorChar + Path.GetRandomFileName();
            try {
                var f = File.Create(tmpfile);
                context.Request.InputStream.CopyTo(f);
                size = f.Length;
                f.Close();
            } catch (Exception e) {
                Log.LogError("Erro ao salvar arquivo da atualização.", e);
            }

            // Se salvou o arquivo corretamente
            if (size > 0) {
                context.Response.Send("Atualização recebida.");
                AutoUpdate_Process(tmpfile);
            } else {
                context.Response.SendError("Erro ao processar atualização.");
            }
        }
    }


    private static string updateurl_base() {
        string url = Config[ConfigConstants.Update.URL]!;   // default em appsettings.json
        return url + (HotConfiguration.asmHotAPI_resource == null ? "" : "HotAPI/");
    }

    public static void StartAutoUpdate() {
        //Thread.Sleep(1000);
        try {
            if (Config[ConfigConstants.Update.URL].IsEmpty())
                throw new ConfigurationErrorsException("'Update:URL' deve estar configurado.");

            string urlbase = updateurl_base();
            string destination = "";

            Log.LogInformation("Iniciando autoupdate em: " + urlbase);

            using var hc = new HttpClient();
            try {
                destination = hc.GetStringAsync(urlbase + "version").Result;
            } catch (Exception) {
            }
            if (destination.IsEmpty())
                throw new Exception("Erro ao pegar a versão atual.");

            destination = destination.Item(1, "\r\n");   // descarta versão das libs
            string app_destination = destination.Item(1, "\t");
            string version_destination = destination.Item(2, "\t");

            Log.LogInformation($"Detectado sistema '{app_destination}' versão {version_destination}.");

            string app_me = Config[ConfigConstants.AppName]!;
            if (app_me != app_destination) {
                throw new Exception($"App destino ({app_destination} diferente de app origem {app_me}. Não atualizado.");
            }

            string version_me = Config[ConfigConstants.Version]!;

            var c = Compare_Versions(version_me, version_destination);
            if (c <= 0) {
                string msg = c == 0 ? "Versões são iguais." : "Versão instalada é maior.";
                throw new Exception(msg + " Não atualizado.");
            }

            Log.LogInformation($"Iniciando atualização da versão {version_destination} para a {version_me}.");

            var executable = File.OpenRead(Config[ConfigConstants.ExecutableFullName]!);

#warning -- Implementar tipo de executável
            // if (executable.Length < 4_500_000) throw new Exception("Arquivo não parece ser pacote publicado. Abortando.");

            using var fileStreamContent = new StreamContent(executable);
            // Considera possível erro de não receber resposta completa devido a shutdown do app server
            try {
                hc.DefaultRequestHeaders.Add("UpdateSecret", Config[ConfigConstants.Update.Secret]);
                var r = hc.PutAsync(urlbase + "autoupdate", fileStreamContent).Result;
            } catch (Exception) {
            }

            // Teste para verificar se foi atualizado
            int tentativas = 10;
            bool ok = false;
            string v = "";
            while (!ok && tentativas >= 0) {
                Thread.Sleep(500); // aguarda processar
                try {
                    v = hc.GetStringAsync(urlbase + "version").Result;
                } catch (Exception) {
                }
                if (v.Item(1, "\r\n").Item(2, "\t") == version_me) {
                    ok = true;
                    break;
                }
                tentativas--;
            }

            if (ok) {
                Log.LogInformation("Atualização processada com sucesso.");
            } else {
                Log.LogError("Não houve erro, porém não foi detectada a nova versão instalada.");
            }
        } catch (Exception e) {
            Log.LogError(e, "Erro.");
            throw;
        }
    }

}
