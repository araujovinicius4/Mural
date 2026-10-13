# Validação — cadastro e gerenciamento de anúncios

Etapa de 13/10/2026, no projeto existente WPF/.NET 10. Foram implementados cadastro, edição e exclusão locais. **A interface completa e a reprodução real ainda precisam de teste manual no Windows.** A fixação permanece desabilitada; esta entrega não valida integração com o desktop.

## Funcionalidades implementadas

- Cabeçalho compacto com título, Novo anúncio, Configurações e Minimizar. Informações técnicas, caminhos e comandos existentes ficam em Configurações. O fechamento normal pela janela permanece disponível.
- `AnnouncementEditorWindow`: título obrigatório, descrição opcional, escolha de imagem ou vídeo local, prévia de imagem, URL opcional, Salvar e Cancelar. Vídeos são identificados na prévia, sem reprodução automática. O editor não abre links.
- Menu ⋯ em cada cartão com Editar e Excluir. Excluir exige confirmação. A edição preserva identificador, data de publicação, curtida, contador e comentários; a mídia do cartão é fechada antes da edição ou exclusão confirmada.
- Imagens PNG/JPG/JPEG e vídeos AVI/MP4/WMV. São verificados caminho local, existência, leitura, extensão, tamanho e assinatura do contêiner. Imagens também passam pelo decodificador WPF. Limites: 20 MiB por imagem, 250 MiB por vídeo e 40 milhões de pixels por imagem. Arquivos vazios e caminhos UNC são recusados.
- Mídia importada com nome GUID exclusivo, sem sobrescrever outros arquivos. O arquivo original selecionado permanece intacto e pode ser movido após o cadastro.
- Grade responsiva de três, duas ou uma coluna; mídia quadrada, recorte central sem distorção, cantos arredondados, curtidas e comentários abaixo da mídia. Os controles separados Reproduzir, Pausar e Reiniciar, links e rolagem foram preservados.
- Exemplos e cadastros são identificados nos cartões. Não há autenticação, servidor, publicação corporativa ou sincronização entre usuários.

## Organização e persistência

`Models/Announcement.cs` distingue exemplos dos cadastros. `Services/ManagedMediaService.cs` valida, importa e remove somente mídia pertencente ao aplicativo. `Services/AnnouncementManagementService.cs` coordena gravação e limpeza. `ViewModels/BoardViewModel.cs` mantém as coleções e interações. A interface está em `MainWindow`, `AnnouncementEditorWindow` e `Controls/AnnouncementCard`.

Arquivos no perfil do usuário:

```text
%LOCALAPPDATA%\MuralInstitucional\Demo\announcements.json
%LOCALAPPDATA%\MuralInstitucional\Publications\announcements.json
%LOCALAPPDATA%\MuralInstitucional\Media\<GUID>.<extensão>
```

O JSON demonstrativo existente continua sendo utilizado, sem migração destrutiva. Cadastros usam outro JSON com `DataKind = local-user`; exemplos continuam com `local-demo`. Curtidas e comentários acompanham o anúncio no respectivo arquivo. Caminhos de mídias antigas não são importados em massa; uma mídia selecionada na edição passa a ser copiada para a pasta gerenciada. Editar um exemplo com imagem gerada em memória exige selecionar um arquivo local para salvar.

A gravação usa arquivo temporário, flush e substituição do JSON, com bloqueio de escrita e comparação do conteúdo carregado para recusar alterações concorrentes. O arquivo admite até 100 anúncios e 5 MiB. Um JSON de cadastros inválido é preservado e bloqueia o gerenciamento nessa execução; não é substituído por dados vazios.

A mídia nova é copiada antes de gravar o cadastro; se a gravação falhar, a cópia é removida. A mídia antiga só é removida depois da gravação bem-sucedida e se nenhum outro anúncio a referenciar. A exclusão nunca remove o arquivo externo selecionado. Falhas na limpeza geram aviso e podem deixar uma cópia órfã. Uma interrupção entre gravação e limpeza também pode deixar arquivos órfãos; não existe varredura automática para apagá-los.

## Validação executada

Comandos concluídos:

```powershell
dotnet build -o bin/ManagementValidation
dotnet run --project Tests/MuralInstitucional.Checks.csproj -p:OutputPath=bin/ManagementValidation/
```

