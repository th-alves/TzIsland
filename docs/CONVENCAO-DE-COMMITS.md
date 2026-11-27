# Convenção de commits — TzIsland

Padrão de mensagem de commit do projeto, montado a partir do que o repositório **já usa de bom**
(`feat(ui):`, `fix(nav):`, `chore:`, `release:`, corpos com marcadores) e das boas práticas do
[Conventional Commits](https://www.conventionalcommits.org/pt-br/v1.0.0/) + [Keep a Changelog](https://keepachangelog.com/pt-br/1.1.0/).

Este documento é só referência: nenhum commit existente precisa ser reescrito.

---

## 1. Formato

```
<tipo>(<escopo>): <resumo no imperativo>

<corpo opcional — o que mudou e por quê>

<rodapé opcional — refs, quebra de compatibilidade>
```

Exemplo mínimo:

```
fix(nav): lente da barra lateral para de ir para o Início ao abrir o Inbox
```

Exemplo completo:

```
feat(mixer): volume por aplicativo com medidor ao vivo

- Lê as sessões do Core Audio e agrupa por processo (várias sessões = uma linha)
- Ícone real do app e nome amigável vindo do executável
- Rolar sobre a linha ajusta de 2 em 2 (Shift = 10)
- Só coleta enquanto a página está aberta

Refs: #42
```

### Regras do resumo (a primeira linha)

| Regra | Detalhe |
|---|---|
| Tamanho | até **72 caracteres**, ideal até 60 |
| Modo | **imperativo**, como uma ordem: "corrige", "adiciona", "remove" — nunca "corrigido", "adicionando", "corrigido o bug" |
| Caixa | minúscula depois dos dois-pontos |
| Ponto final | **sem** ponto no fim do resumo (só no corpo) |
| Idioma | português, como o resto do projeto |
| Conteúdo | o **efeito para quem usa**, não o arquivo alterado ("ilha recolhida mostra o horário", não "mexi no IslandWindow.cs") |
| Um commit | **uma mudança**; se a frase precisa de "e" para caber, provavelmente são dois commits |

O corpo é separado do resumo por **uma linha em branco** (sem isso o Git junta tudo no assunto).
Quebre o corpo em ~72 colunas para o `git log` ficar legível.

---

## 2. Tipos

| Tipo | Quando usar | Entra no changelog? |
|---|---|---|
| `feat` | funcionalidade nova para quem usa o app | ✅ Novidades |
| `fix` | correção de um comportamento errado | ✅ Correções |
| `perf` | desempenho sem mudar o que o app faz (CPU, RAM, quadros) | ✅ Desempenho |
| `refactor` | reorganiza o código sem mudar comportamento | ❌ |
| `style` | formatação, espaços, nomes — nada de comportamento | ❌ |
| `docs` | README, `docs/`, comentários de documentação | ❌ (às vezes) |
| `test` | testes novos ou ajustados | ❌ |
| `build` | csproj, dependências (NAudio, Velopack), publish | ❌ |
| `ci` | `.github/workflows` | ❌ |
| `chore` | tarefas que não tocam o app: `.gitignore`, versão, ferramentas | ❌ |
| `release` | fechamento de versão (ver seção 6) | — |
| `revert` | desfaz um commit anterior (`revert: feat(mixer): …` + `This reverts commit <sha>`) | — |

`hotfix` **não** é tipo: correção urgente também é `fix`, e a urgência fica no corpo.

---

## 3. Escopos

Use o escopo de **área do produto** — é o vocabulário que já aparece nos commits (`feat(ui)`,
`fix(nav)`, `feat(sistema)`, `feat(mixer)`, `feat(ícone)`). Escolha **um** por commit:

**Páginas e módulos**
`inicio` · `midia` · `foco` · `ambiencia` · `inbox` · `produtividade` · `notas` · `tarefas` ·
`agenda` · `sistema` · `arquivos` · `clipboard` · `mixer` · `ia` · `espelho` · `ditado` · `paleta`

**Estrutura do app**
`ilha` (janela, tamanho, posição, estados) · `nav` (barra de seções, lente, páginas) ·
`anim` (motor de molas, perfis de abertura/fechamento) · `visual` (tokens, cores, fontes) ·
`config` (janela de Configurações) · `bandeja` · `icone` · `atalhos`

**Camadas técnicas**
`servicos` (Services/) · `core` (Core/) · `ui` (Ui/) · `cli` (comandos e pipe nomeado)

**Infra**
`deps` · `ci` · `release` · `docs` · `testes` · `build`

Se a mudança atravessa várias áreas, prefira o escopo que o usuário percebe (`ilha`, `nav`,
`visual`) ou deixe sem escopo quando for realmente transversal (`chore: remove bin/obj do repositório`).

---

## 4. Corpo

Opcional, mas **use quando o "porquê" não é óbvio** — é o que salva você daqui a seis meses.

- Marcadores com `-`, um assunto por linha.
- Diga **causa e efeito**, não só o arquivo: "a janela não era mais recortada por `SetWindowRgn`,
  então a área vazia engolia cliques" vale mais que "mexi no `UpdateRegion`".
- Nomes de arquivos, classes e mensagens de erro entre `crases`.
- Se houver medição, traga o número: "recolhido 0,5% CPU / p95 7,5 ms".
- Se algo ficou de fora, registre: "Fora desta rodada: i18n, MSIX".

---

## 5. Rodapés

| Rodapé | Uso |
|---|---|
| `Refs: #12` | issue relacionada |
| `Closes: #12` / `Fixes: #12` | encerra a issue ao entrar na `main` |
| `BREAKING CHANGE: <descrição>` | muda algo que o usuário já tinha configurado (settings.json, atalho, CLI) |
| `Co-authored-by: Nome <email>` | autoria compartilhada |

Quebra de compatibilidade vai com `!` no resumo **e** o rodapé:

```
feat(config)!: settings.json muda de formato e é migrado uma única vez

BREAKING CHANGE: quem já usava "Expandir ao clicar" é migrado automaticamente
(SettingsVersion 1); backups antigos são recusados se o esquema for mais novo.
```

---

## 6. Commits especiais do projeto

### Versão (`release`)
O projeto empacota pelo Velopack, que lê a `<Version>` do `TzIsland.csproj`: **versão só muda em
commit de release**, nunca misturada com código.

```
release: v3.2.0 — relógio na pílula e fonte Manrope

- Versão 3.2.0 no TzIsland.csproj
- README e STATUS (Etapa 26) atualizados
```

### Integração `testes` → `main`
O trabalho acontece na branch `testes` e entra na `main` por merge:

```
Merge testes → main: TzIsland 3.2.0 (relógio na pílula, Manrope)
```

---

## 7. Anti-padrões (o que evitar)

| ❌ Como já apareceu no histórico | ✅ Como fica |
|---|---|
| `novo design` | `feat(visual): novo visual Premium Night Black` |
| `Editando Estilos` | `style(visual): ajusta raios e sombras dos cartões` |
| `Adicionando arquivos` | `chore: adiciona estrutura inicial do projeto` |
| `Update .NET version in README` | `docs: atualiza a versão do .NET no README` |
| `Corrigido Ilhas Duplicadas, Texto Desalinhado…` | um `fix(ilha):` + um `fix(config):`, cada um com sua causa |
| `fix: v3.1.1 — cliques…` | `fix(ilha): cliques voltam a atravessar a área ao redor da ilha` (a versão fica no commit de release) |
| `Reescreve o sistema de animação e adiciona diagnóstico, backup e testes` | `refactor(anim): motor único de molas com posse por propriedade` (+ commits separados para diagnóstico, backup e testes) |
| `wip`, `ajustes`, `correções` | descreva o efeito real; se ainda não terminou, faça squash antes de entrar na `main` |

Checklist rápido antes de commitar:

1. O resumo diz **o que muda para quem usa**, no imperativo e sem ponto final?
2. Cabe em 72 caracteres?
3. É **uma** mudança só?
4. Tem escopo quando ajuda a achar o commit depois?
5. O corpo explica **por quê**, se não era óbvio?
6. Versão, build e `bin/obj` ficaram fora (ou no commit certo)?

---

## 8. Configuração opcional do Git

Template local (preenche o editor a cada commit):

```sh
git config commit.template docs/CONVENCAO-DE-COMMITS.md
```

Ou um template enxuto em `.gitmessage`:

```
# <tipo>(<escopo>): <resumo no imperativo, até 72 chars, sem ponto>
#
# Corpo (opcional): o que mudou e por quê
#
# Tipos: feat fix perf refactor style docs test build ci chore release revert
# Escopos: ilha nav anim visual config sistema mixer notas foco midia inicio
#          clipboard arquivos ia servicos core ui cli docs testes build ci release
# Rodapé: Refs: #12 · Closes: #12 · BREAKING CHANGE: ...
```

Checagem automática (opcional, sem instalar nada): o hook abaixo recusa resumo acima de 72
caracteres ou fora do formato `<tipo>(<escopo>): resumo`. Salve como `.git/hooks/commit-msg`
e rode `chmod +x .git/hooks/commit-msg`.

```sh
#!/bin/sh
msg=$(head -n1 "$1")
printf '%s' "$msg" | grep -Eq '^(feat|fix|perf|refactor|style|docs|test|build|ci|chore|release|revert)(\([a-z0-9-]+\))?!?: .+' \
  || { echo "Commit fora do padrão: <tipo>(<escopo>): resumo"; exit 1; }
[ ${#msg} -le 72 ] || { echo "Resumo com ${#msg} caracteres (máx. 72)"; exit 1; }
case "$msg" in *. ) echo "Sem ponto final no resumo"; exit 1;; esac
```
