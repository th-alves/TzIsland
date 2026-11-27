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
- ⏳ Decisão de distribuição: Velopack (atual) vs MSIX — análise em docs/DISTRIBUICAO.md; o MSIX continua necessário para notificações de outros apps
- ⏳ Reordenar/ocultar ícones da barra inferior por arrastar
- ⏳ i18n (pt-BR literal inline; extrair para .resx quando inglês entrar no escopo)
- ⏳ Acessibilidade: nomes e foco já aplicados nos ícones; falta varrer páginas internas

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

## Etapa 18 — v2.7.0
- Sistema: bateria dos dispositivos Bluetooth com anel de LEDs animado, ícone por tipo, layout compacto quando o card é baixo; clique abre o Bluetooth do Windows.
- Correção: destaque da barra lateral ia para o Início ao abrir o Inbox.

## Etapa 19 — Motion centralizado e continuidade (v2.9.0)
- ✅ **Relógio único:** `Core/Motion/MotionHost.cs` — uma assinatura de `CompositionTarget.Rendering` para o app inteiro e um `dt` por quadro. `MotionLoop`, rolagem suave, espectro, anel de bateria e página Foco passaram a ser consumidores desse relógio (antes eram ~25 assinaturas independentes).
- ✅ **Posse de propriedade:** `Core/Motion/MotionEngine.cs` — cada (objeto, propriedade) tem no máximo uma track. Retarget preserva velocidade e parte do valor visual atual; ao assentar, o valor vira local e a track sai do registro (fim do `FillBehavior.HoldEnd` preso). `Interaction` (hover/press/tint), cores, opacidades e escalas passam por aí.
- ✅ **Sem saltos na interrupção:** reabrir no meio do fechamento não salta mais (`_expO <= 0,001` em vez de 0,05); troca de página não força o fim da anterior; mudar configuração não assenta mais as molas.
- ✅ **Páginas em tracks:** saída e entrada independentes; a página só sai da árvore quando a opacidade assenta (fim do hack `_pg`/`_incoming`/`FinishTransition` forçado).
- ✅ **Menos layout por quadro:** lente da barra e destaque dos chips esticam por **escala** (não por Width/Height); geometria do modo Flutuante é reaproveitada (`RectangleGeometry` mutável); a largura da pílula só muda quando muda de verdade.
- ✅ **Sem blur de software por quadro:** o antigo `Soft()` foi removido; a borda (`_outline`) engrossa conforme a velocidade da forma — identidade a custo zero, repouso idêntico ao anterior.
- ✅ **Custo em repouso:** a página Foco desliga o laço quando nada se move (relógio por evento + texto a 16 Hz); respiração do anel de bateria e do LED viraram tracks pausáveis; `_clockT` só roda recolhida; a checagem de tela cheia não enumera processos quando o modo é "normal".
- ✅ **Configurações granulares:** `SettingsChange` (Look/Layout/Motion/Modules/Behavior). Arrastar um slider não reconstrói a barra de seções nem mata animações.
- ✅ **Intensidade do movimento:** Configurações → Efeitos e Fluidez → Discreto / Natural / Expressivo, mapeado em amplitude, defasagem e firmeza sobre os mesmos cinco presets (Natural = comportamento anterior).
- ✅ **Medidor de quadro:** `Ctrl+Shift+M` mostra p50/p95, quadros longos e consumidores ativos.
- ✅ **Tokens:** `Core/Motion/Tz.cs` concentra molas (Form/Edge/Content/Fade/Lift/Lens/Sheet/Exit/Settle), defasagens e distâncias.
- 🟡 **Ainda não implementado:** morph de conteúdo com matched geometry (LED → bloco, tempo recolhido → anel, espectro contínuo) e sombra externa — dependem de redesenhar as três superfícies e de decidir região/hit-test da janela.
- ⚠️ Validado por `dotnet build` (0 erros). **Não executado visualmente** — confira as animações e me diga o que ajustar.

