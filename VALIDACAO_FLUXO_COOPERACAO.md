# Fluxo local de cooperação e distribuição

## Uso

1. Criar uma Demanda ou Oferta. No formulário, escolher distribuição geral, setores selecionados, assuntos/habilidades ou unidade. Para múltiplos setores/assuntos, informar um critério por linha; unidade aceita apenas um. Não há catálogo institucional conectado. Os critérios são metadados locais, não envio ou descoberta de destinatários em outros computadores.
2. Usar “Tenho interesse em cooperar”. No estado inicial, um segundo clique cancela o interesse, mantendo seu histórico. Abrir **⋯ > Detalhes e cooperação local** para encaminhar à análise; após avançar, o próprio botão passa a “Acompanhar cooperação”.
3. No papel de solicitante simulado, encaminhar para análise. No papel de responsável simulado, aceitar ou recusar a **solicitação de cooperação**, com justificativa objetiva opcional de até 500 caracteres. O aceite nunca é autorização para publicar o anúncio. Sem priorização automática por identidade, hierarquia ou amizade.
4. Depois do aceite, abrir a etapa de consentimentos. Cada papel deve marcar sua confirmação e registrar seu consentimento explicitamente. Um consentimento isolado não identifica nem permite iniciar a cooperação. Os dois consentimentos permitem mostrar somente os rótulos fictícios “Participante A” e “Responsável B”. Nenhum nome ou contato real é coletado.
5. Iniciar, concluir ou cancelar. Recusa, conclusão e cancelamento são terminais. É possível registrar nova solicitação local após um estado terminal; o histórico anterior permanece acessível pelo seletor de solicitações. A ordem da lista é a de registro, sem classificação por identidade.

Comunicados exibem distribuição nos detalhes, mas não possuem ações de cooperação. Converter uma Demanda/Oferta em Comunicado mantém os dados de cooperação armazenados e bloqueia registro, cancelamento, consentimento e avanço de estados; voltar à categoria anterior recupera o acesso.

## Dados e compatibilidade

- `CooperationInterest` permanece como vínculo do interesse atual deste perfil, preservando os campos antigos, data original e escopo local.
- `CooperationRequests` é uma coleção de solicitações com ID próprio, vínculo ao anúncio por `Interest.AnnouncementId`, estado, consentimentos separados e histórico datado. A estrutura permite múltiplas solicitações por anúncio, embora o perfil local registre apenas uma manifestação atual. Não há identificação autenticada de participantes.
- Na leitura de JSON antigo com interesse e sem a coleção, o interesse é copiado para uma solicitação em “Interesse registrado”, sem consentimentos. A migração é idempotente e o primeiro ID é derivado do anúncio, sem coleta de dados pessoais. Novas solicitações recebem IDs independentes.
- `Distribution` ausente assume distribuição geral. Os quatro modos e os critérios são persistidos. Edição e troca de mídia preservam distribuição, solicitações e interações.
- O envelope JSON continua na versão 1, com campos adicionais. Não abrir os dados atualizados em versões antigas do aplicativo: leitores antigos podem descartar os novos campos ao salvar. A proteção existente contra gravação concorrente continua funcionando.
- São validados IDs únicos, vínculo ao anúncio, estados e sequências, correspondência entre consentimentos e eventos, limites de texto e critérios. Limites: 500 solicitações por anúncio, 1.000 eventos por solicitação, 30 critérios de até 100 caracteres. Permanecem os limites gerais do repositório, inclusive JSON de 5 MB.
- JSON inconsistente de cadastro é preservado e tem a gravação bloqueada; dados demonstrativos inválidos são preservados em backup conforme o comportamento existente. Falhas de gravação mantêm a sessão em memória e exibem aviso de que não foi salva.

## Limitações e critérios de uso

Tudo ocorre no mesmo perfil, no mesmo computador. Qualquer pessoa com acesso ao perfil pode operar os dois papéis e alterar os arquivos. Não há backend, autenticação, comunicação em rede, anonimato seguro, autorização institucional ou auditoria inviolável. Os consentimentos são demonstrações locais, não comprovação de consentimento de pessoas reais. O histórico não deve receber dados pessoais.

A análise deve considerar necessidade/oferta, habilidades e disponibilidade. Não há aceite automático. O fluxo não autoriza desvio de função, descumprimento de obrigações institucionais nem substitui os procedimentos de aprovação aplicáveis na instituição.

