# Validação — Interesse em cooperar

Data: 14/10/2026. Projeto existente C# + WPF / .NET 10, executado no Windows.

## Implementação

Demanda e Oferta exibem controles próprios abaixo da mídia, próximos a Curtir e Comentar. Comunicado oculta todo o bloco. Um único botão alterna entre “Tenho interesse em cooperar” e “✓ Interesse registrado”. O estado ativo usa fundo verde, borda verde e texto em seminegrito; continua habilitado e um novo clique cancela o interesse. A dica no estado ativo é “Clique para cancelar seu interesse”; ao cancelar, texto, cores e dica voltam ao estado inicial. Não há diálogo de confirmação. Uma mensagem discreta explica: demonstração local deste perfil, sem autenticação, coleta de identidade ou envio do interesse.

`LocalCooperationInterest` contém apenas `AnnouncementId`, `RegisteredAt` e o escopo fixo `local-demonstrative-profile`. O anúncio contém um registro opcional `CooperationInterest`: ausência/null significa sem interesse; um objeto significa interesse ativo. Há apenas um espaço por anúncio, sem lista ou contador de pessoas. O ID é o do anúncio, não uma identificação de usuário ou computador. Nenhum nome, conta Windows, CPF, matrícula, e-mail ou identificador de rastreamento é coletado.

O serviço `CooperationInterestService` concentra elegibilidade, registro idempotente, cancelamento e validação. O ViewModel verifica pertencimento à coleção e chama a persistência existente. Os comandos são independentes de curtidas e comentários. O modelo notifica os bindings ao registrar e cancelar, sem recarregar a mídia.

A edição copia o registro existente, preservando o identificador do anúncio. Se um anúncio passar para Comunicado, o interesse é preservado e os controles ficam ocultos; ao voltar para Demanda/Oferta, o interesse continua acessível. A exclusão remove o anúncio inteiro e, portanto, o interesse associado, no mesmo commit JSON.

## Armazenamento e integridade

Utiliza os JSONs existentes em `%LOCALAPPDATA%\MuralInstitucional\Demo\announcements.json` e `%LOCALAPPDATA%\MuralInstitucional\Publications\announcements.json`. Não cria arquivo de identidade, backend, banco ou comunicação entre computadores. Mantém versão 1 e campos anteriores.

Arquivos antigos sem `CooperationInterest` são carregados sem interesse ativo. Arquivos ausentes mantêm o comportamento existente: exemplos demonstrativos ou coleção de cadastros vazia. Registros com associação incorreta, data ausente ou escopo desconhecido são rejeitados. Na carga, cadastro inválido preserva o original e bloqueia gravação; exemplos inválidos preservam backup antes da recuperação existente. Na gravação, registro inconsistente é rejeitado antes de substituir o arquivo.

A gravação mantém o mecanismo existente de arquivo temporário, substituição e detecção de conflito entre instâncias. Se falhar, o estado fica apenas na sessão e o cabeçalho informa que as interações não foram salvas. Não houve alteração dos dados do perfil real durante os testes.

A separação entre registro, serviço de ciclo de vida local e persistência permite evolução futura. Identidade autenticada, notificações, aceite/recusa, consentimento mútuo e estados de cooperação não estão implementados. Não existe regra de reciprocidade obrigatória.

## Resultados automatizados

- `dotnet build -c Release`: aprovado, **0 avisos e 0 erros**.
- `dotnet run -c Release --project Tests/MuralInstitucional.Checks.csproj`: aprovado, **132 verificações**, saída 0 (87 regressões e 45 verificações de cooperação).
- `git diff --check`: sem erros de whitespace.

Na implementação anterior, a tentativa inicial em Debug falhou com MSB3027/MSB3021 porque `bin\Debug\net10.0-windows\MuralInstitucional.exe` estava em uso. Foi utilizado Release para validar sem encerrar a aplicação aberta. Para recompilar Debug, fechar a versão em execução.

Na simplificação do botão, a primeira tentativa de build Release coincidiu com a compilação da suíte e encontrou um arquivo intermediário bloqueado (CS2012). A execução sequencial posterior de `dotnet build -c Release` foi aprovada sem avisos ou erros.

Cobertura atual:

1. Bloco presente em Demanda e Oferta e oculto em Comunicado, por instanciação WPF e inspeção das propriedades de visibilidade.
2. Mídia permanece quadrada nas três categorias.
3. Registro associado ao ID e escopo demonstrativo; Comunicado e anúncio fora da coleção rejeitados.
4. Registro repetido idempotente, preservando o mesmo objeto e data.
5. Curtidas e comentários independentes do interesse.
6. Botão único: textos inicial/ativo, estado ativo habilitado, dica de cancelamento, mudança de cor, remoção da dica após cancelar e atualização dos bindings. Alternância registra e cancela pelo mesmo comando usado pelo cartão; anúncio fora da coleção e Comunicado são rejeitados.
7. Reabertura com interesse, curtida e comentário preservados; cancelamento persistido.
8. Oferta registra independentemente da Demanda.
9. Quatro filtros compatíveis com os interesses.
10. Edição, inclusive mudança de categoria, preserva interesse e demais interações.
11. Exclusão elimina o anúncio e a associação do JSON.
12. Compatibilidade com JSON anterior nos dois repositórios.
13. Rejeição de registros inconsistentes na carga e gravação, preservando dados originais.
14. Falha de gravação no registro/cancelamento informada ao usuário.
15. Link permanece disponível e controles de vídeo permanecem habilitados conforme estado, sem carregar automaticamente a fonte.

As regressões existentes também passaram: migração das categorias com comparação integral dos campos antigos, persistência sob filtros sem excluir anúncios ocultos, gerenciamento de mídia, conflitos de gravação, comentários, curtidas, validação de links, renderização de imagens fora da tela, grade de três colunas em 900 px (duas/uma em larguras menores) e transportes de vídeo.

Testes executados em diretórios temporários isolados, sem navegador, reprodução real de vídeo, integração com Explorer ou janela interativa. Não houve execução de SetParent, AppBar ou reativação da fixação experimental. Integração com desktop e distribuição institucional não foram modificadas.

## Testes manuais pendentes

- Abrir a versão Release no Windows e criar Demanda, Oferta e Comunicado. Conferir presença/ausência dos controles, posição abaixo da mídia e distinção de Curtir/Comentar.
- Usar o botão único por mouse e teclado: primeiro clique registra, segundo cancela. Conferir textos exatos, marca ✓, cores e foco; passar o mouse no estado ativo e conferir “Clique para cancelar seu interesse”. Após cancelar, conferir que a dica de cancelamento desaparece.
- Conferir legibilidade do estado ativo, contraste e quebra de texto nas três colunas, larguras menores e escalas DPI diferentes.
- Fechar e reabrir a aplicação após registrar e cancelar; conferir os estados nos cartões.
- Curtir/comentar antes e depois do interesse, editar título/mídia/categoria, confirmar preservação e excluir anúncio ativo.
- Alternar os quatro filtros, inclusive com vídeo reproduzindo, e conferir pausa/encerramento de cartões removidos, rolagem e responsividade.
- Abrir links e reproduzir, pausar, retomar e reiniciar vídeos reais; verificar que registrar/cancelar interesse não altera a reprodução.
- Conferir na interface o aviso de erro em uma falha controlada de gravação, usando cópia de teste dos dados.

**A validação visual interativa não foi executada e permanece pendente.** A execução real dos testes WPF no Windows sem janela visível não equivale à validação visual.
