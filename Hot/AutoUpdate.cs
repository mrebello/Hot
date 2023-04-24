namespace Hot {
    public static class AutoUpdate {
        static ILogger L = Log.Create("Hot.AutoUpdate");

        public static bool Authorized(HttpListenerContext context) {
            bool r = false;
            string IPOrigem = context.Request.IP_Origem();
            string acceptFrom = Config[ConfigConstants.Update.AcceptFrom];
            if (acceptFrom.IsEmpty()) {
                L.LogError($"'Update:AcceptFrom' deve estar configurado em appsettings.json para infos. Tentativa do IP: {IPOrigem}");
                context.Response.SendError("Falha na configuração.", HttpStatusCode.InternalServerError);
            } else {
                if (!IP_IsInList(IPOrigem, acceptFrom)) {
                    L.LogError($"Tentativa de pegar informações de IP não autorizado. IP: {IPOrigem}");
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
                string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]);

                if (WindowsServiceHelpers.IsWindowsService()) {   // Rodando como serviço do windows
                    #region Atualiza Windows Service
                    L.LogInformation($"Atualizando Windows Service.");

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

                    L.LogDebug($"Salvando arquivo {batfile} com " + bat);
                    File.WriteAllText(batfile, bat);
                    L.LogDebug($"Execuando arquivo {batfile}");
                    System.Diagnostics.Process.Start(batfile);
                    System.Environment.Exit(0);

                    #endregion
                } else {   // Se não é como serviço, assume que foi chamado por linha de comando
                    #region Atualiza Windows linha de comando
                    L.LogInformation($"Atualizando Windows command line.");

                    string bat = "";
                    bat += $"cd {path}\r\n";
                    //                    bat += $"taskkill /im:\"{executablename}\"\r\n";
                    //                    bat += $"taskkill /F /im:\"{executablename}\"\r\n";
                    bat += $"ren \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\r\n";
                    bat += $"ren \"{tmpfile}\" \"{executablename}\"\r\n";
                    bat += String.Join(" ", Environment.GetCommandLineArgs()) + "\r\n";
                    bat += $"del \"{batfile}\"\r\n";

                    L.LogDebug($"Salvando arquivo {batfile} com " + bat);
                    File.WriteAllText(batfile, bat);
                    L.LogDebug($"Execuando arquivo {batfile}");
                    System.Diagnostics.Process.Start(batfile);
                    L.LogDebug($"Saindo do processo.");
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
                    L.LogInformation($"Atualizando serviço linux.");

                    string service_name = Config[ConfigConstants.ServiceName];

                    string path = Path.GetDirectoryName(tmpfile) ?? "";
                    string bashfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                    string bashfile2 = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                    string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]);

                    string bat = "#!/bin/bash\n";
                    bat += $"cd \"{path}\"\n";
                    bat += $"mv \"{executablename}\" \"{executablename}-{DateTime.Now.ToString("yyyy-MM-dd-HHMMss")}\"\n";
                    bat += $"mv \"{tmpfile}\" \"{executablename}\"\n";
                    bat += $"chmod u+x \"{executablename}\"\n";
                    bat += $"( rm \"{bashfile}\"  ; systemctl restart \"{service_name}\" )\n";

                    L.LogDebug($"Salvando arquivo {bashfile} com " + bat);
                    File.WriteAllText(bashfile, bat);
                    chmod(bashfile, "u+x");

                    L.LogDebug($"Execuando arquivo {bashfile}");
                    System.Diagnostics.Process.Start(bashfile);
                    //System.Environment.Exit(0);

                    #endregion
                } else {
                    #region Atualiza Linux cmd line
                    L.LogInformation($"Atualizando linux command line.");

                    string path = Path.GetDirectoryName(tmpfile) ?? "";
                    string bashfile = path + Path.DirectorySeparatorChar + Path.GetRandomFileName();
                    string executablename = Path.GetFileName(Config[ConfigConstants.ExecutableFullName]);
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

                    L.LogDebug($"Salvando arquivo {bashfile} com " + bat);
                    File.WriteAllText(bashfile, bat);
                    chmod(bashfile, "u+x");
                    L.LogDebug($"Execuando arquivo {bashfile}");
                    System.Diagnostics.Process.Start(bashfile);
                    L.LogDebug($"Saindo do processo.");
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
            string configsecret = Config[ConfigConstants.Update.Secret];
            string secret = context.Request.Headers["UpdateSecret"] ?? "";

            if (configsecret != secret) {
                L.LogError($"UpdateSecret inválido. IP: {context.Request.IP_Origem()}");
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
                    L.LogError("Erro ao salvar arquivo da atualização.", e);
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

    }
}
