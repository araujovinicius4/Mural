# Validação do mural interativo

Etapa de 13/10/2026 — projeto existente WPF, .NET 10, Windows 11 Enterprise. Fixação ao desktop não faz parte desta entrega.

> Revisão atual dos controles: **42 verificações aprovadas** e build em saída alternativa. A seção “Controles separados de reprodução” registra os resultados atuais; as seções anteriores preservam o histórico.

## Implementação

- Grade com no máximo três colunas: três a partir de 780 DIPs de largura disponível, duas a partir de 520 e uma abaixo disso. O limite usa a largura interna da grade, após margens e barra de rolagem, não a largura externa da janela. Linhas acomodam títulos/descrições sem comprimir a mídia; espaçamento uniforme e rolagem vertical.
- Cada superfície de mídia é medida e arranjada em 1:1 por `SquareMediaPanel`, independentemente da imagem, vídeo ou texto. O recorte é arredondado. Imagens e vídeos usam `UniformToFill`, com recorte central e sem esticar/deformar. Botões, título, descrição e data ficam fora da área quadrada.
- Nove anúncios demonstrativos: oito imagens vetoriais locais geradas em memória e um cartão de vídeo. Não foram baixadas imagens nem copiados ativos externos. A referência visual mencionada no pedido não estava anexada nesta etapa; a composição segue os requisitos escritos e ainda precisa de comparação com a referência pelo usuário.
- Modelos `Announcement`, `DemoComment` e `DemoDocument`: identificadores, título, descrição, tipo/caminho de mídia, URL opcional, data, contador inicial fictício, estado de curtida do perfil e comentários associados ao anúncio.
- Curtir/descurtir alterna um booleano por publicação; o contador é o valor fictício inicial mais zero ou uma curtida local. Persistir e reabrir não adiciona outro voto. Não há identificação nem contagem de pessoas reais.
- Comentários em janela própria, com lista, data e entrada de 1 a 1.000 caracteres. Texto é exibido como texto simples, não HTML. Limite demonstrativo de 500 comentários por publicação. Sem autenticação, nomes, e-mails ou moderação; o aviso pede evitar dados pessoais/sensíveis.
- Clique na mídia com URL abre o navegador padrão somente após ação explícita. Só HTTP/HTTPS absolutos com host e sem credenciais são aceitos. A indicação de link fica na mídia; os controles de curtida, comentário e vídeo são independentes. Sem URL, não há superfície de redirecionamento.
- Vídeo local escolhido pelo usuário: Reproduzir/Pausar, volume inicial 50%, sem autoplay. Fonte carregada apenas ao clicar em Reproduzir. Falhas e arquivos sem faixa de vídeo geram aviso e mantêm o aplicativo. É possível escolher outro arquivo. Não há vídeo incluído para presumir disponibilidade de codec.
- Reprodução pausada quando a mídia sai inteiramente do viewport ou a janela é minimizada; não retoma automaticamente ao voltar. Mídia fechada ao descarregar o cartão ou encerrar. Pause interrompe reprodução, mas pode manter recursos de mídia alocados; não há virtualização de uma biblioteca grande nesta demonstração.
- Janela convencional, reposicionamento à direita, movimentação, redimensionamento, minimizar e fechar preservados. Fixar permanece desabilitado, com explicação. `StartPin` também recusa sondagens `--probe`, antes de criar controle ou chamar `Pin`. Não executar sondagens antigas para validar esta etapa.
- Código de recuperação, controle independente, atalhos, confirmação e rollback mantidos; não foram exercitados nem revalidados. `DesktopIntegrationService` teve somente o destino de log e a criação da pasta de diagnóstico ajustados. Não houve SetParent, AppBar, Topmost, bloqueio de Win + D ou modificação do Explorer.

## Arquitetura e persistência

`Models` define o contrato local; `IAnnouncementRepository` permite futuramente substituir o carregamento por API. `AnnouncementRepository` controla JSON e validação; `BoardViewModel` concentra interações e avisos; `Controls` contém layout e apresentação; `CommentsWindow` trata a edição demonstrativa. A lógica Win32 continua separada no serviço existente. Não foram adicionados pacotes externos.

