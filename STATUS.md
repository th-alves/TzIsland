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
## Etapa 5 — Redesign "Foco" (estilo MacBook / empresarial)
- 🟡 Página **Foco** (nova, 3º ícone da barra): cartões Pomodoro | Timer | Cronômetro + Beber água, como na referência
- ✅ Pomodoro, Timer e Cronômetro **independentes** (rodam juntos); relógio por timestamp, sem deriva
- ✅ Pílula recolhida mostra o último iniciado: ícone coral | rótulo | tempo em coral (+N se houver outros rodando); alarga com overshoot
- ✅ Água: contador diário persistente (water.json), toque nas gotas, lembretes opcionais (08h–22h) pela ilha
- ✅ Animações: entrada em cascata, molas, anel com brilho, onda sonora, chips deslizantes, switch elástico, lente da barra com squash & stretch
- 🟡 Padrão novo: 760×356, visual Solid Black (use "Restaurar layout padrão" nas configurações para aplicar)
- ⚠️ Compilação WPF não executada neste ambiente: só validação de sintaxe com Roslyn
## Etapa 6 — Estilo Foco em toda a ilha + correção do ghosting
- ✅ **Bug de ghosting corrigido:** `K.FadeIn` deixava a animação de opacidade "presa" em 1 (HoldEnd), e a transição de abas não conseguia apagar a página que saía. Agora a animação é liberada ao terminar e a troca é em sequência (a antiga sai, depois a nova entra; nunca duas visíveis)
- ✅ Tokens globais: cartões índigo + borda fina, acento coral (K.Blue agora é coral), texto Dim/Faint arroxeado, campos de texto e sliders no mesmo padrão
- ✅ Botão primário = pílula branca com texto escuro (como o "Iniciar")
- ✅ Início, Mídia e Inbox refeitos em cartões com cabeçalho mono + ícone coral; Produtividade com abas em chips deslizantes
- ✅ Sistema, Arquivos, IA, Espelho e Ditado herdam os tokens novos
## Etapa 7 — Configurações, Anotações, LED e recolher
- ✅ Janela de Configurações refeita: barra lateral, cartões índigo, switches/sliders coral, chips de escolha, barra de título escura
- ✅ Anotações: lista em cartões (título, prévia, data) com marcador coral, editor maior e rodapé limpo
- ✅ Pílula recolhida: só um LED pulsando (laranja = ocioso, verde = ativo: foco rodando, mídia tocando, agente de IA, câmera, ditado ou microfone em uso)
- ✅ Recolher: causa da demora corrigida (espera dupla de 1,6 s). Novas opções em Comportamento: animação (instantânea/rápida/suave), atraso ao tirar o mouse (ms) e recolher por inatividade
- 🟡 Os indicadores de microfone/câmera da pílula foram substituídos pelo LED verde; o detalhe continua na página Sistema
- 🟡 Largura recolhida padrão agora 150 (use "Restaurar layout padrão")
## Etapa 8 — LED à direita, ícones reais, contador e scroll
- ✅ LED agora no lado direito da pill; brilho por gradiente radial + fade cruzado laranja/verde (o DropShadowEffect reprocessado a cada frame era a causa do engasgo)
- ✅ Contador do Inbox: número centralizado, anel preto, posição fixa no canto do ícone e "pulo" elástico ao mudar
- ✅ Ícones reais (Ui/BrandIcons.cs): Claude, OpenAI/ChatGPT, Codex, Cursor, Gemini, Ollama, LM Studio — usados nos agentes de IA, no banner de notificação e nos grupos do Inbox; ícone "faísca" próprio na barra de navegação (no lugar do texto "AI")
- ✅ Scroll reescrito (alvo acumulado + suavização por frame): serve para todas as páginas e para as Configurações
- ℹ️ Logos: vetores do pacote aberto @lobehub/icons-static-svg (MIT); as marcas pertencem aos respectivos donos e aparecem só para identificar o app/agente
## Etapa 9 — Motion design e bordas
- ✅ Bordas que vazavam: `ClipToBounds` em Border com CornerRadius recorta em retângulo. Novo `K.RoundClip` (recorte arredondado) aplicado no cartão de Mídia, capas, miniaturas de arquivos e espelho da câmera
- ✅ Expandir/recolher reescrito: largura, altura e raio em molas diferentes (morph), conteúdo entra depois com escala + subida; ao recolher o conteúdo some primeiro e só depois a forma encolhe
- ✅ Causa do engasgo: BlurEffect no painel inteiro a cada frame (renderização por software) — removido; região da janela só é recriada quando muda
- ✅ Camada `Motion` (Core/Motion.cs): presets de mola, Reveal/RevealAll/RevealPage; menu inferior e cartões de todas as páginas entram em cascata (também ao trocar de aba e de sub-aba)
## Etapa 10 — Premium Night Black (v2.1)
- ✅ **Clássico colado na borda:** `Floating` estava fixo em `true` (o Clássico nunca era aplicado). Agora segue `S.Style`; no Clássico a ilha sobe até `y = 0`, sem margem, com a silhueta de notch (`NotchGeo`: topo reto, ombros côncavos, base arredondada) e borda só nas laterais/base
- ✅ Paleta global neutra: preto puro, cartões #141416, bordas 1 px, acento monocromático (branco). Cada seção usa no máximo uma cor (violeta na mídia, azul na água, verde nos switches). Tokens renomeados: `Coral`→`Accent`, `CoralGradient`→`AccentGradient`, `Blue`→`Sky`
- ✅ Fonte **Inter** embutida (pasta `Fonts/`, licença OFL) com algarismos tabulares nos números; rótulos em caixa alta espaçada (`K.Cap`/`K.Spaced`); se a fonte falhar, cai para Segoe UI Variable
- ✅ **Timers** refeito no layout da referência: Pomodoro (anel branco), Temporizador (ondas + chips + Iniciar/Cancelar), Cronômetro (lista de voltas rolável), Beber água (gotas + switch verde)
- ✅ **Mídia** no estilo "Now Playing": capa arredondada, origem + equalizador, barra violeta com bolinha, três botões
- ✅ **Recolhida com mídia tocando:** só o ícone (música/vídeo) à esquerda e o espectro real do áudio à direita (`SpectrumService`: loopback WASAPI + FFT; só captura quando há consumidor)
- ✅ **Notas** no estilo da referência (lista + editor monoespaçado, lixeira vermelha, "salvo HH:mm")
- ✅ **Ambientação (nova):** 8 sons em camadas (chuva, floresta, cafeteria, escritório, lo-fi, fogueira, riacho, ondas), volume por som, volume geral, desligar sozinho em 15/30/60 min (fade de ~12 s) e "Parar tudo"
- ✅ **Início** no layout "Minimal": Sistema (CPU/RAM/disco), Relógio, Tocando agora, Bateria, Som e luz, Clima e Agenda (sem o widget de iniciar apps); role para ver a 3ª linha
- ✅ Barra de seções: lente em mola mais macia, ícones que acendem/crescem conforme a lente passa, página nova entra pela direção certa
- ℹ️ Os sons da Ambientação são **sintetizados** (sem arquivos nem direitos autorais). Para usar gravações reais, coloque `rain.mp3`, `forest.mp3`, `cafe.mp3`, `office.mp3`, `lofi.mp3`, `fire.mp3`, `stream.mp3` ou `waves.mp3` em `Sounds\` ao lado do app (ou em `%AppData%\TzIsland\Sounds`)
- 🟡 Validado com compilação de tipos fora do Windows e teste numérico do motor de áudio; **não** foi executado em Windows: confira visualmente e me diga o que ajustar
## Etapa 11 — Ajustes após o primeiro teste (v2.2)
- ✅ **Ilha recolhida com atividade ao vivo:** timers em andamento (Pomodoro, Temporizador, Cronômetro) aparecem com ícone + tempo; mídia continua com ícone + espectro; os dois juntos alargam a ilha (timer à esquerda, mídia à direita). Cada troca de combinação é registrada em `log.txt`
- ✅ Seleção legível: o texto ficava escuro sobre fundo escuro e o branco vazava nos cantos porque os chips eram opacos e cobriam o destaque. `ChipRow` refeito em camadas (pílulas → lente branca → texto) com a **mesma mola da barra de seções** (380/21, esticar/achatar, texto acende conforme a lente passa)
- ✅ Barra de seções: botões com o mesmo tamanho e raio da lente (o realce de hover não vaza mais) e ícones vetoriais de traço fino
- ✅ Gotas de água: a área de clique agora é a célula inteira (antes só os pixels do contorno)
- ✅ Caracteres estranhos em "Mix" e "Parar tudo": o código dos ícones tinha barra invertida duplicada
- ✅ **Sistema** refeito no estilo da Início: caixa de itens ativáveis (Bateria, Wi-Fi, Bluetooth, Microfone), caixa de controles (Volume, Brilho, Contraste) e caixa de dispositivos conectados. Wi-Fi e Bluetooth ligam/desligam de verdade pelos rádios do Windows
- ✅ Conjunto de ícones vetoriais novo (35) e ícone de bateria desenhado em código (preenchimento proporcional, cor por nível, raio ao carregar)
## Etapa 12 — Estilos de animação e mídia (v2.3)
- ✅ **Estilo da animação ao abrir/fechar** (Configurações → Efeitos e Fluidez): Fluida (padrão), Elástica, Cristal, Precisa e Bolha. Cada estilo é um perfil de molas em `Core/AnimProfiles.cs` (forma, conteúdo, cascata dos cartões e recolher). Ao trocar, a ilha faz uma prévia sozinha; há também o botão "Pré-visualizar animação"
- ✅ Mídia: o app agora escuta **todas** as sessões do Windows e prefere a que está tocando (antes só a "sessão atual", que podia ser um Spotify pausado enquanto o vídeo tocava no Chrome). Sem capa, a página de mídia mostra um ícone de vídeo/música
## Etapa 13 — Gestos, downloads, agenda, letras, cor da capa, clipboard, foco e sons (v2.4)
- ✅ **Hover corrigido:** o padrão agora é abrir só no **clique**. Configurações → Comportamento tem um seletor único "Abrir a ilha ao: Clicar / Passar o mouse / Os dois" (antes eram duas caixas independentes e o hover continuava ligado). Quem já usava "clicar" é migrado automaticamente (uma vez, via `SettingsVersion`)
- ✅ **Gestos na ilha recolhida:** rolar = volume do sistema (3% por passo, com HUD na própria pílula) · botão do meio = play/pause · arrastar de lado = próxima/anterior (com "empurrão" elástico). Com mídia tocando, para **mover** a ilha segure **Ctrl** e arraste
- 🟡 **Download ao vivo:** a pílula mostra ícone + tamanho baixado + velocidade e uma linha fina animada. **Sem porcentagem:** o navegador não informa o tamanho final à pasta, então a barra é "em andamento" (indeterminada) e só enche (verde) quando o arquivo realmente termina. Detecta .crdownload (Chrome/Edge/Brave), .part (Firefox), .opdownload (Opera)
- ✅ **Próximo compromisso:** a pílula avisa "Título · em 9 min" e, nos últimos 5 min, contagem regressiva mm:ss em laranja; "agora" por 1 min. Antecedência configurável (0 = desligado). Eventos de dia inteiro são ignorados. Limitação herdada: RRULE/TZID ainda não tratados
- ✅ **Letra sincronizada** (LRCLIB, gratuito, sem chave): botão no canto da página de Mídia; linha atual centralizada com rolagem em mola; cache em disco; faz fallback para letra sem tempo; só envia título/artista/duração. Pode ser desligada em Privacidade
- ✅ **Cor dinâmica da capa:** cor dominante "viva" extraída da capa (matiz mais pesado por saturação) → espectro e barra de progresso da página de Mídia, espectro e HUD da pílula, brilho atrás da capa. Transição suave entre faixas
- ✅ **Histórico da área de transferência** (nova seção, ícone de prancheta): texto, imagens e arquivos; fixar/desafixar, remover, filtro Tudo/Fixados; clique copia, duplo clique cola (Ctrl+V na janela ativa). Só os **fixados** vão para disco; conteúdo marcado como privado por gerenciadores de senha é ignorado
- ✅ **Não perturbar no Pomodoro:** durante o foco os banners ficam retidos (as notificações seguem para o Inbox). No fim, um único aviso com o resumo. Alarme do Timer, fim do foco e bateria crítica não são retidos
- ✅ **Sons de abrir/fechar:** opcionais (desligados por padrão), sintetizados em código, volume ajustável e botão "Testar som"
- ✅ **Atualização pelo GitHub:** verifica os Releases de th-alves/TzIsland (a cada N horas), avisa na ilha e, ao tocar no banner, baixa o `TzIsland-Setup.exe` do Release e atualiza sozinho. Configurações → Comportamento → Atualizações
- ⚠️ Validação: sintaxe de todos os arquivos conferida com Roslyn; **compilação WPF e execução não foram feitas** (sem Windows aqui). Rode `dotnet build` e me mande qualquer erro
## Pendente
- ⏳ Empacotamento MSIX (para notificações do Windows)
- ⏳ Reordenar/ocultar ícones da barra inferior por arrastar
- ⏳ Testes automatizados

## Etapa 14 — Correções e organização (v2.4.1 / v2.4.2)
- ✅ **Temporizador com segundos:** role o mouse sobre os minutos ou sobre os segundos para ajustar (Shift = de 5 em 5). A dica fica abaixo dos números
- ✅ **Barra inferior organizável:** Configurações → Módulos, setas ‹ › em cada módulo e "Restaurar ordem padrão da barra"
- ✅ **Cartões do Início organizáveis:** mostrar/ocultar e reordenar em Configurações → Módulos; "Tocando agora" sempre ocupa 2 colunas
- ✅ **Letra da música:** volta a aparecer (a cor de fundo sem `#` derrubava a montagem das linhas e o app mostrava "Sem conexão"); linha cantada centralizada no painel, com fundo próprio e sombra
- ✅ **Ilha não some no print:** a camada de captura (Win+Shift+S, ShareX, Greenshot, Lightshot, Snagit…) não é mais tratada como app em tela cheia
- ✅ Armazenamento local: texto genérico, sem mostrar o caminho do perfil do usuário