## Etapa 20 — Matched geometry, sombra externa e cor por mola (v2.9.1)
- ✅ **Matched geometry (Etapa 4):** `Core/Motion/Matched.cs` mede origem e destino no layout real e leva um **portador** por transformação (posição + escala), revelando o herói na chegada. O ícone/LED da pílula recolhida viaja até a **capa** (Mídia) ou o **anel** (Foco) — `PageBase.Hero`. Medida que falha = nenhum efeito: nunca quebra layout, no pior caso não há continuidade.
- ✅ **Espectro contínuo:** a fase das barrinhas vem do relógio global (`MotionHost.Now`); trocar de superfície (pílula ↔ página) não reinicia mais a animação.
- ✅ **Sombra externa:** halo real (`DropShadowEffect`) atrás da silhueta; a região da janela cresce 24 px e o halo **não** rouba cliques — fora da ilha o `WM_NCHITTEST` devolve `HTTRANSPARENT`. Só no estilo Flutuante (no Clássico os ombros côncavos deixariam preto nos "ears"). Interruptor em Configurações → Efeitos e Fluidez → "Sombra externa".
- ✅ **Cor por mola:** `MotionEngine.ToColor(..., SpringSpec)` — o tom assenta em vez de parar numa duração; `Interaction.Tint` passou a usar.
- 🐞 **Correção de runtime:** `Math.Clamp(x, 0, negativo)` lançava `ArgumentException` quando uma mola ultrapassava zero no fechamento rápido; a exceção era engolida pelo handler global e **abortava o resto do quadro**. Geometria agora nunca recebe valor negativo, a região da janela ignora medidas inválidas e dois clamps com máximo calculado (notch, thumb de slider estreito) foram blindados.
- ✅ Verificado em execução: app inicia e permanece vivo; avisos `--notify` sem exceção no log; hit-test real da janela → corpo `HTCLIENT` (1), halo `HTTRANSPARENT` (-1).
- ⚠️ Aparência (portador, halo, cascatas) **não verificada visualmente** — precisa ser olhada rodando.

## Etapa 21 — Observabilidade, energia, backup, testes e verificação visual (v2.10.0)
- ✅ **Diagnóstico embutido:** `Core/Diagnostics.cs` guarda as exceções agrupadas por assinatura (as do `Log.Write` agora contam) e monta um relatório copiável. Configurações → **Diagnóstico**: contadores (sessão, erros, quadros longos), assinaturas mais frequentes, "Copiar relatório", "Abrir pasta de dados", "Limpar registro". Antes um erro engolido pelo handler global degradava a ilha em silêncio (foi assim que um `Math.Clamp` abortava o quadro).
- ✅ **Orçamento de energia:** `MotionHost.Paused` agora **para** o laço de render (não só impede novos quadros); a ilha pausa em `PowerModeChanged` (suspender) e `SessionSwitch` (bloquear/desconectar), ao ficar invisível e ao entrar em tela cheia. Espectro roda a **30 fps** (metade dos quadros) e só com a pílula visível; relógio e checagem de tela cheia param junto.
- ✅ **Backup dos dados:** `Core/Backup.cs` exporta/importa um `.zip` de `%AppData%\TzIsland` (ajustes, notas, tarefas, água, fixados) sem o log, e **recusa** entradas com subpasta ou `..` (zip é entrada não confiável). Botões em Configurações → Diagnóstico → Dados.
- ✅ **Testes do núcleo:** `Tests/` (runner próprio, sem dependências externas): molas (assentamento, overshoot, atraso, cancelamento), perfis de intensidade, geometria do notch em tamanhos degenerados, cascata radial, backup (round-trip e zip malicioso) e agrupamento do diagnóstico. **26/26 passam** com `dotnet run --project Tests`.
- ✅ **Verificação visual por PNG:** `TzIsland.exe --shot pasta\ [--state collapsed|banner|expanded] [--page media] [--scale 2] [--style classic|floating]` renderiza a ilha sem animar e sem passar pelo mutex/bandeja/serviços. Primeira validação real feita com ele: silhueta do notch (ombros côncavos), halo da sombra no Flutuante, borda fina, lente da barra e as três páginas conferidas.
- ✅ **Esc fecha a ilha expandida** (antes só o mouse).
- ⏳ Fora desta etapa: paleta de comandos (Ctrl+K), MSIX, assinatura de código, OSD nativo de volume/brilho, i18n, API local para scripts e acessibilidade completa por teclado.