Dados: `%LOCALAPPDATA%\MuralInstitucional\Demo\announcements.json`.

Diagnóstico da janela: `%LOCALAPPDATA%\MuralInstitucional\Diagnostics\desktop-integration.log`.

O JSON identifica `Version = 1` e `DataKind = local-demo`. Dados iniciais e interações são salvos ao carregar e após cada alteração; há gravação adicional ao fechar. A gravação usa arquivo temporário na mesma pasta, flush e substituição do destino. Nenhuma mídia é copiada para a instalação; o vídeo persiste como referência ao caminho escolhido. Mover/excluir esse arquivo requer escolher outro. Um caminho pode conter o nome do perfil: não compartilhar o JSON sem revisar esses dados.

Arquivo ausente cria demonstração. JSON corrompido ou schema desconhecido é preservado numa cópia `.invalid-...`, com aviso, antes de reiniciar a demonstração. Falha de leitura/permissão preserva o arquivo inacessível, desativa gravação nessa execução e oferece dados em memória. Falha de gravação mantém interações na sessão e informa que não foram salvas. Não há tentativas de alterar permissões ou políticas.

Limites: até 100 anúncios e JSON de até 5 MB na leitura. Não editar o arquivo enquanto o aplicativo está aberto. Executar uma instância por perfil durante os testes: não há protocolo de sincronização entre instâncias, e duas instâncias podem sobrescrever alterações. Não há backend, autenticação, moderação, painel administrativo, atualização automática ou sincronização institucional implementados.

Para testar imagem local, com o aplicativo fechado, pode-se substituir `MediaPath` de um anúncio de imagem por caminho absoluto de PNG/JPEG válido no JSON, mantendo sua estrutura. `MediaKind` usa valores numéricos (`0` imagem, `1` vídeo). JSON aceita escape `\\` em caminhos Windows. As imagens `demo:n` são identificadores internos, não URLs. A mídia não é obtida por HTTP.

## Verificação automática

Não havia suíte de testes no projeto. Foi criado `Tests/MuralInstitucional.Checks.csproj`, executável de verificações sem pacotes de teste externos, com referência ao projeto existente e diretório temporário isolado. Não abre o mural principal, o Explorer, navegador ou vídeo. A instanciação de controles WPF ocorre sem janela visível e não é teste visual.

Comandos reproduzíveis:

```powershell
dotnet build
dotnet run --project Tests/MuralInstitucional.Checks.csproj
```

Cobertura: criação de JSON ausente, alternância de curtida, persistência sem voto duplicado, comentários válidos/inválidos e associação por ID, isolamento entre anúncios, persistência da referência de vídeo, HTTP/HTTPS e rejeição de esquemas/credenciais, backup de JSON inválido, schema desconhecido, falhas simuladas de leitura e gravação, Measure/Arrange em três larguras, proporção 1:1 com conteúdo extenso e instanciação do XAML do cartão com recorte arredondado.

Resultado: **27 verificações passaram, saída 0**, incluindo instanciação e layout do cartão com mídia de 262 × 262 DIPs e recorte aplicado. Esses testes não comprovam pixels, áudio, codecs, lançamento do navegador ou gestos do usuário.

`dotnet build`: **sucesso, zero avisos e zero erros**. O manifesto continua `asInvoker`, sem novas dependências externas.

## Roteiro manual obrigatório

Executar em terminal comum, sem administrador:

```powershell
dotnet run
```