Resultado: **build sem erros ou avisos; 64 verificações automatizadas aprovadas**. O executor é local, sem dependências adicionais de frameworks de testes.

As verificações cobrem cadastro de imagem e vídeo, cópia exclusiva, reabertura do JSON, independência do arquivo original, edição preservando interações, substituição e exclusão de mídia, compartilhamento de uma mesma mídia, separação dos exemplos, validações de entrada, falhas de gravação com recuperação da cópia, proteção contra escrita desatualizada e preservação de JSON inválido. Também passaram as verificações anteriores de layout, recorte, curtidas, comentários, URLs e transições dos controles de vídeo.

O build no destino padrão foi tentado e falhou com MSB3021 porque uma instância anterior do aplicativo (PID 20196 durante a verificação) mantinha `bin/Debug/net10.0-windows/MuralInstitucional.exe` em uso. O processo não foi encerrado automaticamente. O build em saída alternativa compilou o projeto completo. Feche a instância antiga normalmente antes de executar a nova versão ou repetir `dotnet build` no destino padrão.

Os testes exercitam serviços e controles WPF fora da tela, com dados temporários. Não comprovam aparência da janela, diálogos, decodificação de vídeo ou liberação efetiva de arquivos por codecs. Não foi executado SetParent, nem instalado/executado software de wallpaper.

## Roteiro manual no Windows

1. Fechar a versão antiga normalmente e abrir `bin/ManagementValidation/MuralInstitucional.exe`, sem elevação. Conferir cabeçalho, Configurações, Minimizar e fechamento pelo X.
2. Cadastrar uma imagem horizontal e uma vertical. Conferir recorte central, quadrado e cantos arredondados, sem distorção. Redimensionar para três, duas e uma coluna e testar rolagem.
3. Cadastrar vídeo local suportado pelo Windows. Conferir ausência de autoplay; Reproduzir inicia/retoma, Pausar mantém a posição e Reiniciar volta ao início e reproduz. Conferir estados dos botões, fim de reprodução, minimizar e saída do viewport.
4. Curtir e comentar um cadastro, editar título/descrição/URL e substituir mídia. Confirmar preservação das interações e funcionamento independente dos controles. Editar vídeo enquanto reproduz e verificar que o editor abre e o arquivo antigo pode ser limpo.
5. Abrir Novo anúncio e cancelar depois de escolher arquivo. Conferir que nenhum cadastro ou cópia foi criado. Testar título vazio, arquivo inexistente, extensão incompatível e URL inválida; conferir mensagens claras.
6. Excluir primeiro escolhendo Não e depois Sim. Confirmar que Não preserva tudo, Sim remove o cartão e a cópia gerenciada, e o original externo permanece. Repetir com vídeo em reprodução.
7. Mover o arquivo original após cadastro e reabrir o aplicativo. Confirmar mídia, campos, curtidas e comentários persistidos, com exemplos separados dos cadastros.
8. Testar link HTTP/HTTPS: escolher URL no editor não deve abrir navegador; clicar na mídia do cartão deve abri-lo. Curtir, comentar e controlar vídeo não devem abrir o link.
9. Conferir que Fixar continua desabilitado, que a janela convencional pode ser movida e que Win + D mantém o comportamento convencional do Windows. Esta etapa não pretende permanecer visível após Win + D.
10. Encerrar e verificar ausência de alterações permanentes no Windows além dos dados locais do aplicativo. Não há serviços, Registro global, codecs instalados, AppBar, Topmost ou mudanças no Explorer.

## Limitações

Aceitar a assinatura AVI/MP4/WMV não garante codec disponível nem decodificação correta. O vídeo demonstrativo AVI já incluído continua disponível; MP4 e WMV exigem teste no Windows de destino, sem instalar codecs. A prévia de vídeo no editor é informativa.

Importação e validação são síncronas; arquivos grandes podem tornar o editor temporariamente ocupado. Os caminhos gravados são absolutos e não constituem formato portátil entre perfis. A proteção de escrita recusa conflitos, sem mesclar dados; versões antigas abertas não participam desse protocolo. A demonstração não é adequada a uma biblioteca grande, controle de acesso ou colaboração institucional em produção.

O mecanismo existente de recuperação e a lógica de integração ao desktop foram preservados. Nenhuma fixação experimental foi reativada ou considerada validada.
