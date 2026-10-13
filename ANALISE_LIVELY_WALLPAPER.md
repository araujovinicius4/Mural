# Análise do Lively Wallpaper para o Mural Institucional

Pesquisa em 13/10/2026. Somente análise; nenhuma mudança funcional, execução do Lively ou integração com o Explorer.

## 1. Resultado e recomendação

**O Lively oferece uma hipótese técnica nova em relação ao nosso host recusado: uma superfície própria, layered, filha de Progman e irmã do WorkerW. Entretanto, a implementação examinada não pode ser adaptada integralmente mantendo nossas restrições. Recomenda-se encerrar a implementação dessa linha nas condições atuais, preservando a hipótese como pesquisa condicionada a novas evidências.** Não há autorização nem recomendação para repetir a fixação anterior.

A diferença é relevante: não se trata apenas de selecionar outro WorkerW. Há um caminho de composição distinto. Porém, a preparação e a manutenção do caminho no Lively incluem ações sobre janelas do Explorer. Removê-las para cumprir nossas restrições produziria uma variante ainda não demonstrada, não uma solução comprovada pelo projeto.

Não foi demonstrado atendimento simultâneo aos seis requisitos no Windows 11 Enterprise build 26100, nem rolagem WPF nessa camada. O relatório anterior não constitui prova de impossibilidade; esta pesquisa acrescenta evidência de uma técnica específica e de seus obstáculos.

## 2. Fontes, versão e limites da coleta

