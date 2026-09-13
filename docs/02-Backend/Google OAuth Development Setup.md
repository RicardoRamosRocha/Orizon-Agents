# Google OAuth Development Setup

Connections Gmail usam Authorization Code server-side com PKCE (S256), `openid email`, acesso offline e consentimento explícito. Não há scopes Gmail, Tools ou chamadas à API de mensagens.

## Google Cloud

1. Crie/selecione um projeto e configure a tela de consentimento (branding, público/audience e dados de contato).
2. Se o aplicativo estiver em modo de testes, adicione as contas que poderão autorizar.
3. Crie um cliente OAuth do tipo **Web application**.
4. Cadastre como **Authorized redirect URI** a URL HTTPS externa seguida de `/integracoes/conexoes/google/callback`. Respeite exatamente esquema, host, porta e eventual PathBase.
5. Copie ClientId e ClientSecret para configuração segura. Não versione o secret. Não é necessário habilitar Gmail API para esta etapa de identidade.

Com o perfil HTTPS atual de `src/OrizonAgents.Web/Properties/launchSettings.json`, o redirect de desenvolvimento é:

```text
https://localhost:7267/integracoes/conexoes/google/callback
```

Se usar IIS Express ou outra URL, cadastre o endereço efetivamente usado. A aplicação gera o callback usando `Url.Action`, o esquema e o host da requisição; a URL acima não está fixada no código.

## Desenvolvimento

O projeto Web já possui UserSecretsId. Execute na raiz, substituindo os placeholders:

```powershell
dotnet user-secrets set "Integrations:Google:ClientId" "<SEU_CLIENT_ID>" --project .\src\OrizonAgents.Web
dotnet user-secrets set "Integrations:Google:ClientSecret" "<SEU_CLIENT_SECRET>" --project .\src\OrizonAgents.Web
dotnet ef database update --project .\src\OrizonAgents.Infrastructure --startup-project .\src\OrizonAgents.Web
dotnet run --project .\src\OrizonAgents.Web --launch-profile https
```

Use o banco do ambiente pretendido ao aplicar a migration. A geração da migration nesta tarefa não a aplica automaticamente.

Entre como TenantAdmin, abra Conexões, crie/selecione uma conexão Gmail ativa e clique em **Conectar com Google**. Escolha a conta e confirme o consentimento. Sem configuração OAuth, o painel apresenta uma mensagem administrativa e não redireciona ao Google.

## Produção e proxy

- Configure `Integrations__Google__ClientId` e `Integrations__Google__ClientSecret` por variáveis de ambiente/secret manager.
- Cadastre `https://<HOST_PUBLICO>/<PATHBASE_SE_HOUVER>/integracoes/conexoes/google/callback`, sem duplicar barras. Restrinja `AllowedHosts` aos hosts usados.
- HTTPS é obrigatório neste fluxo, inclusive no desenvolvimento, pois Identity e o cookie de correlação são Secure.
- O middleware aceita X-Forwarded-For/Proto de loopback e dos IPs explicitamente configurados em `ReverseProxy:KnownProxies` (variável `ReverseProxy__KnownProxies__0`, etc.), com um salto. Não há confiança irrestrita nem uso de X-Forwarded-Host. O proxy deve preservar Host e sobrescrever os headers encaminhados. Outras topologias exigem configuração específica.
- Preserve o key ring existente de Data Protection em `DataProtection:KeysPath`; proteja a pasta e seus backups e compartilhe-a entre réplicas com o mesmo ApplicationName. Perder as chaves exige reautorização.
- Não habilite logging de corpos/headers OAuth, parâmetros EF sensíveis ou query strings do callback. O HttpClient Google não possui loggers automáticos; logs próprios contêm somente etapa, tenant e conexão. Os appsettings existentes mantêm ASP.NET Core em Warning. Aplique a mesma restrição no proxy/APM.

## Comportamento e segurança

- O state é protegido por Data Protection, expira em 10 minutos e contém tenant, conexão, usuário, callback, verificador PKCE e hash da correlação de navegador.
- Um hash persistido e o ConcurrencyStamp fazem o consumo ser único entre processos. Um novo início substitui o anterior; desconexão/desativação invalida uma autorização pendente.
- O cookie de correlação é HttpOnly, Secure, SameSite=Lax e tem prefixo __Host. Callback exige login, TenantAdminOnly e correspondência de tenant/usuário/cookie.
- Access token, refresh token, expiração, scopes, subject e ClientId ficam dentro de `EncryptedCredentials`, protegido pelo tenant/ID da conexão. Somente o e-mail verificado fica disponível no DTO administrativo.
- O access token é reutilizado até um minuto antes de expirar. Refresh preserva o token anterior se o Google não retornar outro. Em reautorização, isso só ocorre para o mesmo subject e ClientId.
- Se o Google não fornecer refresh token na primeira autorização, a conta pode usar o access token até expirar; depois será necessário reconectar. O serviço não fabrica refresh tokens.
- Erros transitórios no refresh preservam as credenciais para nova tentativa; invalid_grant ou payload ilegível exigem reautenticação. Cancelar consentimento não marca Error.
- Desconexão tenta revogar remotamente e limpa dados locais mesmo em timeout/falha remota. Nesse caso, o painel orienta remover manualmente o acesso do aplicativo em [Conta Google](https://myaccount.google.com/connections).
- A revogação Google afeta o consentimento da conta para o projeto e pode invalidar outras conexões dessa mesma conta. Não é uma revogação isolada por registro Orizon.
- A remoção do cadastro exige desconectar primeiro quando houver credenciais ou OAuth pendente.
- Concorrência entre callbacks, refresh e desconexão é detectada; uma resposta atrasada não restaura credenciais após desconexão. A operação perdedora solicita nova tentativa.
- Validação automatizada usa handlers HTTP falsos, sem internet/conta Google. O consentimento real depende da configuração externa e deve ser validado manualmente.

Referências: [OAuth para aplicações Web](https://developers.google.com/identity/protocols/oauth2/web-server) e [OpenID Connect / UserInfo](https://developers.google.com/identity/openid-connect/openid-connect).