## Etapa 22 — Redesign "Cupertino Dark" (v3.0 visual)
- ✅ **Tokens de superfície:** `K.Surface` virou gradiente vertical (`#19` → `#0E`) em vez de cor chapada, com `K.Raised` (hover/controle) e `K.Knob` (item selecionado). A borda passou de cinza opaco (`#1D1D20`) para **branco translúcido** (`#14FFFFFF` / `#2EFFFFFF`), que é o que lê como "material" em vez de contorno de dashboard.
- ✅ **Acento azul de sistema:** `K.Accent`/`AccentGradient` agora são `#0A84FF` → `#0071E3`. O branco ficou reservado para a ação primária (pílula "Iniciar"); verde/vermelho/laranja só para estado. Sliders de volume e barras de progresso voltaram a ser **brancos** (o azul ficou só onde indica seleção/estado: controles das Configurações e ícone ativo da barra lateral).
- ✅ **Tipografia sem caixa alta:** `K.Cap` deixou de aplicar `ToUpperInvariant` + haircut de tracking e virou rótulo de 11 px em caixa normal. Isso muda todos os cabeçalhos de cartão de uma vez (efeito "developer" → "app de sistema"). `FK.Header`, Inbox e Configurações seguiram o mesmo padrão.
- ✅ **Contraste:** `K.Faint` subiu de `#6E6E73` (~3,8:1) para `#8E8E93`; `K.Dim` para `#B4B4BA`. Atenção: sobre **Liquid Glass claro** textos em `Faint/Muted` somem — rótulos de cabeçalho continuam em `Muted`.
- ✅ **Cartões:** `FK.Card`/`K.CardBox` com raio/padding padronizados (14/16/18/20 + 16,14) e borda hairline translúcida.
- ✅ **Configurações estilo macOS:** seções sem bolinha e sem caixa alta, linhas com 44 px de altura e divisor interno, chips de escolha com `K.Knob` em vez de branco puro, sidebar com item ativo em cinza `Raised` + ícone azul, fundo `K.Bg`.
- ✅ **Ilha:** grão procedural de 96×96 repetido a 3% (`ImageBrush` tiled) por cima do tint, quebrando o banding do preto. LED de repouso ficou **estático** (sem respiração; só o estado ativo pulsa). Destaque ativo da barra lateral virou tile **opaco** (`Raised`) — antes era branco a 16%, que clareava sobre o vidro e apagava o ícone branco.
- ✅ **Ícones:** emoji do clima substituído por ícones vetoriais de traço (`sun`, `partly`, `cloud`, `fog`, `drizzle`, `rain`, `snow`, `storm`) em `WeatherService.Icon`. Nenhum emoji colorido na interface.
- ✅ **Espaçamento:** padding dos cartões do Início e das páginas com cartão alinhado em 16/14; a grade do Início manteve 148/124 de altura para a 3ª linha continuar aparecendo na rolagem.
- ⚠️ **Backdrop nativo não aplicável:** a janela é `AllowsTransparency=false` com região recortada, então Acrylic/Mica do DWM não compõe. O vidro continua sendo a captura desfocada; o ganho veio do grão + composição.
- 🐞 **Borda sobrando ao trocar Clássico → Flutuante:** com a ilha parada, `SetState` sai cedo (mesmo estado) e nenhum quadro rodava depois de `ApplySettings`, então o recorte e a `_outline` ficavam com a silhueta do notch desenhada em volta da pílula. `ApplySettings` agora reaplica a geometria (`Frame()`) na hora.
- 🐞 **"Cancelar" espremido no Temporizador:** o botão dividia a linha com "Pausar" em colunas proporcionais e cortava o texto. Virou um alvo redondo de 32 px com ✕ (a coluna anima em pixels, não em estrelas), e a pílula principal ficou com a largura restante.
- ✅ Verificado com `--shot` (home, focus, media, system, settings, collapsed) + `dotnet build` 0 erros. Não há teste automatizado de pixel: mudanças de cor/raio precisam de uma olhada rodando.