O endereço solicitado redirecionou de `rocksdanister/lively` para `lively-community/lively`. A referência principal foi fixada no commit [25a0a5f4ed1988ff42117d3a17feb321062237fe](https://github.com/lively-community/lively/commit/25a0a5f4ed1988ff42117d3a17feb321062237fe), da branch `core-separation`, datado de 29/09/2026. Não presumimos que branch, release e instalação de terceiros sejam a mesma versão.

Foram lidos os três relatórios locais solicitados e os serviços atuais do mural. Os arquivos públicos relevantes do commit foram obtidos como **texto em pasta temporária**, sem clone completo, instalação, compilação ou execução do Lively. A primeira consulta de rede pelo shell isolado falhou; a leitura pública posterior conseguiu obter os arquivos e o SHA. Páginas da branch acessadas pelo navegador estavam parcialmente desatualizadas em relação ao commit; os nomes e as linhas abaixo usam os textos do SHA fixado.

Classificação das conclusões:

- **Código:** uma instrução ou fluxo existe no commit inspecionado; isso não comprova seu resultado visual neste computador.
- **Projeto:** declaração de release, documentação ou manifestação identificada do mantenedor.
- **Hipótese:** consequência esperada ou adaptação proposta, dependente de validação própria.

Não houve nova enumeração ao vivo das janelas, SetParent, SetWindowPos, alteração de estilos do shell, mensagem 0x052C ou reinício do Explorer. Não se verificou todo o repositório, todas as dependências, todos os renderizadores ou todas as políticas de uma instalação corporativa. Nenhum código-fonte do Lively foi incorporado ao Mural.

## 3. Arquitetura e mapa do código

O Lively separa interface de gerenciamento, núcleo de desktop e renderizadores. A interface WinUI não é, por si, o wallpaper. O contrato [IWallpaper](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/IWallpaper.cs) distingue `Handle`, `InputHandle`, processo, apresentação, reprodução e encerramento.

O adaptador [WebWebView2](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/Wallpapers/WebWebView2.cs#L177) inicia um player em `ShowAsync`. No tratamento de `msg_hwnd`, busca um HWND de apresentação e um `InputHandle` na hierarquia `Chrome_WidgetWin_0/1`; trata handles ausentes como erro. Interação de um player de navegador não equivale a eventos roteados de um `ScrollViewer` WPF.

### Núcleo de hospedagem — evidência do código

Referências em [WinDesktopCore.cs](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs):

| Métodos/linhas | Responsabilidade |
| --- | --- |
| [SetupDesktopLayer, 147](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L147) | Detecta WS_EX_NOREDIRECTIONBITMAP em Progman; envia 0x052C (0xD, 1); enumera DefView/WorkerW. |
| [TrySetWallpaperPerScreen, 517](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L517) | SetWindowPos, MapWindowPoints, hospedagem e geometria por monitor. |
| [TryAttachToDesktop, 1037](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L1037) | No desktop raised, com IsWorkerWParent false: WS_CHILD, alpha 255, SetParent em Progman, posição abaixo de DefView. Outro caminho: pai WorkerW. |
| [EnsureWorkerWZOrder, 1078](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L1078) | Pode mover WorkerW para HWND_BOTTOM. |
| [WorkerWHook_EventReceived, 249](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L249) | Reage à destruição do host. |
| [WndProc_TaskbarCreated, 1100](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L1100) | Compara PID do Explorer; restaura wallpapers após mudança. |
| [ResetWallpaperAsync, 581](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L581) | Fecha/recria conteúdo; repete descoberta após 500 ms se necessário. |

A descoberta clássica procura um WorkerW top-level seguinte ao host com DefView; a raised procura WorkerW filho de Progman. Não é seleção baseada em pixels. Mudanças de display e desbloqueio também são tratadas. O hook de destruição contém ressalva sobre reanexar ao novo thread. Não há comprovação visual nesse fluxo. Há ainda personalização opcional do wallpaper estático.

### Utilitários, configurações e recuperação

[DesktopUtil.cs](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively.Common/Helpers/Shell/DesktopUtil.cs) contém `GetProgman`, `GetDesktopWorkerW` e `GetDesktopSHELLDLL_DefView`: resolve janelas por classe e relação hierárquica. O utilitário também contém `SetDesktopIconVisibility`, com comando interno para DefView; sua existência não demonstra que o fluxo de fixação o invoque. Essa funcionalidade não deve ser transplantada.

[WindowUtil.cs](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively.Common/Helpers/WindowUtil.cs#L194) reúne `TrySetParent`, `SetWindowStyle`, `SetWindowTransparency` e `GetLastChildWindow`. A transparência acrescenta WS_EX_LAYERED e chama SetLayeredWindowAttributes. `TrySetParent` avalia o retorno não zero da API, sem a confirmação explícita de GetParent usada no mural. Um retorno de pai anterior não prova visibilidade. `GetLastChildWindow` usa EnumChildWindows, que inclui descendentes; seu nome não deve ser confundido com uma especificação pública da camada de wallpaper.

[SettingsModel.cs](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively.Models/SettingsModel.cs#L235) inicializa `IsWorkerWParent = false`; configurações persistidas podem selecionar outro comportamento. `InputForward` é inicialmente mouse. Valores padrão não certificam a configuração de uma instalação existente.

[WndProcMsgWindow.xaml.cs](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Views/WindowMsg/WndProcMsgWindow.xaml.cs) registra `TaskbarCreated` com RegisterWindowMessage e expõe o evento ao núcleo. Observar uma recriação espontânea é diferente de provocar reinício do Explorer. Os handlers examinados não encerram nem iniciam Explorer; o reset citado é do conteúdo e da integração do Lively.

[WatchdogProcess.Start](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/Watchdog/WatchdogProcess.cs#L21) inicia um processo auxiliar com comunicação por stdin, não instala um serviço Windows, apesar do texto do log. [Program.Main do watchdog](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively.Utility.Watchdog/Program.cs) aguarda o processo principal terminar, encerra players registrados e a UI e pede atualização do wallpaper com SystemParametersInfo. Isso não é equivalente ao controle independente e rollback do mural; não recomendamos copiá-lo.

## 4. Técnica e diferença em relação ao nosso diagnóstico

Diagrama conceitual do caminho raised, **não snapshot desta máquina**:

```text
Progman
├── SHELLDLL_DefView — ícones
├── superfície própria do wallpaper — layered
└── WorkerW — fundo do Windows
```

A intenção desse arranjo é integrar a superfície à composição do desktop. Como hipótese, isso explica por que ela poderia permanecer ao mostrar o desktop e ficar coberta pelos aplicativos, sem AppBar ou Topmost. O arranjo não fornece garantia pública de Win + D, e nenhum teste visual foi feito aqui.

No [diagnóstico local](DIAGNOSTICO_FIXACAO.md), Progman tinha exStyle `0x200080`, que inclui `WS_EX_NOREDIRECTIONBITMAP` (`0x200000`). Havia DefView layered como primeiro filho e WorkerW desabilitado atrás dele. **Inferência:** a topologia é compatível com os sinais de desktop raised, embora isso não demonstre que o novo arranjo renderize corretamente no nosso WPF.

Nosso código procurou hospedar o mural **dentro do WorkerW** e recusou o candidato antes de SetParent. O novo posicionamento conceitual seria uma superfície **irmã acima daquele WorkerW**, não torná-lo um host aceitável nem habilitá-lo. Portanto, a pesquisa não justifica reutilizar o candidato recusado.

Há uma ressalva importante sobre cobertura: alpha global 255 não mede o alpha de cada pixel do conteúdo de DefView. A documentação de [Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows) distingue composição e transparência. O comentário atribuído a Microsoft no código do Lively descreve DefView como majoritariamente transparente; não foi localizada nesta coleta uma especificação pública independente que transforme esse comentário em contrato de extensão.

Assim, a classificação anterior de possível cobertura permanece uma **proteção conservadora**, não prova de opacidade integral. Não removeremos a proteção com base nesse comentário. Outra superfície e outra ordem Z exigiriam diagnóstico e validação próprios.

## 5. Entrada do mouse e rolagem

[RawInputMsgWindow.Window_SourceInitialized e Hook](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Views/WindowMsg/RawInputMsgWindow.xaml.cs#L30) registram mouse/teclado com `ExInputSink`, recebem WM_INPUT e consultam GetCursorPos. O modo inicial da janela coletora inclui teclado, mesmo que a preferência de encaminhamento seja somente mouse. Isso merece atenção de privacidade; não significa gravação de teclas.

**A roda não está implementada nesse encaminhamento:** o ramo `MouseWheel`, na linha 123, contém lógica comentada e indicação de desativação/ausência de teste. Não existe evento de roda equivalente aos eventos ativos de movimento e clique nesse arquivo. Isso não prova que nenhum player possa receber roda por algum caminho nativo; prova que não podemos apresentar este mecanismo como solução pronta para rolagem.

[InputUtil](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively.Common/Helpers/InputUtil.cs) converte coordenadas por monitor/span e encaminha mensagens com PostMessageW. Seus métodos ativos contemplam movimento, botões e teclado, sem encaminhador de roda. Coordenadas de monitor inteiro não podem ser usadas diretamente para nosso painel deslocado à direita: a adaptação precisaria da origem cliente real, escalas DPI e representação correta de coordenadas negativas.

O mecanismo de Raw Input é uma API documentada, não um bloqueio de Win + D. [RAWINPUTDEVICE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice) informa que EXINPUTSINK recebe entrada de fundo sob condições, inclusive dependendo do registro pelo aplicativo em foreground; não é captura universal garantida. Observar entrada não transforma o alvo em destinatário nativo nem garante foco, acessibilidade ou gestos WPF.

O núcleo filtra cliques pela preferência e por `IsDesktop` (foreground igual a Progman ou ao host original). `ForwardMouseToWallpapers` seleciona monitor, categoria e `InputHandle`; movimento pode ser encaminhado fora do desktop por configuração. Esses filtros são verificáveis nos [handlers de entrada](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/Core/WinDesktopCore.cs#L1157), mas não equivalem a verificar cada ícone ou cada pixel exposto.

Para um mural corporativo, **hipótese de adaptação ainda não demonstrada:** preferir entrada nativa; se inviável, avaliar somente roda do mouse, sem teclado global, com filtro pela região realmente exposta e pelos ícones. Jamais encaminhar para cartões encobertos por programas. O foreground do desktop isoladamente não comprova que o cursor esteja numa área livre de ícone. Evitar duplo processamento pelo caminho nativo e pelo encaminhamento. Essas exigências permanecem trabalho novo.

## 6. Compatibilidade, privilégios e licença

**Documentado pelo projeto:** a [release v2.2.0.0](https://github.com/lively-community/lively/releases/tag/v2.2.0.0) anuncia compatibilidade com Windows 11 24H2 e reescrita de componentes para evitar loops de reinício de wallpapers. Também informa WebView2 como player web padrão. Isso sustenta que houve trabalho concreto de compatibilidade, mas não certifica Enterprise, WPF .NET 10, este ambiente, ou rolagem sob nossas restrições.

**Relato do mantenedor:** na [discussão #3004](https://github.com/lively-community/lively/discussions/3004), em novembro de 2025, ele associa mudanças recentes a problemas de entrada e hangs para alguns usuários. A discussão também tem relatos posteriores. São evidências de risco e histórico, não testes controlados nem prova de falha universal. Não adotamos como fatos receitas postadas por outros participantes na issue #2074.

**Privilégios no código:** o [manifesto do núcleo](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/src/Lively/Lively/app.manifest#L19) usa `asInvoker`, `uiAccess=false`. A hospedagem analisada não contém pedido de UAC. Isso não é auditoria de instaladores, plugins ou de toda a aplicação. Ser possível como usuário comum não elimina AppLocker/WDAC, bloqueios de runtime, EDR, UIPI e políticas de personalização. Nenhum desses controles deve ser alterado para obter sucesso.

**Licença verificada:** o [LICENSE no commit](https://github.com/lively-community/lively/blob/25a0a5f4ed1988ff42117d3a17feb321062237fe/LICENSE) contém GPL versão 3; o README declara GPL-v3. Não é licença permissiva. Ao distribuir uma obra derivada que incorpore código protegido, as condições aplicáveis incluem manter avisos/licença, identificar modificações, licenciar a obra coberta sob GPL e disponibilizar o código-fonte correspondente conforme a modalidade de distribuição (seções 4–6). Uso privado não exige automaticamente publicação geral do código. Dependências e arquivos com avisos próprios exigem análise individual. Antes de qualquer cópia ou distribuição corporativa, verificar o alcance jurídico e a compatibilidade do projeto. Esta entrega usa descrição técnica e links, sem transplantar implementação.

## 7. Viabilidade sob os seis requisitos

| Requisito | Avaliação para o Mural |
| --- | --- |
| Direita e persistência após Win + D | Hipótese plausível pela camada de composição; sem resultado local. Geometria parcial deve substituir geometria de monitor inteiro. |
| Aplicativos à frente, inclusive maximizados | Esperado conceitualmente na camada do desktop; exige testar ativação, restauração e ordem Z. |
| Nenhuma reserva | A hospedagem examinada não precisa de AppBar; verificar área útil antes/depois. |
| Rolagem vertical | Não demonstrada. O encaminhamento de roda inspecionado está desativado; WPF exige investigação específica. |
| Fixar/desafixar | Exige um adaptador novo e reversível. Fechar um wallpaper/player não equivale a restaurar moldura, owner, estilos e geometria de uma janela WPF. |
| Sem administrador | Manifesto asInvoker é evidência favorável; DPI entre processos, UIPI e políticas ainda podem impedir integração. |

As funções Win32 individuais são públicas, mas **WorkerW/Progman/DefView como pontos de extensão, mensagem 0x052C e reconhecimento da topologia por estilos não constituem API pública de wallpaper interativo**. [SetParent](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent) documenta gestão de estilos e problemas de DPI entre processos; não certifica Explorer como host WPF.

Comparação operacional:

| Aspecto | Mural atual | Risco da adaptação |
| --- | --- | --- |
| Preparação do shell | Inspeção; sem 0x052C. | O caminho Lively prepara estado interno não documentado. |
| Ordem do shell | Não altera janelas do Explorer. | Pode exigir mover WorkerW; incompatível com a restrição. |
| Falhas/tentativas | Uma tentativa por processo; rollback. | Restaurar automaticamente por eventos reintroduziria tentativas não autorizadas. |
| Recuperação | Controle separado, atalhos, prazo de 20 s e restauração convencional. | Devem ser preservados integralmente; não trocar por watchdog de players. |
| Confirmação | Visual obrigatória; recusa estrutural conservadora. | Não tratar sucesso Win32 ou declaração de release como aprovação visual. |

Principais riscos adicionais: HWND transitório/destruído, UI WPF bloqueada em chamadas síncronas, recorte e origem cliente errados, mudanças de monitores/DPI, RDP e desktops virtuais, ícones inacessíveis, entrada duplicada e conteúdo sensível em logs. Alterar somente estilos da janela própria não elimina a dependência interna do shell.

## 8. Decisão e plano de testes condicionado

**Decisão atual: não implementar uma PoC copiando o fluxo do Lively.** A descoberta melhora a explicação técnica, mas não atende ao critério de alternativa confiável sob as restrições. Encerrar essa linha de implementação nas condições atuais; manter modo convencional e mecanismos de recuperação sem mudança.

Uma futura hipótese limitada — superfície própria sob DefView, acima de WorkerW, usando somente a topologia já existente — só merece PoC se houver evidência de que funciona **sem enviar 0x052C nem reordenar qualquer janela do Explorer**. Isso não foi verificado. Não é autorização para testar agora nem para contornar a recusa atual. Se a restrição a modificar Explorer incluir todo novo filho de Progman, até essa variante estará excluída.

Plano para uma etapa futura expressamente autorizada:

1. **Antes de integração:** diagnóstico somente leitura de toda a hierarquia/ordem Z, PID, estilos, geometria cliente, DPI, composição e hit test. Confirmar condição diferente da antiga tentativa. Sem criar/habilitar/exibir host; se faltar condição necessária, encerrar o teste.
2. **Proposta revisável:** projeto/processo experimental separado, sem modificar o serviço atual; estado próprio capturado, uma tentativa por execução, log sem conteúdo sensível, controle de recuperação visível e restauração após 20 s sem confirmação. Não executar SetParent sem autorização específica. Revisar licença antes de incorporar qualquer fonte.
3. **Imagem:** usuário confirma os cartões à direita e o fundo restante; testa Win + D duas vezes, botão Mostrar desktop, minimizar/restaurar programas, abrir e maximizar janelas sobre o painel. A imagem não pode reaparecer sobre aplicativos nem capturar foco indevido.
4. **Entrada:** testar roda para cima/baixo sobre painel exposto e após Win + D, mouse de alta resolução, monitor com DPI distinto, ícones à esquerda e sob a região direita. Não acionar mural quando um aplicativo ou ícone recebe entrada. Sem interceptar atalhos, teclado global ou bloqueio do shell.
5. **Transições e segurança:** desafixar, movimentar/redimensionar/minimizar, fechar nos dois modos, recuperar sem botões do painel e verificar ausência de processo experimental remanescente. Respeitar limite; nova tentativa somente em nova execução deliberada. Falha leva ao modo convencional, sem reanexar automaticamente.
6. **Mudanças naturais:** testar bloqueio/desbloqueio e mudanças de display aprovadas; observar eventual recriação espontânea do Explorer sem provocá-la. HWND perdido deve resultar em recuperação, não reinício do Explorer.
7. **Ausência de efeito permanente:** comparar área útil, ícones, fundo, políticas e configurações antes/depois. Não aplicar screenshot ao wallpaper, instalar serviço ou alterar Registro/arquivos do sistema. Documentar cada resultado visual e reprovar se qualquer requisito falhar.

Nesta etapa, somente os arquivos e fluxos foram investigados. Nenhuma fixação nova, nenhuma rolagem fixada e nenhum comportamento de Win + D foram validados. O build anterior documentado em [POC_FIXACAO_V2.md](POC_FIXACAO_V2.md) não valida esta técnica; não foi executado novo build, pois a entrega altera apenas documentação.