| Cenário | Critério de aceitação — ainda não validado visualmente |
| --- | --- |
| Grade | Largura interna >= 780 DIPs: exatamente três colunas. Estreitar para duas/uma. Nenhuma quarta coluna ou rolagem horizontal. |
| Mídia | Superfície 1:1 em todas as larguras; cantos arredondados, recorte central. Testar imagens retrato/paisagem; título longo/comentários não alteram a proporção. |
| Rolagem | Roda do mouse sobre mídia, textos e botões alcança os nove anúncios; redimensionamento não corta controles. |
| Curtida | Alternar repetidamente: zero/uma curtida local. Fechar/reabrir: mesmo estado e contador, sem novo incremento. |
| Comentários | Abrir dois anúncios separadamente; adicionar texto, rejeitar vazio, observar contador e persistência após reabrir. Verificar lista longa com rolagem. |
| Links | Clicar em anúncio com indicação abre uma aba no navegador padrão. Curtir/comentar/reproduzir não abre URL. Sem URL, mídia não redireciona. URL inválida deve exibir erro sem abrir programa. Nenhum link abre na inicialização. |
| Vídeo | No cartão correspondente, escolher arquivo local com codec já disponível. Confirmar ausência de áudio/autoplay antes de Reproduzir; testar Pausar/retomar/final/reproduzir novamente. |
| Falha de mídia | Escolher arquivo inválido ou codec não disponível; verificar aviso e aplicativo utilizável. Remover arquivo com app fechado; reabrir e escolher outro. Não instalar codecs para fazer passar. |
| Vídeo fora de vista | Durante reprodução, rolar o cartão completamente para fora do viewport: áudio/reprodução devem pausar. Retornar: retomar somente pelo botão. Minimizar deve pausar. Fechar deve liberar o arquivo/processo. |
| Persistência | Verificar JSON em LocalAppData, reabrir e comparar likes/comentários/caminho de vídeo. Em cópia de teste, corromper JSON com app fechado: aviso, backup e novos dados. |
| Janela/encerramento | Mover, redimensionar, minimizar/restaurar, reposicionar e fechar pelo botão ou X. Confirmar ausência de processo remanescente e ausência de áudio. |
| Fixação/Windows | Fixar desabilitado com explicação. Win + D permanece convencional. Confirmar ícones/área útil/políticas intactos e nenhuma solicitação UAC. |

Suporte de vídeo depende do MediaElement e dos componentes multimídia disponíveis no Windows. Extensão de arquivo não garante suporte ao codec. A documentação oficial descreve [MediaElement](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.mediaelement?view=windowsdesktop-10.0) e seus modos de controle; a compatibilidade real nesta máquina exige o roteiro acima. Nenhum codec, extensão ou componente foi instalado.

**Nenhum teste visual ou de reprodução real foi declarado concluído.** Esta entrega não valida fixação ao desktop nem comportamento de wallpaper. Os relatórios anteriores permanecem como histórico da investigação.

## Revisão visual e vídeo local

### Alterações

A mídia ocupa agora a largura interna integral do cartão, sem a antiga borda de padding de 12 DIPs. O conteúdo textual tem margens próprias menores. Cartões curtos não esticam seu fundo branco até a altura do maior cartão da linha; a organização continua em três/duas/uma colunas e as linhas continuam alinhadas. Curtir e Comentar permanecem abaixo da superfície de mídia.

Foi encontrado e corrigido um problema real de layout: o tamanho desejado de uma imagem em modo cover podia expandir o espaço de uma grade interna, deixando o recorte deslocado. `SquareMediaPanel` passou a distribuir slots explícitos e `CoverImage` desenha com escala uniforme `max(larguraDestino/larguraOrigem, alturaDestino/alturaOrigem)`, centralizando o excedente e aplicando recorte. Não altera a proporção da imagem. As artes vetoriais demonstrativas também receberam limite explícito de 600 × 600 para que círculos decorativos fora da tela não aumentem suas dimensões. A geometria arredondada continua aplicada à superfície inteira.

Vídeos recebem posicionamento central calculado pela proporção nativa após MediaOpened. O novo botão **Reiniciar** volta a posição para zero e inicia a reprodução por ação explícita, tanto durante reprodução quanto após pausa/fim. Reproduzir alterna com Pausar. A política de pausa fora do viewport/minimização e fechamento de recursos permanece. O preenchimento e os comandos de vídeo ainda precisam de validação real no Windows.

### Vídeo incluído e licença

