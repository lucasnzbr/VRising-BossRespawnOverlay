# Boss Respawn Overlay - documentacao tecnica

## 1. Estado final

- Versao final atual: `0.4.3`.
- Plugin separado do `SangrisInterface.dll`.
- BepInEx: Unity IL2CPP para VRising.
- DLL instalada: `BepInEx/plugins/BossRespawnOverlay.dll`.
- Dependencia obrigatoria: `BepInEx/plugins/SangrisInterface.dll`.
- GUID: `sangriafalls.vrising.bossrespawnoverlay`.
- Nome do plugin: `Boss Respawn Overlay`.

A versao `0.4.0` foi a primeira versao estavel da organizacao por atos. A `0.4.3` e a revisao final, incluindo a correcao de inicializacao IL2CPP para computadores com ordem de carregamento diferente.

## 2. O que o plugin faz

O plugin envia internamente o comando:

```text
.boss tempo <boss>
```

Ele captura as mensagens de resposta no ECS/chat do cliente, interpreta o tempo e remove apenas as respostas geradas pela consulta automatica. Comandos enviados manualmente pelo jogador nao devem ser consumidos pelo overlay.

O cronometro e local: depois de receber o tempo do servidor, ele diminui continuamente ate a proxima consulta.

## 3. Interface

### Botao circular

O overlay inicia minimizado. A bolinha continua visivel e alterna entre mostrar e esconder o painel.

### Arrastar

Arraste o cabecalho do painel. A posicao fica salva em `PositionX` e `PositionY`.

### Atos

Os grupos iniciam fechados e podem ser abertos individualmente:

| Ato | Nivel | Bosses |
|---|---:|---:|
| Ato 1 | 30-47 | Keely ate Quincey |
| Ato 2 | 50-68 | Beatrice ate Octavian |
| Ato 3 | 70-75 | Ziva ate Cyril |
| Ato 4 | 76+ | Magnus ate Adam |

Os atos abertos ficam salvos em `ExpandedActs`.

### Preferenciais

O botao `Fixar` move visualmente o boss para a secao `Preferenciais`. O botao muda para `Topo` quando o boss esta fixado.

Os nomes dos preferenciais ficam salvos em `PinnedBosses`, portanto continuam entre sessoes. A ordem visual nao altera a ordem base da lista.

### Botao Morto

`Morto` marca o boss como morto, zera o contador visual e faz uma consulta prioritaria daquele boss. A consulta prioritaria nao altera permanentemente a fila normal.

## 4. Polling

O intervalo entre comandos e aproximadamente 1 segundo. Sem preferenciais, os bosses percorrem a fila normal.

Com preferenciais, o agendamento e alternado assim:

```text
Preferencial 1 -> Normal 1 -> Preferencial 2 -> Normal 2 -> Preferencial 3 -> Normal 3
```

Os preferenciais possuem um ponteiro proprio e a fila normal outro ponteiro. Bosses fixados nao sao repetidos na fila normal.

Se existirem tres preferenciais, cada um tende a ser revisitado a cada seis consultas, desde que o servidor responda normalmente.

`PollIntervalSeconds` continua no arquivo de configuracao por compatibilidade, mas a cadencia atual e controlada pela fila de 1 segundo e pela alternancia preferencial/normal.

## 5. Lista e nomes especiais

O plugin possui 61 bosses, na ordem de nivel informada para o projeto.

Os identificadores que exigem atencao sao:

- `Willfred`: usa dois `l` no comando.
- `Barão`: usa o caractere `ã`; a grafia antiga `bar~ao` e migrada automaticamente.

O log do BepInEx registra a lista carregada no inicio com os comandos efetivamente consultados.

## 6. Configuracao

Arquivo:

```text
BepInEx/config/sangriafalls.vrising.bossrespawnoverlay.cfg
```

Chaves importantes:

