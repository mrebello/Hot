# Hot

Rotinas básicas para aplicações, baseada em .NET 6, para serviços, envolvendo:


##Config

Diversos pontos implementados para melhorar o *Log* padrão do .NET:

- Usa **System.Configuration.ConfigurationManager**.
- Implementa _Log_ por email e em arquivo
- *appsettings.json* fique embutido no executável. 
- pesquisa arquivos de configurações em uma sequência padrão de locais
- classe estática global para uso na aplicação como um todo, independente da sequência de inicialização da injeção de dependência confusa original. 

Uso normal:

    string app_me = Config["AppName"];


##Log

Usa **Microsoft.Extensions.Logging**, adicionado:

- emailLogger: envio de log por email (usando System.Net.Mail)
- facilidades para criar categoria de log e recurso para *Information* e *Debug* usando lambda (para não degradar a performance da aplicação)

exemplos de uso:

- padrão (instância estática *Log* é global, instanciada automaticamente pela biblioteca)

        Log.LogError($"Tentativa de atualização de IP não autorizado. IP: {IPOrigem}");


- com categoria

        ILogger L = Log.Create("Hot.HttpServer");
		...
        L.LogInformation("~HttpServer: Fechando listener");

- com lambda

        Log.LogInformation(() => Log.Msg( funcao_demorada(c) ));


##SelfHostedService

Classe abstrata para ser usada para criação de serviços sefthosted.

Baseada no **IHostedService**, adicionando:

- *MainDefault* para ser usada como função básica da classe, que implementa parâmetros padrões de linha de comando: help, helpconfig, install, uninstall, infos, version
- install/uninstall   - instala a aplicação como serviço do windows ou linux (systemd). Dados de nome e descrição do serviço são pegos dos metadados da aplicação.
- autoupdate  - recurso para publicação de nova versão do executável 'de produção' mesmo quando instalado como serviço automaticamente via nova versão do desenvolvimento, tratando também a aplicação *em produção* instalada como serviço do windows (apenas para aplicações httpserver)
- Uso da configuração padrão da biblioteca


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
                string infos = Config.Infos();
                context?.Response.Send(infos.ReplaceLineEndings("<br>"));
            }
        }
    }

Para a criação de APIs Web, use a <a href="https://github.com/mrebello/HotAPI">HotAPI</a>.


##BD

Classe para acesso a direto a banco de dados (*database first*, sem uso de ORM, ao sqlserver, usando **System.Data.SqlClient**):

- Conexão automática (com ou sem transação), com retry automático
- Comandos SQL com parâmetros de forma simples
- Log dos comandos (nível de log nas configurações)
- String de conexão nas configurações

uso na aplicação: (não é necessário nenhuma declaração antes do uso)

    BD.SQLCmd("UPDATE CRM_email_Enviado SET Data_leitura=getdate() WHERE Cod_CRM_email_Enviado=@1 AND Data_Leitura IS NULL", Cod_CRM_email_Enviado);
