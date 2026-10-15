# Encerramento da janela principal

Implementação: `WindowChrome` mantém a moldura redimensionável e os comportamentos Win32 da legenda (arrastar, duplo clique e menu de sistema). A legenda WPF apresenta Minimizar e Maximizar/Restaurar, sem X. Não são removidos `WS_SYSMENU`, `WS_MINIMIZEBOX` ou `WS_MAXIMIZEBOX`, nem interceptados `WM_CLOSE`, `WM_QUERYENDSESSION` ou `WM_ENDSESSION`. As outras janelas mantêm suas legendas existentes.

Referência: https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome?view=windowsdesktop-10.0

“Configurações > Encerrar aplicativo” pede confirmação, com Não como resposta padrão. Sim chama `Close()`, passando pela rotina existente de `Closing`: fechamento dos players (`MediaElement.Close`), gravação dos repositórios locais, parada do monitor e liberação da integração. Nenhuma confirmação foi adicionada a `Closing` ou ao encerramento da sessão. A integração experimental com o desktop não foi alterada.

## Verificação automatizada executada

Em 15/10/2026:

```powershell
dotnet build MuralInstitucional.csproj -p:BaseOutputPath=artifacts/close-window/
dotnet run --project Tests/MuralInstitucional.Checks.csproj -p:BaseOutputPath=artifacts/close-window/
```

Build aprovado, zero avisos e erros. As 132 verificações existentes passaram, incluindo persistência, liberação do transporte de vídeo e registro/cancelamento de interesse em cooperar. A saída foi isolada porque o executável padrão estava em uso; a tentativa no destino padrão falhou por bloqueio de arquivo. A suíte não valida a aparência da legenda, diálogos nativos nem desligamento real do Windows.

## Testes manuais necessários (pendentes)

1. Abrir a versão compilada e conferir ausência do X, presença e funcionamento de Minimizar e Maximizar/Restaurar. Testar arrastar, duplo clique, redimensionar e restaurar pela barra de tarefas, em diferentes DPI e monitores. Conferir os limites da janela maximizada e acesso à barra de tarefas.
2. Abrir Configurações > Encerrar aplicativo. Não, Escape e fechar o diálogo devem manter o mural aberto. Repetir e escolher Sim: a aplicação deve terminar sem processo remanescente. Repetir com uma janela de edição aberta.
3. Registrar interesse em cooperar, curtir, comentar e cadastrar um anúncio com vídeo. Reproduzir o vídeo e encerrar pelo menu. Reabrir, verificar os dados, e confirmar que o arquivo de vídeo pode ser renomeado após fechar (sem player retendo o arquivo).
4. Encerrar por Alt+F4 e pelo comando Fechar do menu de sistema (Alt+Espaço). Confirmar encerramento normal e persistência, sem a confirmação específica do menu Configurações.
5. Com vídeo em execução, solicitar encerramento normal pelo Gerenciador de Tarefas. Repetir com Finalizar processo: o Windows deve conseguir terminá-lo. Terminação forçada não executa `Closing`; não há garantia de gravação final nesse caso, embora as interações já sejam persistidas durante o uso e o sistema libere os recursos do processo.
6. Em sessão de teste, desligar/reiniciar e encerrar a sessão do Windows com o mural aberto, inclusive com a confirmação de saída aberta. Não deve aparecer bloqueio de sessão criado pelo aplicativo. Reabrir e verificar os dados previamente salvos.
7. Conferir filtros, edição, comentários, links, vídeos e “Tenho interesse em cooperar”. Janelas de edição/comentários devem continuar com X e comportamento anteriores. Conferir que Fixar permanece desabilitado e os controles de integração existentes permanecem iguais.

Estas instruções substituem as referências antigas a fechar a janela principal pelo X nos documentos anteriores.