| Secao | Chave | Funcao |
|---|---|---|
| General | `Enabled` | Liga/desliga o overlay |
| General | `InitialDelaySeconds` | Atraso antes do primeiro polling |
| General | `PollIntervalSeconds` | Chave legada; mantida por compatibilidade |
| Boss | `Bosses` | Lista de comandos separada por virgula |
| Boss | `PinnedBosses` | Preferenciais persistentes |
| UI | `ExpandedActs` | Atos abertos, por exemplo `1,3` |
| UI | `PanelWidth` | Largura minima recomendada: 420 |
| UI | `PanelHeight` | Altura e area de rolagem |
| UI | `PositionX` / `PositionY` | Posicao salva |
| UI | `FontSize` | Fonte configurada, limitada para preservar o layout |

## 7. Dependencias e distribuicao

Para distribuir para outro jogador, envie o `BossRespawnOverlay.dll` da mesma versao. O jogador precisa ter:

1. BepInEx Unity IL2CPP instalado para VRising.
2. `SangrisInterface.dll` na mesma pasta `BepInEx/plugins`.
3. A DLL do overlay na mesma pasta.

O `SangrisInterface.dll` e uma dependencia forte. Se estiver ausente, o BepInEx nao carrega o overlay.

O arquivo ZIP de distribuicao deve conter somente:

```text
BossRespawnOverlay.dll
```

O pacote final pronto para enviar esta em `releases/BossRespawnOverlay-0.4.3.zip`.

Preferencias e atos abertos sao configuracoes locais e nao acompanham a DLL.

## 8. Compilacao

A compilacao depende dos assemblies da instalacao local do VRising/BepInEx. A partir da pasta `BepInEx/plugins`:

```powershell
dotnet build BossRespawnOverlay\BossRespawnOverlay.csproj --no-restore
```

O alvo copia a DLL compilada para a pasta principal de plugins. Se o jogo estiver aberto, a DLL pode estar bloqueada. Nesse caso, apenas compile:

```powershell
dotnet build BossRespawnOverlay\BossRespawnOverlay.csproj --no-restore -p:SkipPluginCopy=true
```

Depois de fechar o jogo, faca a compilacao normal para instalar.

## 9. Correcao IL2CPP da versao 0.4.3

As versoes anteriores criavam `ComponentType.ReadOnly(...)` em um campo estatico da classe. Em algumas maquinas isso acontecia antes de os tipos IL2CPP estarem prontos e causava:

```text
Il2CppException: NullReferenceException
Unity.Entities.ComponentType.ReadOnly
```

Na `0.4.3`, os componentes ECS sao criados sob demanda, no primeiro polling, quando o mundo do cliente ja esta disponivel.

## 10. Pesquisa na documentacao do Sangria Falls

Consulta realizada em 19/07/2026:

