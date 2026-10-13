# Diagnóstico da janela que desaparece — 13/10/2026

## Problema e conclusão

O usuário confirmou no Windows 11 Enterprise build 26100 que clicar Fixar faz a janela desaparecer enquanto o processo continua ativo. Portanto, os resultados estruturais da versão anterior não demonstraram fixação visual funcional nem encerramento do processo.

A investigação desta revisão encontrou um WorkerW filho de Progman, abaixo de SHELLDLL_DefView, com WS_DISABLED. DefView ocupa toda a área do host e apresenta WS_EX_LAYERED, LWA_ALPHA e alpha global 255, sem color key. Os atributos apontam para uma camada de cobertura incompatível com o mural atrás dela. A causa visual exata não foi observada diretamente nos pixels; há também a possibilidade de diferenças de composição/renderização do WPF nesse host. Não foi demonstrada uma correção confiável da imagem nessa topologia.

O resultado seguro implementado é **recusar esse host antes de SetParent**, mantendo a janela convencional e uma mensagem clara. Não foi tentado mudar alpha, estilos, estado enabled ou ordem das janelas do Explorer, nem colocar o mural acima dos ícones.

## Evidências da sessão real

Diagnóstico somente leitura executado na sessão interativa 2, OS 10.0.26100.0, Explorer PID 15008. Handles são transitórios e servem apenas para correlacionar o log desta execução.

| Janela | Dados observados |
| --- | --- |
| Mural convencional | HWND 0x1106CC; parent/owner 0; style 0x16CF0000; exStyle 0x40100; visible/enabled true; minimized/hung false; DWM cloaked 0. |
| Geometria do mural | Tela (768,0)–(1920,1032), 1152×1032; cliente 1136×993; origem do cliente (776,31); DPI 96. Representa 60% de uma área útil de largura 1920. |
| Progman | HWND 0x10132; 3840×1080; style 0x96000000; exStyle 0x200080; visible/enabled true; cloaked 0. A área do shell abrange dois monitores; o mural utiliza a área útil do monitor escolhido. |
| Camada dos ícones | SHELLDLL_DefView 0x10136; primeiro filho de Progman na ordem Z; 3840×1080; exStyle 0x80000 (WS_EX_LAYERED); GetLayeredWindowAttributes true; alpha 255; flags 0x2 (LWA_ALPHA), sem LWA_COLORKEY. |
| WorkerW selecionado | HWND 0x7066A; segundo filho de Progman, atrás de DefView; style 0x58000000, com WS_CHILD/WS_VISIBLE/WS_DISABLED; exStyle 0; visible true, **enabled false**; 3840×1080; DPI 96; cloaked 0. |
| Resposta do host | WM_NULL retornou dentro do timeout de 250 ms; IsHungAppWindow false. Não há indício de host travado nessas amostras. |

Essas medidas não sustentam a hipótese de um deslocamento fora da área útil na janela convencional. A versão anterior também verificava a geometria após SetWindowPos. Nesta revisão, não foi forçado reparenting para obter novas medidas num host reconhecidamente inadequado. Os pontos de diagnóstico antes/depois de SetParent/SetWindowPos estão implementados para uma eventual configuração admissível.

Referências que delimitam a interpretação:

- [IsWindowVisible](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowvisible) consulta WS_VISIBLE na hierarquia; pode retornar true quando a janela está totalmente coberta ou recortada pelo pai.
- [GetLayeredWindowAttributes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlayeredwindowattributes) retorna alpha global e color key quando disponíveis. Falha de consulta não significa transparência; a composição por pixel não é uma confirmação visual do resultado WPF.
- [DwmGetWindowAttribute](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetwindowattribute) permite registrar o estado cloaked. Cloaked zero, isoladamente, também não garante que os cartões possam ser vistos.
- [WindowFromPoint](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-windowfrompoint) fornece evidência de hit test, não uma medição de pixels ou opacidade.

## Mudanças

