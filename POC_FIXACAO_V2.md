# Mural Institucional — prova de conceito de fixação V2

Data: 13/10/2026. Plataforma-alvo: Windows 11 Enterprise build 26100, C# / WPF / .NET 10.

**Resultado: nenhuma alternativa confiável foi identificada para cumprir simultaneamente todos os requisitos. Não foi implementado um novo modo de fixação. O código funcional e a recuperação existentes foram preservados.**

Este resultado segue a condição solicitada: quando não houver uma alternativa confiável, documentar as limitações e não implementar soluções improvisadas. Não constitui prova de impossibilidade de qualquer implementação futura; delimita o que as APIs e as evidências examinadas sustentam hoje.

## 1. Requisitos e decisão de implementação

O modo desejado precisa reunir, na mesma superfície, visibilidade à direita após Win + D, cobertura por outras janelas inclusive maximizadas, nenhuma reserva de área útil, rolagem com mouse, alternância Fixado/Desafixado e execução sem administrador. AppBar, Topmost, interceptação do atalho e mudanças no Explorer ficam excluídos.

A aplicação atual cumpre posicionamento, rolagem e operação convencional. Ela recusa a configuração WorkerW diagnosticada, em vez de apresentar uma janela invisível como uma fixação bem-sucedida. Não foi encontrada uma segunda estratégia que justifique trocar essa recusa por um novo caminho experimental.

**Isolamento da entrega:** apenas este relatório foi acrescentado. Não há novo executável, projeto, flag V2, estratégia de janela ou botão que sugira uma solução implementada. Não houve cópia/recriação do projeto WPF, alteração de fontes ou desativação das proteções. Gerar um projeto vazio ou duplicar a estratégia recusada não demonstraria viabilidade.

## 2. Evidências anteriores examinadas

Foram revisados [DIAGNOSTICO_FIXACAO.md](DIAGNOSTICO_FIXACAO.md), [ALTERNATIVAS_FIXACAO.md](ALTERNATIVAS_FIXACAO.md), a implementação de `DesktopIntegrationService`, seus diagnósticos, `MainWindow`, `RecoveryWindow` e o manifesto.

O diagnóstico anterior registra:

| Elemento | Evidência histórica | Consequência para a V2 |
| --- | --- | --- |
| WorkerW filho de Progman | Visível, desabilitado para interação, atrás de SHELLDLL_DefView. | Não reutilizar esse host nem relaxar a recusa existente. |
| Camada de ícones | Retângulo de 3840×1080, WS_EX_LAYERED, alpha global 255 sem color key, cobrindo a área do mural. | Há indício forte de cobertura; mudar somente o pai ou o renderer não resolve essa evidência. |
| Outros WorkerWs enumerados | Top-level invisíveis nos logs anteriores. | Não há outro host admissível demonstrado. Não mostrar/habilitar janelas do Explorer para fabricar um candidato. |
| Janela convencional | Retângulo de 1152×1032 à direita da área útil do monitor. | A geometria convencional não explica, isoladamente, o desaparecimento após integração. |
| Processo e resposta | Usuário confirmou processo ativo; host respondeu à consulta anterior. | Existência do processo e resposta Win32 não comprovam renderização visível. |

Os handles do relatório anterior são transitórios, não identificadores permanentes. Nesta etapa não se executou o aplicativo nem se fez nova tentativa de SetParent, mudança de owner ou alteração de ordem Z. As evidências históricas não foram convertidas em uma declaração sobre a topologia atual ao vivo.

O diagnóstico não examinou diretamente todos os pixels do compositor. Alpha global e WS_VISIBLE fundamentam a proteção conservadora, mas não substituem confirmação visual. A documentação admite que [IsWindowVisible](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowvisible) retorne verdadeiro mesmo quando outras janelas cobrem totalmente a janela consultada.

## 3. Investigação técnica

### 3.1. Janela independente sem moldura ou sem minimizar