A distribuição não filtra o mural por usuário nem envia notificações. Todos os anúncios do perfil continuam disponíveis pelos filtros existentes. Uma solução futura com múltiplos usuários precisará implementar identificação de participantes, controle de acesso, consentimento real, entrega e regras institucionais no backend; os IDs locais não fornecem essas garantias.

## Verificação automatizada — 15/10/2026

```powershell
dotnet build MuralInstitucional.csproj -p:BaseOutputPath=artifacts/coop-flow/
dotnet run --project Tests/MuralInstitucional.Checks.csproj -p:BaseOutputPath=artifacts/coop-flow/
```

Build aprovado com zero avisos e erros. **259 verificações aprovadas**, incluindo as 132 existentes e 127 novas:

- Migração e reabertura nos repositórios `local-demo` e `local-user`, com preservação de interesses, curtidas e comentários.
- Fluxo completo em Demanda e Oferta, consentimentos parciais e completos, ausência de identificação no aceite, recusa e cancelamento.
- Matriz das 64 combinações entre estados, bloqueio de saltos, estados terminais e justificativa longa.
- Persistência de histórico, IDs independentes em novas solicitações, edição, mudança de categoria e troca de mídia sem perda do fluxo/distribuição.
- Distribuição nos quatro modos e rejeição de critérios inválidos; falhas de integridade sem sobrescrever arquivos e proteção de dados inválidos na leitura.
- Instanciação da janela de detalhes, seleção entre solicitações e carregamento dos critérios no editor, sem abrir janelas visíveis ou reproduzir vídeos.

A saída de build foi isolada para evitar conflito com o executável padrão em uso. Os testes usam diretórios temporários e não migram os dados reais do perfil.

## Testes visuais pendentes

1. Criar e editar anúncios nos quatro modos de distribuição; testar vários setores/assuntos, unidade única, campos vazios, duplicados e alternância para geral. Reabrir e conferir os critérios nos detalhes.
2. Percorrer o fluxo em Demanda e Oferta, conferindo estado discreto no cartão, “Acompanhar cooperação”, ações habilitadas por etapa, justificativa e histórico. Cancelar ainda no registro, recusar na análise, cancelar após aceite e concluir após início. Registrar nova solicitação e selecionar o histórico anterior.
3. Confirmar que aceitar a solicitação não altera a publicação nem apresenta identificação fictícia. Registrar apenas um consentimento: identificação e início devem continuar indisponíveis. Com os dois, conferir os rótulos fictícios e início habilitado. Fechar/reabrir os detalhes entre essas etapas.
4. Converter anúncio com solicitação em Comunicado: não deve haver ações de cooperação. Voltar a Demanda/Oferta e conferir recuperação dos dados. Comunicados novos devem permitir apenas consulta de distribuição, sem fluxo.
5. Conferir rolagem, textos, foco e navegação por teclado nas janelas de detalhes e edição, em DPI de 100%, 150% e 200%, especialmente nas menores larguras. Validar ausência de cortes e sobreposições.
6. Conferir três colunas e comportamento responsivo, curtidas e coração vermelho, comentários, filtros, edição/exclusão, links e vídeos (reproduzir/pausar/reiniciar). Validar que janela principal, encerramento e controles de desktop mantêm o comportamento anterior.
7. Usar uma cópia dos JSONs reais anteriores para inspeção visual da migração. Conferir anúncios e interesses existentes, sem duplicação, antes de usar os dados atualizados no perfil principal.

## Arquivos

- Modelos: `Models/Announcement.cs`, `Models/CooperationRequest.cs` (novo), `Models/AnnouncementDistribution.cs` (novo).
- Serviços e view model: `Services/CooperationInterestService.cs`, `Services/AnnouncementRepository.cs`, `Services/AnnouncementManagementService.cs`, `ViewModels/BoardViewModel.cs`.
- Interface: `AnnouncementEditorWindow.xaml` e `.xaml.cs`, `Controls/AnnouncementCard.xaml` e `.xaml.cs`, `CooperationDetailsWindow.xaml` e `.xaml.cs` (novos).
- Testes: `Tests/CooperationFlowChecks.cs` (novo) e `Tests/Program.cs`.
- Documentação: `README.md` e este arquivo.

Sem alterações na janela principal, integração com desktop ou implementação de mídia. Nenhum commit ou push executado.
