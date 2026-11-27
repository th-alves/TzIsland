# TzIsland

Dynamic Island para Windows 10/11 (x64). C# · WPF · .NET 8.

## Como compilar e instalar
    dotnet run                                  # testar
    dotnet publish -c Release -o publish        # gera publish\TzIsland.exe (arquivo único)
    tools\vpk.exe pack -u TzIsland -v 2.7.0 -p publish -e TzIsland.exe -i Assets\TzIsland.ico -c win -o releases
Rode `releases\TzIsland-win-Setup.exe` para instalar. Detalhes de empacotamento, publicação no
GitHub Releases e atualização automática (Velopack): **docs/VELOPACK.md**. O `installer.iss` (Inno) ficou obsoleto.

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
- Fonte Manrope embutida (`Fonts/Manrope.ttf`, variável, licença OFL em `Fonts/LICENSE-Manrope.txt`).
- Sons da Ambientação sintetizados em tempo real; para trocar por gravações, coloque `rain.mp3`, `forest.mp3`, `cafe.mp3`, `office.mp3`, `lofi.mp3`, `fire.mp3`, `stream.mp3` ou `waves.mp3` na pasta `Sounds\` ao lado do executável.

## Linha de comando (scripts e hooks)
    TzIsland.exe --notify "Título" "Texto"
    TzIsland.exe --agent claude --state working|needs_input|done|idle --message "..." [--progress 40]
                 [--usage-session 62 --usage-weekly 30]

Controle local (AutoHotkey, Stream Deck, atalhos do Windows, scripts):

    TzIsland.exe --timer 25m [--pause|--stop]      # 90s, 1h30m, 45 (min)
    TzIsland.exe --dnd on|off|toggle
    TzIsland.exe --volume 40|+5|-5     ·   --mute on|off|toggle
    TzIsland.exe --page media|focus|system|...
    TzIsland.exe --media play|pause|next|prev|toggle

Tudo vai pelo mesmo pipe nomeado (`TzIsland.ipc.v2`): a segunda instância repassa o comando à
principal e encerra. Nada de HTTP aberto nem porta na rede.

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

## Verificação visual (`--shot`)

Renderiza a ilha em PNG no estado pedido, sem animar e sem passar pelo mutex/bandeja/serviços —
útil para conferir layout depois de mexer em UI, inclusive em escala (equivalente a DPI maior):

```bash
TzIsland.exe --shot shots --state collapsed
TzIsland.exe --shot shots --state expanded --page media --scale 1.5
TzIsland.exe --shot shots --state expanded --page home --style classic
TzIsland.exe --shot shots --settings --tab 8 --scale 1.5     # janela de Configurações, aba 8 (Diagnóstico)
```

Saída: `shots\<estado>[-página]@<escala>x.png` (a pasta `shots/` está no `.gitignore`).
Limitação: os serviços não sobem nesse modo, então widgets de sistema aparecem com valores padrão.

## Testes

```bash
dotnet run --project Tests
```

Runner próprio (sem pacote externo), 46 casos: molas, perfis de intensidade, geometria do notch, cascata
radial, backup (round-trip, zip malicioso, manifest e esquema mais novo), agenda (RRULE/COUNT/EXDATE/
RECURRENCE-ID/TZID), rotação do log, durações da CLI e agrupamento do diagnóstico. Sai com código 0
quando tudo passa. O workflow `.github/workflows/windows.yml` roda build + testes em `windows-latest`
em todo push e pull request.

## Desempenho

```bash
TzIsland.exe --bench 10              # recolhido
TzIsland.exe --bench 10 --expanded   # expandido
```

Imprime CPU do processo, RAM privada e p95 de quadro, e grava em `%AppData%\TzIsland\bench.txt`.
Baseline medido e guarda de regressão: **docs/PERF.md**. Os três números também aparecem em
Configurações → Diagnóstico.

## Distribuição

Velopack (atual) vs MSIX (necessário para ler notificações do Windows de outros apps): a análise e a
recomendação estão em **docs/DISTRIBUICAO.md**.
