# Distribuição: Velopack ou MSIX?

Esta é uma **escolha de arquitetura**, não uma tarefa. As duas opções disputam o mesmo papel — quem
instala, quem atualiza e qual identidade o Windows enxerga — e não podem coexistir no mesmo app.

## O que cada uma dá

| | Velopack (hoje) | MSIX |
|---|---|---|
| Instalação sem admin | sim (`%LocalAppData%`) | sim |
| Atualização automática | sim (`UpdateManager` + GitHub Releases) | sim (App Installer / Store), mas **fora** do controle do app |
| Identidade de pacote no Windows | **não** | **sim** |
| `UserNotificationListener` (Inbox do Windows, WhatsApp) | **não** | **sim** |
| Assinatura de código | opcional (SmartScreen reclama) | **obrigatória** para instalar fora da Store |
| Captura de áudio/tela, DDC/CI, WMI | sem restrição | capacidades declaradas; algumas exigem revisão |
| `--notify`, `--timer`, IPC por pipe | sim | sim (o executável continua sendo um processo) |
| Complexidade de release | baixa (`release.cmd`) | média (manifesto, `signtool`, empacotamento) |

## O ponto de conflito

Ler notificações de outros apps **exige** identidade de pacote (MSIX). O Velopack **é** o dono do
update hoje. Manter os dois significa dois atualizadores escrevendo na mesma instalação — não é
suportado. Portanto:

- **Continuar no Velopack** = o Inbox fica restrito ao próprio app, hooks e `--notify`. O WhatsApp nunca
  aparece. É o estado atual, documentado na interface.
- **Migrar para MSIX** = o Inbox do Windows passa a existir de verdade; em troca, o Velopack sai, o
  update passa a depender do pacote (App Installer/Store), e é preciso certificado de assinatura.

## Recomendação

**Ficar no Velopack por enquanto.** O redesign, a paleta de comandos, a API local e a agenda recorrente
são entregas de alto retorno e baixo risco; o Inbox nativo é a única capacidade que justifica o MSIX, e
mexer em distribuição antes de estabilizar a v3.1 troca um canal de atualização que funciona por um
processo de assinatura e empacotamento novo.

**Quando reavaliar**: se o Inbox do Windows virar prioridade de produto (WhatsApp/Teams aparecendo na
ilha), a migração deve ser feita **de uma vez**, não em paralelo:

1. obter um certificado de assinatura de código (EV ou OV) e configurar `signtool`;
2. criar o `Package.appxmanifest` com as capacidades de notificação e startup task;
3. empacotar o publish atual em MSIX e validar instalação/atualização em uma máquina limpa;
4. remover `VelopackApp` do `OnStartup`, o `UpdateService` e as referências `Vpk*` do `.csproj`;
5. publicar o MSIX no GitHub Releases (App Installer) e desativar os releases Velopack antigos.

Até essa decisão, **não investir nos dois lados**: qualquer trabalho novo no Inbox deve assumir que
notificações do sistema continuam indisponíveis.