`Assets/demo-mural.avi`: animação geométrica original de três segundos, 160 × 90 pixels, 12 fps, 36 quadros, sem áudio, 1.556.296 bytes. Foi criada localmente por `Tools/GenerateDemoVideo.ps1`, usando somente escrita de arquivo com classes .NET. Não há filmagem, fontes, pessoas, música ou material baixado de terceiros. Vídeo e gerador têm licença MIT em [Assets/VIDEO_LICENSE.md](Assets/VIDEO_LICENSE.md), copiada junto com o ativo para a saída e publicação.

SHA-256: `BEB412474BE64A9BA408F55F5504891CE3CFAFEAE29FCE65FB89EDCB404ED80E`.

Não havia `ffmpeg`/codificador H.264 disponível no PATH. A alternativa entregue é **AVI DIB/BI_RGB de 24 bits sem compressão**, não MP4/H.264. Não foram instalados encoders, codecs ou extensões. O gerador segue a estrutura descrita na [referência AVI RIFF da Microsoft](https://learn.microsoft.com/en-us/windows/win32/directshow/avi-riff-file-reference); isso valida o formato gerado, não prova suporte de reprodução de toda instalação do MediaElement. Se o Windows não puder reproduzi-lo, há aviso e a opção de escolher outro arquivo local continua disponível.

No cartão de vídeo, clicar em **Vídeo demonstrativo** seleciona o ativo incluído e persiste seu caminho pelo mesmo mecanismo JSON. A seleção não inicia a reprodução. Não substituímos automaticamente o vídeo já escolhido pelo usuário, nem migramos/reescrevemos suas curtidas e comentários. Depois, clicar em Reproduzir. O arquivo permanece na saída do aplicativo; o caminho JSON precisa ser atualizado se a instalação for movida.

### Resultados desta revisão

O comando `dotnet build` padrão chegou à compilação, mas falhou na cópia do apphost com MSB3027/MSB3021: o executável em `bin/Debug/net10.0-windows` estava em uso. A instância aberta não foi encerrada. Foi então executado:

```powershell
dotnet build -o bin/Validation
dotnet run --project Tests/MuralInstitucional.Checks.csproj -p:OutputPath=bin/Validation/
```

Build alternativo: **zero avisos e zero erros**. Suíte: **32 verificações aprovadas, saída 0**. Além dos testes anteriores, há renderização offscreen real de imagens paisagem/retrato com amostragem de pixels no topo/lateral, comprovando preenchimento central nesses casos; verificação do cabeçalho/tamanho/metadados do AVI; e controles Reproduzir/Reiniciar habilitados sem atribuir Source nem iniciar reprodução no carregamento. Não foi decodificado vídeo nessa suíte. Recuperação, integração com desktop, bloqueio de fixação, links e persistência não tiveram alterações funcionais nesta revisão; os testes de links/JSON continuam passando.

Para executar a nova versão sem sobrescrever a instância antiga, pode-se abrir `bin/Validation/MuralInstitucional.exe`. Para uso normal, fechar a janela antiga pelo X/Fechar antes de executar `dotnet run`. Evitar manter duas instâncias em uso com o mesmo JSON.

### Validação manual pendente

1. Confirmar imagens preenchendo toda a área quadrada, recorte central, cantos arredondados e ausência de deformação em fotos paisagem/retrato; verificar três colunas e adaptação ao estreitar.
2. Verificar redução de padding e fundos brancos sem espaços excessivos; Curtir/Comentar sempre abaixo da mídia, com links e contadores independentes.
3. Selecionar Vídeo demonstrativo, confirmar nenhum autoplay, clicar Reproduzir e observar movimento do círculo/barra. Pausar deve congelar; Reiniciar deve começar novamente do primeiro quadro. Testar Reiniciar durante pausa, reprodução e depois do fim.
4. Confirmar vídeo paisagem cobrindo o quadrado por recorte central, sem deformação. Rolagem para fora da área e minimização devem pausar; retornar não deve disparar reprodução automática.
5. Escolher MP4/H.264 local já disponível, se houver, e repetir controles/falhas. O vídeo AVI silencioso não valida áudio; testar áudio apenas com arquivo local escolhido deliberadamente. Não instalar codecs para fazer passar.
6. Confirmar persistência de caminho, curtidas e comentários ao reabrir; testar arquivo removido/incompatível e mensagem de erro. Fechar normalmente e confirmar ausência de áudio/processo remanescente.

**Renderização offscreen foi validada automaticamente; aparência da janela e reprodução/pausa/reinício com codecs reais continuam pendentes de teste manual.** Nenhuma integração com Explorer foi executada ou reativada.

## Controles separados de reprodução

O botão **Pausar** foi acrescentado entre Reproduzir e Reiniciar. Reproduzir deixou de alternar com pausa: seus rótulos e funções agora são independentes. `VideoPlaybackController` concentra as transições e disponibilização de comandos; o cartão continua executando as operações do MediaElement e tratando falhas.

| Estado | Reproduzir | Pausar | Reiniciar |
| --- | --- | --- | --- |
| Arquivo indisponível/falha | Desabilitado | Desabilitado | Desabilitado |
| Parado/arquivo selecionado/fim | Habilitado | Desabilitado | Habilitado |
| Reproduzindo (inclusive abertura solicitada) | Desabilitado | Habilitado | Habilitado |
| Pausado | Habilitado | Desabilitado | Habilitado |

Reproduzir chama Play sem mudar Position, permitindo retomar. Pausar chama Pause sem Stop, Close ou seek. Reiniciar chama Stop, atribui Position zero e inicia Play. Ao terminar, o player volta a zero e o estado fica parado. Pausa por rolagem/minimização utiliza o mesmo controle. Falha de mídia desabilita os comandos; escolher outro arquivo ou selecionar novamente o demonstrativo reavalia sua disponibilidade. Carregar um cartão continua sem autoplay.

Não houve alteração de layout da mídia, proporção quadrada, recorte, cantos arredondados, número de colunas, JSON, curtidas, comentários, links, recuperação ou integração com desktop.

Validação executada:

- `dotnet build`: falhou ao substituir o apphost em uso, com MSB3027/MSB3021; a instância aberta foi preservada.
- `dotnet build -o bin/Validation`: **sucesso, zero avisos e zero erros**.
- `dotnet run --project Tests/MuralInstitucional.Checks.csproj -p:OutputPath=bin/Validation/`: **42 verificações aprovadas, saída 0**.

Os dez novos checks cobrem o botão Pausar separado no XAML, comandos indisponíveis sem vídeo, estados parado/reproduzindo/pausado, prevenção de comandos duplicados, preservação de posição na pausa/retomada, reinício durante pausa e reprodução, término e indisponibilidade após fechamento/falha. As posições foram verificadas com um player simulado por delegates: são testes de semântica dos comandos, não de decodificação. Os testes anteriores de layout, renderização offscreen, links e persistência continuam passando.

Validação manual ainda necessária na nova versão:

1. Selecionar Vídeo demonstrativo ou vídeo local compatível; confirmar Reproduzir/Reiniciar habilitados e Pausar desabilitado antes de iniciar.
2. Reproduzir: confirmar movimento e troca correta dos botões. Pausar no meio: a imagem deve congelar na posição atual. Aguardar e retomar com Reproduzir: deve continuar dali, sem voltar ao início.
3. Reiniciar durante reprodução e durante pausa: confirmar retorno ao primeiro quadro e reprodução imediata, com Pausar habilitado.
4. Esperar o fim: confirmar estado parado e possibilidade de Reproduzir ou Reiniciar novamente.
5. Rolar para fora do viewport/minimizar durante reprodução: confirmar pausa e retomada somente por comando explícito.
6. Testar arquivo inválido/removido, escolha de outro vídeo, fechamento normal e ausência de reprodução residual. Confirmar visualmente os três comandos em janela estreita, sem cortes; o WrapPanel pode distribuí-los em mais de uma linha.

Não houve reprodução real nem inspeção visual da janela nesta revisão. Fechar a instância antiga normalmente antes de abrir `bin/Validation/MuralInstitucional.exe`, evitando duas instâncias gravando no mesmo JSON. O build em saída separada não atualiza a janela que já estava aberta.