## Etapa 23 — Custo em repouso (v3.1 de desempenho)
- ❌ **Teto de quadros (desfeito):** a tentativa de acumular o vsync em passos de 13,5 ms foi revertida — nos monitores de 120/144 Hz ela dava um passo a cada dois quadros e a animação passou a parecer travada. `MotionHost.OnRender` voltou a dar exatamente um passo por quadro entregue pelo vsync, com o `dt` real.
- ✅ **Captura do vidro pela metade:** `CaptureBackdrop` copia a tela e reduz para 50% antes de virar `_backdrop` (metade da RAM do bitmap residente, indistinguível atrás do desfoque e da opacidade).
- ✅ **Um tique por segundo:** `IslandWindow` trocou `_clockT` (1 s), `_statsT` (1 s) e `_fsT` (2 s) por um único `_tickT` → `OnSlowTick()`: relógio recolhido, medidor de quadro e checagem de tela cheia (a cada 2 tiques). O tique para junto no sono/sessão bloqueada.
- ✅ **Microfone sob demanda:** `AudioService` só liga o `DispatcherTimer` de 4 s quando `MonitorMicrophone` está ligado (e religa/desliga em `Settings.Changed`), em vez de acordar 15×/min para sair cedo.
- ✅ **Fixadas da área de transferência:** o PNG fixado é decodificado em 1024 px para a prévia (o arquivo em disco continua no tamanho original) — antes cada item carregava o bitmap inteiro na inicialização.
- ⚠️ **Não medido:** as mudanças são de custo estrutural (quadros, wakes, bitmaps). Use `Ctrl+Shift+M` antes/depois para comparar p50/p95; nenhuma captura de CPU/RAM foi feita nesta etapa. *(Medido na Etapa 24: ver docs/PERF.md.)*

