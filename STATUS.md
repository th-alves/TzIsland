# Registro de funcionalidades — TzIsland 2.0

Legenda: ✅ implementado · 🟡 implementado com limitação · ⛔ bloqueado por limitação técnica · ⏳ pendente
**IMPORTANTE:** este código foi escrito num ambiente Linux, sem Windows. Foi verificado apenas por análise de sintaxe
(Roslyn), compilação dos módulos de lógica com stubs e validação do XAML embutido. **Nada foi executado em Windows.**
Todos os itens abaixo precisam ser testados por você no primeiro `dotnet run`; marque o que falhar e mande o erro.

## Etapa 1 — Estrutura principal
- ✅ Ilha no topo com estilos Clássico e Flutuante
- ✅ Aparências Liquid Glass / Dark Glass / Solid Black
- 🟡 Vidro: o desfoque é uma *captura* do que está atrás, feita ao expandir (não acompanha o fundo em tempo real)
- ✅ Largura/altura (expandida e recolhida), posição (centro/esq./dir./arrastada), arrastar para reposicionar
- ✅ Animações de mola (expandir, recolher, banner, troca de página, indicador, botões)
- ✅ Expandir por hover e/ou clique, atraso configurável; recolhimento por inatividade
- ✅ Vários monitores (escolha do monitor; arrastar entre monitores); DPI por monitor (PerMonitorV2)
- ✅ Comportamento em tela cheia (ocultar / não expandir / normal)
- ✅ Configurações completas, persistência, restaurar padrões, iniciar com o Windows, ícone na bandeja
## Etapa 2 — Integrações com o Windows
- ✅ Mídia via SMTC (Spotify, Apple Music p/ Windows e outros que exponham SMTC), play/pause/próx./ant., aleatório, repetir, progresso, volume do sistema
- ⛔ Capas animadas: o SMTC só entrega imagem estática
- 🟡 Notificações do Windows (UserNotificationListener): exigem app **empacotado (MSIX)** — veja docs/MSIX.md. Sem isso a Inbox funciona só com notificações do próprio app/hooks/`--notify` e a interface mostra o motivo
- ⛔ WhatsApp: só aparece se o Windows entregar a notificação (mesma dependência acima); nenhum acesso a mensagens privadas
- ✅ Bateria (% e carregando, alertas de 20% e 10%)
- ✅ Volume (slider + mudo); 🟡 brilho: tela interna via WMI (só notebooks) e externos via DDC/CI (depende do monitor)
- 🟡 Bluetooth: estado do rádio e dispositivos conectados; bateria só quando o dispositivo/Windows informam
- ✅ Detecção de microfone em uso e do app responsável (indicador laranja no notch)
- ✅ Monitoramento de Downloads e Capturas de Tela
## Etapa 3 — Produtividade
- ✅ Notas (criar/editar/excluir, salvar automático, persistem)
- ✅ Tarefas (prioridade, concluir, editar com duplo clique, excluir)
- ✅ Foco: Pomodoro configurável, temporizador, cronômetro, notificação ao fim
- 🟡 Agenda: eventos locais + calendários externos via link ICS; eventos recorrentes (RRULE) e fusos (TZID) ainda não tratados
- ✅ Atalhos personalizados (programas, pastas, sites, `ms-settings:` etc.)
## Etapa 4 — Avançado
- 🟡 Agentes de IA: detecção por processo/HTTP; estado exato apenas via hooks (README); sem dados inventados
- ⛔ Limites de uso (sessão/semana) por API oficial: não existe para Claude Code/Codex/Cursor; anéis só com dados que você fornecer
- 🟡 Ícones dos provedores: usamos badges de letra (não embutimos logos de marcas)
- ✅ Bandeja de arquivos: adicionar, arrastar para fora, abrir, remover sem apagar, ligar/desligar monitoramento
- ✅ Mirror: seleção de câmera, abre só quando ligado, fecha ao sair; sem gravação
- 🟡 Ditado: reconhecimento do Windows (exige "reconhecimento de voz online" ativo); texto vai ao clipboard/colar na janela ativa
- ✅ Widgets: clima (Open-Meteo), previsão de 3 dias, bateria, música
- ⛔ Tela de bloqueio: sem API pública; widgets ficam dentro da ilha
## Pendente
- ⏳ Empacotamento MSIX (para notificações do Windows)
- ⏳ Reordenar/ocultar ícones da barra inferior por arrastar
- ⏳ Testes automatizados