1. Removidas todas as mensagens experimentais 0x052C ao Explorer. A descoberta agora apenas inspeciona hosts já existentes. Não há criação, exibição, habilitação ou reordenação de WorkerW.
2. Limite de uma tentativa por execução. Após recusa, desafixação ou rollback, Fixar fica desabilitado. Uma nova janela criada pela recuperação mantém esse limite. Nenhum timer faz novas tentativas.
3. Proteção contra host desabilitado e camada de cobertura acima do WorkerW. Para DefView, a proteção conservadora usa cobertura integral do retângulo do mural, alpha global 255 e ausência de color key; não presume transparência.
4. Captura do retângulo de destino em coordenadas de tela antes de SetParent. O ponto é convertido para o cliente do novo pai e a geometria efetiva é comparada após SetWindowPos.
5. Diagnóstico de pai/owner, estilos, retângulos de tela/cliente, DPI, ordem Z, enabled, minimized, hung, cloaked, alpha e hit test. Cada retorno de SetParent, SetWindowLongPtr e SetWindowPos é registrado com erro e estado efetivo. Logs ficam em arquivo local UTF-8, com PID; o caminho/erro de gravação aparece na interface.
6. Em um host admissível, fixação apenas provisória, com controle WPF independente criado antes de SetParent. Não tem Owner nem Topmost; possui sua própria entrada na barra de tarefas e comandos de confirmação, restauração e fechamento.
7. Sem confirmação de que os cartões estão visíveis em 20 segundos, restauração automática por relógio monotônico. Isso trata o caso em que Win32 informa visibilidade mas o usuário não vê o mural. O monitor não consegue medir pixels após uma confirmação; o usuário pode restaurar a qualquer momento pelo controle/atalhos.
8. Mantidos Ctrl+Alt+Shift+F12 para desafixar e Ctrl+Alt+Shift+F11 para fechar. Falha de registro impede fixação. Fechar o controle pelo X também restaura o mural.
9. O monitor verifica integridade e novas possíveis coberturas. Quando uma verificação falha, restaura; não tenta integrar novamente. Se restaurar o HWND falhar, cria uma janela convencional e fecha a anterior.
10. Sondagens não aprovam automaticamente a imagem: saída 3 para um ciclo meramente estrutural, 2 para recusa/falha; saída 0 da sondagem de recuperação representa somente restauração demonstrada.

## Validação desta revisão

| Teste executado | Resultado |
| --- | --- |
| `dotnet build` | Sucesso, zero avisos e zero erros. |
| `--diagnose` na sessão real | Saída 0, estrutura descrita acima, sem SetParent. |
| Uma única tentativa protegida `--probe` | Detectou a camada de ícones cobrindo o host; recusou **antes de SetParent e da troca dos estilos do mural**. |
| Resultado da recusa | Log confirmou modo convencional; verificação estrutural de filho do WorkerW false; comando Unpin retornou modo convencional. |
| Fechamento da sondagem | Saída 2, esperada para a recusa; nenhum processo de teste remanescente. |

Trechos da tentativa protegida, às 12:12 na sessão real:

```text
Possível cobertura acima do WorkerW: HWND=0x10136; class=SHELLDLL_DefView;
  screen=(0,0)-(3840,1080); alpha=255; layeredFlags=0x2
Fixação falhou: WorkerW está atrás de uma possível camada de cobertura.
  Fixação visual não confiável; janela convencional preservada. Modo convencional.
PROBE RESULT pinProvisional=False; structural=False; unpin=True;
  automaticallyRestored=False; visual=SEM CONFIRMAÇÃO; Win+D=não testado
```

A execução na sessão real exigiu acesso ao desktop interativo fora do isolamento da ferramenta. Isso não altera o manifesto asInvoker, não solicita UAC e não implementa elevação no aplicativo.

## Pendências de teste manual e limites

O roteiro está no [README.md](README.md). Ainda requer confirmação do usuário: janela convencional visível após a recusa, movimentação/redimensionamento/minimização, ícones acessíveis e fechamento pelos comandos visíveis.

O timer de 20 segundos, os atalhos acionados por teclado, o controle independente durante uma fixação real e o rollback após reparenting **não foram exercitados nesta revisão na sessão real**, porque o único host encontrado foi recusado. Os testes antigos de rollback pertencem à implementação anterior. Não foi contornada a proteção para produzir uma aparência de validação.

Win + D, renderização WPF, cobertura por outras aplicações, interação com cartões fixados, múltiplos monitores/DPI, RDP e desktops virtuais seguem não validados. Um bloqueio da thread de interface dentro de uma API nativa ou destruição/crash do processo/Explorer pode impedir timer e atalhos; não existe garantia de recuperação para essas situações nesta prova.

**Estado final:** operação convencional preservada; configuração WorkerW observada recusada; fixação visual não validada. Não há recomendação de uso fixado corporativo baseada nestes testes.