WPF pode exibir uma janela própria com rolagem, sem ocupar área reservada. Entretanto, o aspecto visual de um painel não lhe atribui papel de conteúdo do desktop.

A Microsoft distingue Mostrar área de trabalho (Win + D) de Minimizar tudo (Win + M). Sua explicação histórica descreve que Mostrar área de trabalho também coloca o desktop à frente de janelas que não podem ser minimizadas. **Conclusão para a V2:** retirar a moldura ou o botão de minimizar não oferece imunidade documentada a Win + D. Essa referência não é uma especificação do compositor do build 26100. [Minimize All and Show Desktop](https://devblogs.microsoft.com/oldnewthing/20040527-00/?p=39153/).

`WS_EX_TOOLWINDOW` altera apresentação na barra de tarefas/Alt+Tab; `WS_EX_NOACTIVATE` altera ativação e tem implicações para foco/acessibilidade. Nenhum desses estilos declara uma janela interativa integrante do fundo do Explorer. [Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles).

### 3.2. Ordem Z com SetWindowPos

`HWND_BOTTOM` posiciona uma janela no fundo da ordem Z. Isso pode colocá-la atrás da própria superfície do Explorer. `HWND_TOP` a coloca na frente de outras janelas da mesma faixa; `SWP_NOACTIVATE` evita ativação, mas não impede cobertura visual. A API não define uma categoria estável de “janela acima do desktop e abaixo de todos os aplicativos”. [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos).

**Inferência de engenharia:** tentar manter essa categoria com timers ou callbacks que reajustam continuamente a janela introduziria disputa com o shell, oscilações e possíveis coberturas indevidas. Reexibir após Win + D também não garante a reversão normal no segundo acionamento. Essa opção não foi implementada nem testada.

Também não seria aceitável posicionar a janela em relação a um HWND topmost externo e assumir que ela continuará não topmost. A Microsoft documenta a possibilidade de herdar essa faixa por uma operação de ordem Z. [The Old New Thing: positioning beneath a topmost window](https://devblogs.microsoft.com/oldnewthing/20200101-00/?p=103285/).

### 3.3. Owner de Progman, sem SetParent

Esta hipótese foi avaliada separadamente: usar `SetWindowLongPtr(GWLP_HWNDPARENT)` no HWND próprio para atribuir um owner ao invés de transformar o mural em filho WorkerW. Para janelas top-level, esse índice altera ownership, não o parent de uma janela filha. [SetWindowLongPtrW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw).

A relação documentada mantém uma janela owned acima de seu owner e vincula aspectos de visibilidade e ciclo de vida a ele. Isso não assegura que ela permaneça abaixo de todos os outros aplicativos, não desapareça em Mostrar área de trabalho ou preserve entrada nos ícones. Usar Progman como owner acrescentaria dependência da atuação do shell sobre essa janela. [Window Features — Owned Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#owned-windows).

Há base documental para a operação de ownership; **não há, nas fontes consultadas, base suficiente para usá-la como solução confiável dos seis requisitos**. Não foi alterado owner de nenhuma janela e não se apresentou essa hipótese como uma implementação V2 validável.

### 3.4. Atributos DWM de Peek

`DWMWA_EXCLUDED_FROM_PEEK` evita o efeito de transparência durante Peek; `DWMWA_DISALLOW_PEEK` controla a prévia da janela. Peek não equivale a Mostrar área de trabalho por Win + D. Esses atributos não documentam isenção da minimização/ocultação e ordenação executadas por esse comando. [DWMWINDOWATTRIBUTE](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute).

Não foram aplicados. Uma captura visível durante Peek não seria evidência suficiente para aprovar Win + D.

### 3.5. Outro host do Explorer ou outro renderer

Um WorkerW diferente só seria admissível com nova evidência de que ele existe naturalmente, pertence ao shell correto e tem composição e interatividade compatíveis. Nenhum candidato assim foi demonstrado no material existente. O código atual usa funções públicas de descoberta, mas o papel de WorkerW/Progman como host de um aplicativo continua uma dependência interna do Explorer.

Tornar um mural filho direto de Progman abaixo de DefView mantém o problema potencial de cobertura; colocá-lo acima pode obstruir ícones na mesma região. Não há justificativa para repetir isso sem nova hipótese verificável. Foram descartadas mensagens de criação de WorkerW, mudança de alpha/estilos/estado enabled do shell e qualquer reinício do Explorer.

Trocar WPF por Direct2D/DirectComposition não remove uma camada que recobre o HWND. DirectComposition requer que o HWND alvo pertença ao processo chamador, e não permite por essa API associar diretamente conteúdo à janela do Explorer. [CreateTargetForHwnd](https://learn.microsoft.com/en-us/windows/win32/api/dcomp/nf-dcomp-idcompositiondevice-createtargetforhwnd).

### 3.6. Observação de eventos e rolagem do mouse

`SetWinEventHook` com `WINEVENT_OUTOFCONTEXT` permite observar eventos sem instalar hook de teclado no Explorer. Não concede prioridade de composição nem transforma uma janela própria em wallpaper. [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook).

Eventos de foreground, minimizar/restaurar e visibilidade podem ter causas distintas de Win + D. Portanto, sua interpretação como um estado exclusivo de Mostrar área de trabalho seria uma heurística. Não foi criado observador para restaurar/reordenar continuamente o mural.

Um ScrollViewer precisa receber entrada adequada. Uma janela coberta, desabilitada ou passada para trás do shell pode continuar renderizável em tese e não receber a roda do mouse. A documentação de janelas layered também descreve passagem de eventos em áreas transparentes; tornar uma sobreposição inteiramente permeável ao mouse não é equivalente a preservar rolagem interativa nela. [Window Features — Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows).

Não se propõe interceptar globalmente a roda nem redirecionar entrada do Explorer para disfarçar esse problema. A compatibilidade deve ser demonstrada por entrada normal do Windows, respeitando inclusive a configuração de rolagem de janelas inativas.

### 3.7. Alternativas que não satisfazem o escopo

| Alternativa | Motivo de exclusão nesta V2 |
| --- | --- |
| AppBar | Expressamente proibida; sua reserva de área também contraria o requisito de nenhum espaço reservado. |
| Topmost | Expressamente proibido; interfere na cobertura por janelas de outros aplicativos. |
| Imagem no wallpaper | Pode servir ao requisito passivo de leitura no desktop, mas não oferece rolagem com mouse nessa superfície. WPF separado não satisfaz esse requisito no modo fixado. |
| Wallpaper com alterações de quadros disparadas por hook de mouse | Transformaria a falta de interação em captura/redirecionamento global de entrada; não é uma alternativa confiável demonstrada. |
| Cancelar WM_SYSCOMMAND/WM_WINDOWPOSCHANGING para resistir ao shell | Operações locais não constituem contrato de integração; podem disputar o comando Mostrar área de trabalho. Não implementadas como contorno. |
| Registrar Win + D ou bloqueá-lo por hook | Expressamente proibido. |
| Modificar/habilitar/reordenar janelas do Explorer, injetar código, alterar políticas | Contraria as restrições. |

## 4. Estado dos requisitos

| Requisito | Código atual preservado | Resultado da investigação V2 |
| --- | --- | --- |
| Região direita e permanência após Win + D | Posicionamento convencional existe; permanência fixada não validada. | Nenhuma estratégia confiável identificada para ambos. |
| Outros aplicativos na frente, inclusive maximizados | Comportamento normal de uma janela não topmost; cenário visual continua a conferir. | Gestão automática de ordem Z não oferece garantia completa. |
| Nenhuma área reservada | Não há AppBar/reserva no código existente. | Mantido; nenhum mecanismo de reserva foi acrescentado. |
| Rolagem com mouse | ScrollViewer implementado na janela convencional. | Não há superfície fixada V2 com entrada demonstrada. |
| Alternar Fixado/Desafixado | Comandos experimentais existentes, com recusa segura do host diagnosticado. | Não acrescentado outro modo nem removida a recusa. |
| Sem administrador | Manifesto asInvoker e uiAccess=false preservado. | Nenhum requisito de elevação acrescentado. |

Não é correto somar características de alternativas diferentes e declarar sucesso: wallpaper visível mais ScrollViewer em uma janela convencional não produz um mural fixado rolável após Win + D.

## 5. Recuperação e limitações preservadas

O mecanismo existente foi mantido integralmente:

- Uma tentativa de integração por execução e bloqueio de novas tentativas após conclusão/falha.
- Recusa de host inadequado, contextos DPI incompatíveis e possíveis camadas de cobertura.
- Registro local de diagnósticos; mensagem na interface quando a gravação falha.
- Controle independente, criado antes da tentativa, com comandos de confirmação, restauração e fechamento.
- Prazo monotônico de 20 segundos sem confirmação visual e monitor de integridade.
- Atalhos existentes Ctrl+Alt+Shift+F12 para restaurar e Ctrl+Alt+Shift+F11 para fechar, com recusa se não puderem ser registrados.
- Restauração verificada de parentesco, estilos e geometria; janela convencional substituta se a restauração falhar.

Não houve teste novo de disparo de atalhos, prazo ou rollback nesta entrega. Permanece a ressalva já documentada sobre F12 reservado ao depurador; não se alteraram combinações de teclas. Timer e comandos dependem da thread WPF responsiva e da sobrevivência do processo. Não se atribui garantia de recuperação a crash/destruição do HWND ou bloqueio dentro de uma API nativa.

Essas proteções pertencem à implementação anterior. Não são evidência de uma nova integração V2.

## 6. Validação executada nesta etapa

```powershell
dotnet build
```

Resultado: **compilação com êxito, zero avisos e zero erros**. O projeto compilado permanece `MuralInstitucional.csproj`, alvo `net10.0-windows`; foram gerados os artefatos habituais em `bin`/`obj`.

Além da leitura das fontes, foram capturados hashes SHA-256 dos arquivos existentes para verificar sua preservação na entrega. A verificação de integridade não substitui teste funcional ou visual.

| Ação | Resultado/alcance |
| --- | --- |
| Revisão de código e diagnósticos anteriores | Concluída. |
| Consulta de documentação Microsoft sobre ownership, ordem Z, Peek e eventos | Concluída; fundamentos e limites descritos acima. |
| Build do projeto atual | Passou; não valida a fixação. |
| Implementação de executável V2 | Não realizada: não passou no critério técnico de viabilidade. |
| Reuso do WorkerW inadequado ou nova tentativa de SetParent | Não realizado. |
| Execução de sondagem de fixação ou alteração de owner/ordem Z | Não realizada. |
| Alteração de Explorer, wallpaper, Registro, serviços, políticas ou área útil | Não realizada por esta etapa. |
| Validação visual de Win + D, mouse e ícones pelo usuário | Não realizada; fixação não validada. |

## 7. Testes manuais necessários

O roteiro abaixo separa testes da aplicação existente de critérios para uma futura V2. **Não há um executável V2 a aprovar agora.** Não usar as flags antigas `--probe` para alegar validação de uma alternativa nova ou executar várias tentativas em busca de um host.

### 7.1. Regressão da janela convencional existente

Execute `dotnet run` em um terminal comum. Confira visualmente o posicionamento à direita e a barra de tarefas livre. Mova, redimensione, minimize/restaure, role até o último cartão e feche pelo X ou Fechar. Verifique o acesso aos ícones expostos, sem reposicioná-los para produzir um resultado favorável.

Pressione Win + D com outra aplicação aberta e novamente para restaurar. Registre o comportamento da janela convencional, **sem esperar nem declarar que ela permaneça fixada**. Esse teste caracteriza o fallback atual; não comprova o objetivo principal.

### 7.2. Critérios obrigatórios antes de aprovar uma futura V2

Somente após surgir uma alternativa fundamentada e uma implementação isolada, executar uma sessão de teste delimitada; não reutilizar o host recusado nem desabilitar proteções.

| Teste | Procedimento e resultado exigido |
| --- | --- |
| Win + D | Com cartões visíveis, abrir outro programa, pressionar Win + D e confirmar que os cartões continuam visíveis. Pressionar novamente e conferir o retorno normal das janelas. Repetir a ação do usuário dentro da mesma sessão de apresentação, sem novas tentativas de integração. |
| Janelas na frente | Abrir, mover, maximizar e restaurar outro programa sobre a região direita. Ele deve cobrir o mural integralmente; fechar/minimizar deve revelar o mural novamente. Nenhuma disputa visível de ordem Z. |
| Rolagem vertical | No desktop exposto, posicionar o mouse nos cartões e usar a roda até o último cartão e de volta. Testar a configuração corporativa de rolagem de janelas inativas. Não aceitar funcionamento apenas por encaminhamento global de mouse. |
| Ícones preservados | Selecionar e abrir ícones à esquerda e à direita, quando existentes. Não aceitar perda de acesso, mudança de posição ou captura indevida de cliques. |
| Fixar/Desafixar | Um comando explícito inicia a transição provisória; confirmar só ao ver os cartões. Desafixar deve devolver janela convencional, controles nativos e posição prevista. Qualquer novo ciclo deve respeitar o limite de tentativas da implementação. |
| Recuperação | Não confirmar e aguardar o prazo; usar controle separado e os atalhos; fechar o controle pelo X. Confirmar retorno seguro sem nova integração automática. |
| Encerrar | Fechar em ambos os modos e confirmar ausência de processo remanescente com Get-Process MuralInstitucional. Não aceitar alteração permanente do desktop para manter a imagem. |
| Nenhuma reserva | Comparar a área útil antes, durante e depois. Um aplicativo maximizado deve ocupar a mesma área útil, com a mesma barra de tarefas. Não basta observar que o mural cabe à direita. |
| Nenhuma alteração permanente | Comparar fundo, ícones, área útil e configurações relevantes antes/depois. Conferir que não houve criação de serviço, mudança de políticas/Registro/arquivos de sistema e que todos os recursos de janela/atalhos próprios foram liberados. |
| Compatibilidade | Registrar build, monitores e escalas DPI; verificar mudanças de resolução, monitores e sessão remota quando aplicáveis, sem reiniciar ou modificar o Explorer. |

A observação do usuário deve ser registrada por cenário. GetParent correto, WS_VISIBLE, ausência de DWM cloaked, processo ativo e retorno de APIs são apenas evidências auxiliares. Se Win + D, rolagem ou acesso aos ícones falhar, reprovar o modo fixado e manter a janela convencional.

## 8. Conclusão e próximo critério de avanço

O objetivo completo continua **sem solução confiável demonstrada no ambiente e sob as restrições especificadas**. Não se recomenda substituir a recusa atual por ownership de Progman, alterações de Peek ou laços de ordem Z sem evidência que resolva os requisitos faltantes.

O próximo avanço útil seria uma evidência técnica nova: host diferente naturalmente disponível e admissível, ou um contrato público que sustente persistência interativa sem AppBar/Topmost. Uma hipótese nova deve explicitar composição, entrada, ordem Z, cleanup e comportamento de Win + D antes de justificar implementação/teste.

Nesta entrega, o resultado é o relatório isolado e o build do projeto preservado. A **fixação V2 não foi implementada nem validada visualmente**; o modo convencional e a recuperação existentes continuam sendo a base operacional.