## Etapa 15 — Mixer de volume (v2.5.0)
- ✅ **Mixer de volume na ilha:** volume e mudo de cada app que usa o som, com o ícone real do programa e medidor ao vivo; rolar o mouse ajusta de 2 em 2 (Shift = 10)
- ✅ **Geral:** volume e mudo do sistema, com o nome da saída atual
- ✅ **Saída:** troca o dispositivo de áudio padrão (fones, caixas, monitor) com um clique
- ✅ Só atualiza enquanto a página está aberta. Módulo "Mixer de volume" em Configurações → Módulos

## Etapa 16 — Ícone oficial (v2.5.1)
- ✅ **Novo ícone "Tz" neon:** no exe, na barra de tarefas, na janela de Configurações, na bandeja do sistema e no instalador (Assets/TzIsland.ico, 16–256 px)

## Etapa 17 — Barra lateral e nova página de Notas (v2.6.0)
- ✅ **Barra de seções na lateral direita:** ícones em coluna, lente deslizando na vertical, engrenagem fixa embaixo, rolagem com bordas esmaecidas quando há muitas seções; troca de páginas na vertical
- ✅ **Notas redesenhadas:** painel de formatação (Negrito, Itálico, Riscado, Código, Lista, Numerada, Título) + editor em cartão, expansível
- ✅ **Cabeçalho das notas:** contador, alternador Lista/Formatar, indicador de salvo, nova, exportar (.txt), excluir e ajuda com atalhos
- ✅ Formatação salva junto da nota (campo `Rich`), notas antigas continuam abrindo
- ✅ Correções: lente fora do lugar ao abrir a ilha; editor não recebia teclado
