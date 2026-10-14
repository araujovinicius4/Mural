# Mural Institucional — diagnóstico da fixação

Projeto WPF existente em .NET 10. A interface convencional permanece funcional; a fixação em WorkerW é experimental e não foi validada visualmente.

> **Etapa atual: classificação e gerenciamento local de anúncios.** Tipo obrigatório (Demanda, Oferta ou Comunicado), selo colorido, filtros no cabeçalho e migração dos registros antigos para Comunicado. Validação atual: 87 verificações aprovadas; detalhes e testes manuais pendentes em [VALIDACAO_CLASSIFICACAO_ANUNCIOS.md](VALIDACAO_CLASSIFICACAO_ANUNCIOS.md). Novo anúncio, edição, exclusão confirmada e cópia de mídia para o perfil estão disponíveis. Resultados e testes manuais: [VALIDACAO_GERENCIAMENTO_ANUNCIOS.md](VALIDACAO_GERENCIAMENTO_ANUNCIOS.md). A etapa anterior aprovou 64 verificações automatizadas. Fixar está desabilitado em todas as execuções, incluindo `--probe`. As instruções de fixação abaixo são histórico; não habilitam nem validam integração nesta versão. Dados e diagnósticos ficam em `%LOCALAPPDATA%\MuralInstitucional`, sem gravação na instalação. O histórico do mural está em [VALIDACAO_MURAL_INTERATIVO.md](VALIDACAO_MURAL_INTERATIVO.md).

## Executar

Em um terminal comum, sem executar como administrador:

```powershell
dotnet build
dotnet run
```

A janela ocupa aproximadamente 60% da área útil à direita, respeita a barra de tarefas e contém cartões fictícios em grade com rolagem. No modo convencional pode ser movida, redimensionada, minimizada e fechada normalmente.

## Resultado do diagnóstico

Na sessão Windows Enterprise build 26100 examinada, o WorkerW selecionado é filho de Progman, está atrás de SHELLDLL_DefView e está desabilitado para interação. A camada de ícones é uma janela layered, informa alpha global 255 sem color key e cobre toda a área do host. É um indício forte de cobertura incompatível com o mural; não é uma confirmação visual dos pixels do compositor.

A versão anterior aceitava parentesco, estilos, geometria e IsWindowVisible como evidência estrutural. O relato do usuário confirmou que a janela desaparecia enquanto o processo continuava ativo. **Esse resultado não representa fixação visual funcional.**

Nesta configuração, a versão corrigida **recusa a fixação antes de SetParent**, mantém a janela convencional e apresenta a explicação. Não tenta colocar o mural acima dos ícones nem alterar a transparência do Explorer.

O relatório detalhado está em [DIAGNOSTICO_FIXACAO.md](DIAGNOSTICO_FIXACAO.md). [VALIDACAO.md](VALIDACAO.md) contém o histórico anterior, que não valida a versão atual.

## Proteções e recuperação

- Apenas uma tentativa de fixação por execução. O comando Fixar permanece na interface, mas fica desabilitado após a tentativa. Não existem retries, timers de integração ou tentativa de outro host após falha.
- A descoberta do Explorer é somente leitura. Foram removidas todas as mensagens experimentais 0x052C. Não são criados, habilitados, mostrados ou reordenados WorkerWs.
- O serviço recusa hosts desabilitados, contextos DPI incompatíveis e possíveis camadas de cobertura. Nenhum estilo ou atributo de janelas do Explorer é alterado.
- Quando houver um host admissível, a fixação é **provisória**. Uma janela independente chamada Controle do Mural, com entrada própria na barra de tarefas, permite confirmar a imagem, desafixar ou fechar. Ela não tem Owner, SetParent nem Topmost.
- É necessário clicar em **Estou vendo os cartões** no controle separado em até 20 segundos. Sem essa confirmação, o mural volta automaticamente ao modo convencional. O prazo usa relógio monotônico; mudar a hora do Windows não o prolonga. A recuperação depende de a thread de interface continuar respondendo.
- **Ctrl+Alt+Shift+F12** restaura a janela; **Ctrl+Alt+Shift+F11** fecha. Esses atalhos são registrados antes de SetParent e não dependem de os botões do mural estarem visíveis. Se o registro falhar, a integração é recusada.
- Fechar o Controle do Mural pelo X também cancela a fixação e restaura a janela. Esse controle pode minimizar com Win + D; use os atalhos ou sua entrada na barra de tarefas.
- A integridade é monitorada: parentesco, ordem Z, estilos, geometria, estado enabled/minimized/hung e DWM cloaked. A perda de integridade provoca desafixação, nunca nova integração.
- A restauração verifica parentesco, estilos e geometria. Se ela falhar, abre-se uma nova janela convencional e fecha-se a anterior. A nova janela também mantém o bloqueio de novas tentativas.

A confirmação de cartões visíveis pelo usuário não aprova automaticamente Win + D ou outros cenários. Quando confirmada, o controle separado permanece disponível para recuperação.

## Diagnóstico local

O caminho aparece na própria interface. O arquivo UTF-8 é anexado ao lado do executável:

```text
bin/Debug/net10.0-windows/desktop-integration.log
```

