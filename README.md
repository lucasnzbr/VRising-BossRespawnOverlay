# Boss Respawn Overlay

Addon client-side para o SangrisInterface do VRising/Sangria Falls.

## Versao final

`0.4.10`

O painel mostra o tempo de renascimento dos bosses sem deixar as respostas automaticas no chat.

- overlay inicia minimizada e pode ser arrastada;
- botão discreto de escala alterna entre 60% e 175% para adaptar a interface à resolução;
- bosses organizados por Ato 1 a 4;
- preferenciais ficam no topo e persistem entre sessoes;
- polling alternado: preferencial, fila normal, preferencial, fila normal;
- botao `Morto` dispara uma consulta imediata;
- texto verde indica vivo; vermelho indica morto.

## Instalacao

Coloque na pasta `BepInEx/plugins`:

```text
BossRespawnOverlay.dll
SangrisInterface.dll
```

O arquivo de configuracao e criado em:

```text
BepInEx/config/sangriafalls.vrising.bossrespawnoverlay.cfg
```

### Abrindo e movendo a overlay

O painel começa minimizado. Clique no ponto circular destacado no canto superior direito para abrir a interface. Depois, arraste o cabeçalho para mover a overlay; clique nele novamente para minimizar.

![Ponto para abrir e mover a overlay](assets/overlay-toggle.png)

Detalhes tecnicos, comandos de build, lista de bosses e ideias futuras estao em [DOCUMENTACAO.md](DOCUMENTACAO.md).

## Desenvolvimento

Requisitos:

- .NET SDK 6;
- uma instalacao local do VRising com BepInEx e SangrisInterface.

Compile informando a pasta do jogo:

```powershell
dotnet build .\BossRespawnOverlay.csproj -c Release -p:VRisingDir="C:\Program Files (x86)\Steam\steamapps\common\VRising"
```

Para copiar a DLL compilada diretamente para `BepInEx/plugins`, use `-p:DeployPlugin=true`.
Por padrao, o build apenas gera a DLL em `bin/Release`.

O projeto deve ficar fora de `BepInEx/plugins`; o BepInEx pode procurar DLLs também nas subpastas desse diretório.

## Site de download

O `index.html` na raiz é uma página estática pronta para a Vercel. Para publicar:

1. Crie ou importe este repositório na Vercel.
2. Use `Other` como framework preset.
3. Deixe o build command vazio e publique a raiz do projeto.

O arquivo `BossRespawnOverlay.dll` na raiz é o download apresentado na página. Depois de uma nova build, atualize o arquivo e o SHA-256 exibido em `index.html`.
