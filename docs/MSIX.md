# Notificações do Windows e identidade de pacote

A API `UserNotificationListener` só concede acesso a apps com **identidade de pacote** (MSIX ou "sparse package").
Sem isso o TzIsland continua funcionando, mas a captura de notificações de outros apps fica indisponível
(a interface mostra o motivo em Inbox e em Configurações > Notificações).

Caminhos possíveis (próxima etapa):
1. Projeto "Windows Application Packaging" (MSIX) no Visual Studio apontando para o TzIsland, com a capability
   `userNotificationListener` no manifesto.
2. "Sparse package" (identidade de pacote sem empacotar o app inteiro).
Depois, conceda a permissão em Configurações do Windows > Privacidade e segurança > Notificações.
