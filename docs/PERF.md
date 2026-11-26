# Desempenho — régua medida

Toda decisão de desempenho do TzIsland é comparada com estes três números. A rodada anterior provou
por que: o teto de quadros era uma "otimização" sem medição que custou fluidez, e a Etapa 23 terminou
com um "não medido" explícito.

## Como medir

```bash
# recolhido (o estado em que o app passa a maior parte do tempo)
TzIsland.exe --bench 10

# expandido no Início
TzIsland.exe --bench 10 --expanded

# a régua longa (10 min), como no levantamento inicial
TzIsland.exe --bench 600
```

O modo `--bench` sobe o app completo (serviços, ilha, tique), **não** passa pelo mutex — é uma medição,
não uma segunda instância — e ao final imprime:

```
bench 10s (recolhido) · CPU 0,5% · RAM 96 MB · p95 7,5 ms · p50 7,0 ms · quadros 1437 (longos 3)
```

A mesma linha é gravada em `%AppData%\TzIsland\bench.txt` (o app é `WinExe`, então o console nem
sempre está disponível). As três medidas também aparecem no cartão **Diagnóstico** das Configurações e
no relatório copiável (`Ctrl+Shift+M` continua mostrando o medidor de quadro da própria ilha).

- **CPU**: média do processo no intervalo (`TotalProcessorTime` / tempo / núcleos).
- **RAM**: memória privada residente (`PrivateMemorySize64`).
- **p95**: percentil 95 do tempo de quadro entregue pelo vsync (últimos 180 quadros).

## Baseline — 26/11/2026

| Estado | CPU | RAM privada | p95 | p50 | Quadros (10 s) |
|---|---|---|---|---|---|
| Recolhido (pílula + LED) | **0,5%** | **96 MB** | **7,5 ms** | 7,0 ms | 1437 (3 longos) |
| Expandido (Início) | **2,2%** | **107 MB** | **7,4 ms** | 6,9 ms | 1193 (3 longos) |

Máquina: Windows x64, .NET 8, monitor de 144 Hz (por isso ~144 quadros/s de teto). Os números absolutos
variam com o hardware; o que vale é a **comparação** antes/depois na mesma máquina.

## Guarda de regressão

Comparado ao baseline, uma mudança é aceitável se, na mesma máquina e no mesmo estado:

- CPU não subir mais de **0,5 ponto percentual**;
- RAM privada não subir mais de **10 MB**;
- p95 não piorar mais de **1,5 ms**.

Acima disso, a mudança precisa de justificativa explícita (ou ser revertida). Meça recolhido **e**
expandido: a maior parte do tempo de vida do app é recolhido, e foi ali que o teto de quadros doeu.

## O que já foi medido nesta rodada

- **Modo economia em bateria** (Etapa 24): em bateria < 20% ou com a economia do Windows, o tique passa
  de 1 s para 4 s, o espectro e o HUD param, e clima/calendário são suspensos. Não altera nenhuma
  animação visível. Medir com `--bench` **na bateria** para ver o ganho real; na tomada o modo fica inativo.
- O baseline acima já inclui as mudanças estruturais da Etapa 23 (captura de vidro em 50%, tique único,
  microfone sob demanda, previews de 1024 px).