- [Documentacao principal](https://sangriafalls.com/documentacao/)
- [Lista completa de comandos](https://sangriafalls.com/comandos/)
- [Interface Visual](https://sangriafalls.com/interface/)
- [Sistema de Missoes](https://sangriafalls.com/sistema-de-missoes/)
- [Classes](https://sangriafalls.com/classes/)
- [Sistema de Pets](https://sangriafalls.com/sistema-de-pets/)
- [Profissoes](https://sangriafalls.com/profissoes/)
- [Sistema de Prestigio](https://sangriafalls.com/sistema-de-prestigio/)
- [Moedas do servidor](https://sangriafalls.com/moedas/)
- [Passe de Batalha](https://sangriafalls.com/passe-de-batalha/)
- [Meditacao Vampirica](https://sangriafalls.com/meditacao-vampirica/)
- [Mercado Sombrio](https://sangriafalls.com/mercado-sombrio/)
- [Venda Automatica](https://sangriafalls.com/venda-automatica/)
- [Crusher Defender](https://sangriafalls.com/crusher/)
- [Arena Ilusional](https://sangriafalls.com/arena-ilusional/)
- [Dantos Sangrentum](https://sangriafalls.com/dantos-sangrentum/)
- [Mapa](https://sangriafalls.com/mapa/)

## 11. Ideias de overlays e facilitadores

### Prioridade alta: painel de progresso do personagem

Criar uma segunda aba usando respostas internas de comandos como:

- `.diaria ver` para missao diaria;
- `.passe ver` para progresso do passe;
- `.dobroxp ver` para tempo de dobro de experiencia;
- `.recompensa info` para proxima recompensa online;
- `.banco saldo` para moedas do banco;
- `.evo info` para evolucao de classe;
- `.prestigio buffs` ou `.prestigio lb` para buffs ativos.

Esses comandos aparecem na documentacao oficial e formam um painel de estado muito mais util no dia a dia do que varias mensagens no chat.

### Prioridade alta: painel de pet, almas e tier

Uma aba poderia reunir:

- pet ativo e status via `.pet ver`;
- caixas e organizacao via `.pet caixa` e `.pet l`;
- tiers via `.tier lista` e `.tier info`;
- almas por ato via `.alma resumo`;
- almas protegidas via `.alma protegidas`.

O foco deve ser leitura. Comandos que removem pets, caixas ou almas nao devem ser disparados automaticamente.

### Prioridade media: agenda de eventos

Um pequeno calendario/contador poderia mostrar eventos conhecidos, como Dantos diario as 20h, Chefe Supremo nos horarios publicados e Piracema no intervalo indicado no guia de farm. A agenda deve ser configuravel, pois horarios e eventos podem mudar.

Tambem seria possivel criar uma tela de evento ativo com botoes explicitos para comandos como `.arena entrar`, `.crusher entrar` e `.ds lutar`, sem envio automatico.

### Prioridade media: build planner

As paginas de Classes, Armas e Sangue possuem tabelas de atributos e sinergias. Um overlay local poderia permitir selecionar classe, arma e sangue e mostrar uma comparacao de atributos, sem precisar consultar o servidor.

Esse e um bom candidato para uma tela puramente informativa e de baixo risco.

### Prioridade media: atalhos controlados

Comandos de leitura podem ter botoes, por exemplo:

- `.mercado ir`;
- `.tp l` e `.tp ir [nome]`;
- `.meditar ir`;
- `.pet di`;
- `.prestigio exoforma`.

Cada acao deve exigir clique explicito. Nao e recomendavel transformar em automacao comandos com efeito irreversivel ou gasto de recursos.

### Nao automatizar sem confirmacao

Devem ficar fora de polling e ter confirmacao visual:

- `.banco vender tudo`, porque vende itens automaticamente e sem confirmacao;
- `.pet r` e `.pet remc`, que removem pets ou caixas;
- `.quest completar`, que gasta Sangricoins;
- comandos de prestigio, troca de classe e alteracao de atributos;
- qualquer comando que compre, venda, saque ou gaste moeda.

## 12. Arquitetura recomendada para uma futura suite

Se o projeto voltar a ser desenvolvido, a melhor evolucao e separar o codigo em camadas:

1. `InternalCommandTransport`: envia comandos e captura entidades de resposta.
2. `CommandRequestScheduler`: controla intervalo, prioridade e cancelamento por comando.
3. `ResponseParsers`: um parser por sistema, sem misturar boss, pet e banco.
4. `OverlayWindows`: janelas arrastaveis, abas e persistencia de layout.
5. `Modules`: Bosses, Personagem, Pets, Economia, Eventos e Build Planner.

O mecanismo atual de cancelamento de consulta automatica quando o jogador digita manualmente deve continuar sendo aplicado a todos os modulos.

## 13. Estado de encerramento

O Boss Respawn Overlay foi encerrado como projeto funcional na versao `0.4.3`. O codigo, configuracao e instrucoes estao nesta pasta. As ideias da secao 11 sao apenas backlog; nao foram implementadas nesta versao.