## Etapa 24 — v3.1 fechada: versão, medição, CI, agenda, teclado, paleta, CLI e dados (v3.1.0)
- ✅ **Versão:** `.csproj` passou de 2.9.0 para **3.1.0** — o Velopack empacota essa versão, e o Diagnóstico/Sobre mostram o valor real. Sem isso, quem já tinha 2.9.0 nunca receberia a atualização.
- ✅ **Baseline medido e guarda de regressão:** `--bench N [--expanded]` sobe o app completo e imprime CPU do processo, RAM privada e p95 de quadro (também em `bench.txt` e no cartão Diagnóstico). Medido em 26/11/2026: **recolhido 0,5% CPU / 96 MB / p95 7,5 ms** · **expandido 2,2% / 107 MB / 7,4 ms**. Limites de regressão em **docs/PERF.md** (+0,5 p.p. de CPU, +10 MB, +1,5 ms de p95).
- ✅ **CI no Windows:** `.github/workflows/windows.yml` roda `dotnet build` e `dotnet run --project Tests` em `windows-latest` a cada push/PR. Os testes saíram de 26 para **46** casos (agenda, manifest de backup, rotação de log, durações da CLI).
- ✅ **Agenda com RRULE e TZID:** DAILY/WEEKLY/MONTHLY/YEARLY com INTERVAL, COUNT, UNTIL e BYDAY; `EXDATE` remove ocorrências; `RECURRENCE-ID` substitui a ocorrência remarcada (com a duração herdada do mestre); `TZID` converte da zona do calendário para a local. Expansão em janela de 90 dias com pulo direto para perto de "agora" (um diário de 2015 não custa milhares de iterações).
- ✅ **Teclado e acessibilidade (fatia da barra):** `Tab`/`Shift+Tab` e ↑/↓ trocam de página na barra lateral, com anel de foco visível; `Enter`/`Espaço` ativam o botão focado; `AutomationProperties.Name` em todos os botões só de ícone (a barra, a engrenagem e os controles criados por `K.IconBtn`).
- ✅ **Paleta de comandos (Ctrl+K):** busca sobre páginas e ações (Pomodoro, timer de 25 min, Não perturbar, mudo/volume, trocar saída de áudio, colar o último fixado, editar cartões, atualizar, configurações, recolher). ↑/↓/Enter/Esc. O atalho só é capturado com a ilha aberta (registrar Ctrl+K global roubaria o atalho de editores).
- ✅ **API local / CLI:** `--timer 25m`, `--dnd on|off|toggle`, `--volume 40|+5|-5`, `--mute`, `--page media` e `--media play|pause|next|prev|toggle`, pelo pipe nomeado já existente. Relativo de volume é resolvido na instância principal.
- ✅ **Proteção dos dados:** backup agora tem **manifest** (esquema, versão, data, arquivos) e **recusa** um backup de esquema mais novo em vez de importar pela metade; **backup automático diário** em `backups\` mantendo os 7 últimos; fixados da área de transferência (índice e imagens) gravados com **DPAPI CurrentUser** (`clipboard.bin`, PNG cifrado) com migração do formato em claro.
- ✅ **Log com rotação:** ~1 MB, mantendo `log.txt` + `log.1.txt` + `log.2.txt`; o tamanho é rastreado em memória em vez de reler o arquivo a cada evento.
- ✅ **Modo economia automático:** em bateria abaixo de 20% ou com a economia do Windows, o tique passa de 1 s para 4 s, espectro/HUD param e clima/calendário são suspensos até voltar à tomada. Nenhuma animação muda.
- ✅ **Decisão de distribuição documentada:** **docs/DISTRIBUICAO.md** recomenda manter o Velopack agora e migrar para MSIX de uma vez se o Inbox do Windows virar prioridade (não manter os dois).
- ⏳ **Fora desta rodada:** i18n (extração para `.resx`), acessibilidade das páginas internas e MSIX.
- ⚠️ **Verificado:** `dotnet build` 0 erros, 46/46 testes, `--bench` recolhido e expandido. A paleta, o foco por teclado e o anel de foco **não foram inspecionados visualmente** nesta rodada.

## Etapa 25 — Correção: área morta de cliques em volta da ilha (v3.1.1)
- ✅ **Causa:** a janela da ilha (880×444 px, sempre no topo) não era mais recortada por `SetWindowRgn` desde a reescrita do motor de animação; o click-through dependia de `HTTRANSPARENT`, que só repassa cliques para janelas da mesma thread. Para outros apps, toda a área vazia da janela engolia os cliques.
- ✅ **Correção:** `UpdateRegion()` voltou (ilha + `ShadowPad` no Flutuante), chamada a cada quadro e no `Place()`; `SuppressFrame()` é reaplicado a cada nova região para não voltar o contorno de 1 px.
- ⚠️ **Verificado:** `WindowFromPoint` nos cantos e no centro com a ilha recolhida e expandida; prints sem contorno. Modo Clássico e multi-monitor não testados.

## Etapa 26 — Relógio na pílula, Manrope e texto nítido (v3.2.0)
- ✅ **Horário na ilha recolhida:** `HH:mm` centralizado na pílula, na tipografia do app (Manrope SemiBold, algarismos tabulares) e não na fonte padrão do Windows; atualiza no tique de 1 s e só troca o texto quando o minuto vira. O LED e a faixa ao vivo (mídia, timer, download, compromisso) vão para as bordas e a pílula alarga 60 px para o horário caber. Fica no palco do mini, irmão do LED e da faixa, então nunca herda a opacidade animada de outro elemento.
- ✅ **Fonte do app: Inter → Manrope:** `Fonts/Manrope.ttf` (variável 200–800, OFL: um arquivo cobre Regular, Medium e SemiBold) no lugar dos três arquivos da Inter; `K.Font`, `K.FontMedium` e `K.FontSemi` apontam para ela com o mesmo fallback Segoe UI Variable. `Fonts/**` fica só como Resource (os .ttf não vão soltos para a publicação).
- ✅ **Texto nítido:** fundo da ilha e das Configurações de `#000000` para `#0F0F11` e cartões de `#0E0E10–#19191D` para `#141416–#1A1A1D` (branco puro sobre preto puro "irradia" e realça cada degrau de pixel); texto de `#F5F5F7` para `#F2F2F5`; texto comum em Medium e títulos/números em SemiBold; `TextOptions` ClearType + Hinting Fixed e `FormattingMode` Ideal nas duas janelas (o Display encaixa as letras na grade e deixa a Manrope torta); sombra e largura do mini arredondadas para pixel inteiro.
- ✅ **Convenção de commits documentada:** `docs/CONVENCAO-DE-COMMITS.md`, montada a partir do histórico real (tipos, escopos, formato, anti-padrões, checklist).
- ⚠️ **Verificado:** `dotnet build` 0 erros, 46/46 testes. Prints em `shots/relogio/` e `shots/nitidez/`. O modo Clássico, multi-monitor e DPI diferente de 100% **não foram inspecionados** nesta rodada.

## Etapa 27 — Cliques na silhueta e recolhimento confiável (v3.2.1)
- ✅ **Cliques ao redor da ilha:** a região que recorta a janela seguia a caixa cheia (pílula + 24 px do halo da sombra), então essa faixa invisível engolia cliques em abas e botões dos apps de baixo. A região agora é a **silhueta exata** (`HitRegion`, em `Core/Motion.cs`: degraus de 1 px nos cantos redondos, ombros côncavos no Clássico) e o halo foi para uma **janela própria click-through** (`WS_EX_TRANSPARENT`), fora da região da ilha.
- ✅ **Recolher ao tirar o mouse:** o recolhimento dependia de `MouseLeave`/`IsMouseOver`, que a região recortada deixa de entregar (o ponteiro segue dentro do retângulo da janela). Agora um vigia de 200 ms lê o cursor de verdade (`GetCursorPos`) e compara com a mesma silhueta; `KeepOpen` só considera campo de texto **desta** janela, porque com `WS_EX_NOACTIVATE` o botão clicado mantém o foco de teclado para sempre — era o que prendia a ilha aberta depois de abrir por clique.
- ✅ **Foco do editor:** clicar fora do campo de texto solta o foco de verdade (o caret para e as setas voltam para a ilha).
- ⚠️ **Verificado:** `dotnet build` 0 erros, 56/56 testes (10 novos da região de cliques). O recolhimento com mouse real **não foi confirmado nesta versão**; Clássico e DPI diferente de 100% também não foram inspecionados.

## Etapa 28 — Borda da ilha lisa nos dois modos e em DPI ≠ 100% (v3.2.2)
- ✅ **Causa:** o serrilhado não vinha do desenho e sim do `SetWindowRgn`, que recorta em pixels inteiros e cortava o degradê antialiasado da borda; a região ganhou folga (`HitRegion.Grow`) para devolver esses pixels (invisíveis, transparentes na forma).
- ✅ **Folga de 2 px, não 1:** medido renderizando a silhueta como o app desenha (clip arredondado e notch) e comparando pixel a pixel o alfa desenhado com o que a região deixa passar, em 100 casos. Com **1 px** sobravam pixels cortados em DPI ≠ 100% (até **21 px, alfa 219** no Clássico 320×36 a 200%), porque `left*dpi` cai no meio do pixel e a origem da região é arredondada; com **2 px** o corte é **zero** em todos os casos (100/125/150/175/200%, deslocamentos fracionários de 0 a 0,75 px, Flutuante 420×44 e 760×300, Clássico 420×44 e 320×36).
- ✅ **Clássico no topo da tela:** a folga faz a região começar em `y = -2`; testado numa janela real que o `SetWindowRgn` aceita a região negativa e o sistema mantém a coordenada — nada a recortar.
- ⚠️ **Verificado:** `dotnet build` 0 erros, **58/58** testes (2 novos: a folga cresce 1 px por lado e folga zero mantém a região). **Não inspecionado na tela** nesta rodada: a suavidade foi conferida por medição de alfa, não a olho; mouse real nos dois modos e em DPI ≠ 100% fica com quem usar.

