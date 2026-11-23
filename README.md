# TzIsland

Dynamic Island para Windows 10/11 (x64). C# · WPF · .NET 10 ou superior.

## Como compilar e instalar
    dotnet run                                  # testar
    dotnet publish -c Release -o publish        # gera publish\TzIsland.exe (arquivo único)
Depois abra `installer.iss` no Inno Setup e compile → `TzIsland-Setup.exe`.
A desinstalação remove o app e a pasta de dados (`%AppData%\TzIsland`).

## Arquitetura
    Core/       configurações (JSON), barramento de eventos (Hub), motor de mola (Motion), P/Invoke, IPC
    Services/   integrações com o Windows: mídia (SMTC), bateria, volume/microfone (Core Audio),
                brilho (WMI + DDC/CI), Bluetooth, arquivos, notificações, câmera, ditado, clima, agenda, foco
    Ai/         monitor de agentes: um adaptador independente por provedor (IAgentAdapter)
    Ui/         janela da ilha, kit de estilos, páginas (uma por módulo) e janela de configurações
Cada módulo é independente: desligue em Configurações > Módulos.

## Animação "estilo Apple"
Tamanho, raio dos cantos, entrada do conteúdo e o indicador da barra inferior usam **molas amortecidas**
(`Core/Motion.cs`), não curvas fixas: há overshoot sutil, a velocidade é preservada se você interromper
(entrar e sair rápido não "trava"), e o conteúdo entra em cascata com atraso. O loop de animação só roda
enquanto algo se move (consumo ocioso ≈ 0). Botões têm "squish" ao clicar e rolagem tem inércia.
Para desligar tudo: Configurações > Aparência > Animações.

## Ambientação e fontes
- Fonte Inter embutida (`Fonts/`, licença OFL em `Fonts/LICENSE-Inter.txt`).
- Sons da Ambientação sintetizados em tempo real; para trocar por gravações, coloque `rain.mp3`, `forest.mp3`, `cafe.mp3`, `office.mp3`, `lofi.mp3`, `fire.mp3`, `stream.mp3` ou `waves.mp3` na pasta `Sounds\` ao lado do executável.

## Linha de comando (scripts e hooks)
    TzIsland.exe --notify "Título" "Texto"
    TzIsland.exe --agent claude --state working|needs_input|done|idle --message "..." [--progress 40]
                 [--usage-session 62 --usage-weekly 30]

## Monitoramento de agentes de IA (sem dados inventados)
O app mostra apenas o que consegue **de fato** saber:
- **Processo aberto**: Claude Code, Codex, Cursor, Ollama, LM Studio.
- **Ollama / LM Studio / endpoints próprios**: consulta HTTP local (`/api/ps`, `/v1/models`) — mostra se está online e os modelos carregados.
- **Claude Code**: além do processo, infere "trabalhando" pela escrita nos arquivos de sessão (marcado como *inferido*).
- **Estado exato (trabalhando / precisa de você / concluído)**: via **hooks**, que chamam o `--agent` acima.
- **Limites de uso (anéis de sessão/semana)**: não existe API pública e oficial para isso nessas ferramentas, então os anéis
  ficam como "—" até que *você* alimente com `--usage-session/--usage-weekly` a partir de uma fonte que você tenha.
  Com valores recebidos o app alerta em 80% e 95% e avisa quando o limite de sessão renova.

### Exemplos de hooks (confira o formato atual na documentação de cada ferramenta)
Claude Code (`~/.claude/settings.json`, eventos `UserPromptSubmit`, `Notification` e `Stop`), comando de cada hook:
    "C:\Users\VOCE\AppData\Local\Programs\TzIsland\TzIsland.exe" --agent claude --state working
    "C:\...\TzIsland.exe" --agent claude --state needs_input --message "Precisa da sua ação"
    "C:\...\TzIsland.exe" --agent claude --state done
Codex (`~/.codex/config.toml`):
    notify = ["C:\\...\\TzIsland.exe", "--agent", "codex", "--state", "done"]

## Privacidade
Tudo local em `%AppData%\TzIsland`. Internet apenas para clima (Open-Meteo) e links ICS que você configurar.
Câmera só enquanto o Mirror está ligado; microfone só no ditado; nada é gravado ou enviado.
