# HotAPI

Biblioteca complementar à <a href="https://github.com/mrebello/HotLIB">HotLIB</a> para a criação de WebAPIs ou aplicações WEB.




Rotinas básicas para aplicações, baseada em .NET 6, para serviços, envolvendo:

##Config

Usa **System.Configuration.ConfigurationManager**.

Sequência dos locais de configuração padrão alterados para permitir que o *appsettings.json* fique embutido no executável. 

 adicionando:

- classe estática global para uso na aplicação como um todo
- Ambiente de desenvolvimento "Development" definido via variável de ambiente nas propriedades de depuração (DOTNET_ENVIRONMENT=Development) ou parâmetros na linha de comando
- configurações 'padrão' do aplicativo ficam no arquivo AppSettings.JSON, que deve estar incorporado no APP
- Procura configuração também em {exename}.conf e em /etc/{assemblername}.conf antes da linha de comando
- Linha de comando = appupdate.exe /MySetting:SomeValue=123
- Variáveis de Ambiente = set Logging__LogLevel__Microsoft=Information      (__ ao invés de : na variável de ambiente em sistemas linux)

Uso normal:

- appsettings.json incorporado na aplicação
- appsettings.Development.json não incorporado
- *user-secrets* para guardar senhas de desenvolvimento
- *xxxxx.json* (xxxxx = nome do executável) para guardar configurações do ambiente de execução (para permitir empacotamento em arquivo único, e uso de diversos microserviços em mesmo diretório)

exemplo de uso: (instância *Config* é global, instanciada automaticamente pela biblioteca)

    string app_me = Config["AppName"];


## Log

Usa **Microsoft.Extensions.Logging**, adicionado:

- emailLogger: envio de log por email (usando System.Net.Mail)
- facilidades para criar categoria de log e recurso para *Information* e *Debug* usando lambda (para não degradar a performance da aplicação)

exemplos de uso:

- padrão (instância *Log* é global, instanciada automaticamente pela biblioteca)

        Log.LogError($"Tentativa de atualização de IP não autorizado. IP: {IPOrigem}");


- com categoria

        ILogger L = Log.Create("Hot.HttpServer");
		...
        L.LogInformation("~HttpServer: Fechando listener");

- com lambda

        Log.LogInformation(() => Log.Msg( funcao_demorada(c) ));


## BD

Classe para acesso a banco de dados (sqlserver, usando **System.Data.SqlClient**), adicionando:

- Conexão automática (com ou sem transação), com retry automático
- Comandos SQL com parâmetros de forma simples
- Log dos comandos (nível de log nas configurações)

declaração para uso com diversos bancos na aplicação:

    global using static nnnn.BD.SQL;

    namespace nnnnn.BD {
        public class BDs : BD_simples {
            public BDs() : base(null) {
            }
            public BD_simples WEB = new BD_simples("WEB");
            public BD_simples ASPPREV = new BD_simples("ASPPREV");
        }

        public static class SQL {
            public static BDs BD = new BDs();
        }
    }

uso na aplicação: (não é necessário nenhuma declaração antes do uso)

    BD.WEB.SQLCmd("UPDATE CRM_email_Enviado SET Data_leitura=getdate() WHERE Cod_CRM_email_Enviado=@1 AND Data_Leitura IS NULL", Cod_CRM_email_Enviado);


## SelfHostedService

Classe abstrata para ser usada para criação de serviços sefthosted.

Baseada no **IHostedService**, adicionando:

- *MainDefault* para ser usada como função básica da classe, que implementa parâmetros padrões de linha de comando:
    - /? - help
    - /helpconfig  - lista sequência de pesquisa de arquivos de configuração
    - /install
    - /uninstall   - instala a aplicação como serviço do windows (falta implementar linux). Dados de nome do serviço e descrição são pegos dos metadados da aplicação)
    - /autoupdate  - recurso para atualização do executável 'de produção' automaticamente via nova versão do desenvolvimento, tratando também a aplicação *em produção* instalada como serviço do windows (apenas para aplicações httpserver)
- método **StartAsync** e **StopAsync** abstratos para implementação do serviço

## HttpServer

Classe abstrata para aplicação de um servidor http simples.

Baseada no **HttpListener**, adiciona:

- método abstrato *Process(HttpListenerContext context)* para processar os pedidos, já sendo chamado de forma assíncrona pelo tratamento do listener
- pré-processamento para devolver versão e fazer o autoupdate da aplicação

exemplo de uma **aplicação** de httpserver:

    internal class Program : HttpServer {
        public static void Main(string[] args) {
            MainDefault<Program>();
        }

        public override void Process(HttpListenerContext context) {
            string url = RemoveIgnorePrefix(context?.Request.Url?.LocalPath);

            if (url == "/teste") {
                string infos = $@"Infos:
                  IsWindows = {Config["IsWindows"]}
                  IsLinux = {Config["IsLinux"]}
                  Ambiente = {Config["Environment"]}
                  AppName = {Config["AppName"]}
                  Config search path = {HotConfiguration.configSearchPath}
                  Teste = {Config["Teste"]}";

                context?.Response.Send(infos.ReplaceLineEndings("<br>"));
            }
        }
    }