Cada linha contém data/hora com fuso e PID. São registrados:

- HWND, classe, PID, pai e owner do mural, WorkerW, Progman e camada de ícones.
- Filhos imediatos de Progman na ordem Z e possíveis camadas acima do WorkerW.
- Estilos e estilos estendidos em hexadecimal; visibilidade, enabled, minimized e hung.
- GetWindowRect, GetClientRect, origem do cliente em coordenadas de tela e DPI.
- DWM cloaked, HRESULT de consulta e atributos layered/alpha/color key quando disponíveis.
- Resultado de WM_NULL com timeout de 250 ms para verificar resposta do host. Essa consulta não solicita criação nem modifica a estrutura do Explorer.
- Três amostras de WindowFromPoint; identificam quem recebe o hit test, sem comprovar opacidade.
- Antes e depois de SetParent: pai solicitado, pai anterior, pai efetivo, erro Win32 e snapshots completos.
- Antes e depois de SetWindowPos: retângulo de tela, coordenadas convertidas para o cliente do pai, flags, retorno, erro e geometria efetiva.
- Restauração, recusa, confirmação visual pelo usuário e encerramento.

Se a pasta não permitir gravar o log, a interface informa o erro; recuperação e fechamento continuam funcionando. Não são gravados títulos de outras aplicações, capturas de tela ou comunicados reais.

Para observar somente a estrutura, sem tentar fixar:

```powershell
dotnet run -- --diagnose
```

O diagnóstico abre uma janela convencional, inspeciona a estrutura existente e fecha. Não executa SetParent.

Sondagens opcionais, cada uma com no máximo uma tentativa:

```powershell
dotnet run -- --probe
dotnet run -- --probe --probe-recovery
dotnet run -- --probe --probe-fail-after-parent
```

Não execute as três em sequência para procurar uma configuração que funcione. Use apenas a sondagem necessária ao diagnóstico.

`--probe` aguarda 1,5 segundo, verifica a estrutura, restaura e encerra. Saída `3` indica somente um ciclo estrutural provisório; saída `2` indica recusa/falha. **Nunca retorna aprovação visual automática.**

`--probe-recovery` aguarda 22 segundos sem confirmar a imagem. Saída `0` indica somente que a restauração automática ocorreu; `2` indica que não foi demonstrada, inclusive quando a fixação foi recusada antes do reparenting.

`--probe-fail-after-parent` injeta falha após SetParent para verificar rollback em um host admissível. Saída esperada `2`. As proteções não são contornadas: na configuração observada, a recusa ocorre antes da injeção.

## Testes manuais necessários

| Teste | Procedimento e critério |
| --- | --- |
| Modo convencional | Mover, redimensionar, minimizar/restaurar, rolar os cartões e fechar pelo X. Confirmar que a barra de tarefas permanece acessível. |
| Recusa segura no build 26100 | Clicar Fixar uma vez. Na estrutura observada, a janela deve permanecer convencional com mensagem de fixação não confiável; Fixar fica desabilitado. Confirmar visualmente o resultado e consultar o log. |
| Fixação provisória em host admissível | Se não for recusada, observar se os cartões realmente aparecem. Não confirmar se a janela desapareceu, ficou preta ou foi coberta. Usar o controle independente/atalhos para restaurar. |
| Recuperação automática | Se houver fixação provisória, não confirmar. Aguardar 20 segundos e conferir a volta da moldura, posição e barra de tarefas. |
| Recuperação independente | Usar Ctrl+Alt+Shift+F12 ou Controle do Mural pela barra de tarefas. Fechar o controle pelo X e verificar restauração. |
| Win + D | Somente após ver os cartões e confirmar, abrir outra janela e pressionar Win + D. Confirmar que o mural continua visível; pressionar novamente. Se falhar, restaurar e considerar a integração reprovada. |
| Outras aplicações | Confirmar que uma janela aberta sobre a região direita cobre o mural normalmente e que ele reaparece ao fechar/minimizar essa janela. |
| Ícones do desktop | Selecionar e abrir ícones à esquerda e, se existentes, à direita. Não deve haver mudança de posição ou perda de acesso. |
| Encerramento | Usar Fechar, o X no modo convencional ou Ctrl+Alt+Shift+F11 no modo provisório/fixado. Verificar ausência de processo remanescente com Get-Process MuralInstitucional. |

Não considerar a fixação validada sem confirmação visual do usuário e aprovação dos cenários essenciais. Nenhuma solução confiável foi demonstrada para a topologia opaca/desabilitada observada; o modo convencional é o resultado seguro nesta máquina.

## Limites e segurança

Sem Topmost, elevação, serviços, alterações do Registro, arquivos do sistema, reinício/modificação do Explorer ou contorno de políticas. O manifesto permanece asInvoker e uiAccess=false.

Os testes de resposta e os estilos Win32 não comprovam renderização efetiva pelo compositor. A thread de interface travada, a destruição da janela filha pelo Explorer ou um crash do processo podem impedir timer e atalhos; esta prova não promete recuperação nesses casos. Não foram validados múltiplos monitores/DPI, desktops virtuais, RDP ou outras políticas/builds.
#   M u r a l  
 