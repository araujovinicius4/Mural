# Validação da classificação dos anúncios

Data: 14/10/2026.

Implementação no projeto C# / WPF existente (.NET 10). Tipos, nomes e cores centralizados em `Models/AnnouncementTypes.cs`. Cadastro exige seleção explícita; edição carrega a categoria atual e permite alterá-la. Categorias inválidas são rejeitadas pelo serviço e pelo repositório.

O selo no canto superior esquerdo da mídia usa fundo colorido, texto escuro e não captura cliques. Fica abaixo da faixa do link externo para evitar sobreposição em cartões estreitos. Filtros no cabeçalho: Todos, Demandas, Ofertas e Comunicados. O filtro usa uma visualização da coleção; a persistência sempre usa a coleção completa. Cadastro, edição e exclusão atualizam essa visualização.

JSON mantém a versão 1 com o campo adicional `Type`, gravado como `Demanda`, `Oferta` ou `Comunicado`. Ausência do campo em arquivos antigos assume `Comunicado`; ao abrir o mural, os repositórios de exemplos e cadastros são salvos pelo mecanismo existente de gravação atômica e controle de concorrência. Nenhum campo antigo é descartado do modelo. Arquivos inválidos continuam sujeitos às proteções existentes.

## Execução automatizada

- `dotnet build`: aprovado, zero avisos e zero erros.
- `dotnet run --project Tests/MuralInstitucional.Checks.csproj`: aprovado, **87 verificações**.
- Novos testes: migração dos dois repositórios com comparação integral do JSON antes/depois (removendo apenas o novo campo), persistência imediata da migração, reabertura com curtidas e comentários, persistência das três categorias, quatro filtros com ordem preservada, salvar sob cada filtro sem perder registros, coleção vazia, inserção/edição/exclusão sob filtro e rejeição de categoria ausente/inválida.
- Testes de gerenciamento ampliados: cadastro como Demanda e Oferta, edição para Comunicado e reabertura preservando categoria, curtida e comentários.
- Regressões aprovadas: grade de três colunas em 900 px (duas e uma em larguras menores, conforme comportamento existente), mídia quadrada, renderização de imagens fora da tela, validação de links, interações, gerenciamento de arquivos, falhas de gravação e estados dos controles de vídeo.

Testes executados em diretórios temporários isolados, sem abrir navegador, reproduzir vídeo ou interagir com Explorer. A primeira execução do novo teste de migração usou IDs coincidentes entre exemplos e cadastros e acionou corretamente o bloqueio existente; a fixture foi corrigida para usar IDs distintos e a suíte completa passou.

## Testes manuais pendentes

- Criar anúncios com cada categoria; tentar salvar sem categoria; editar, cancelar e reabrir o editor para conferir seleção atual.
- Conferir tamanho, legibilidade e posição dos selos nas três categorias, com imagens claras/escuras e vídeos, nas larguras usuais e em diferentes escalas DPI.
- Alternar os quatro filtros por mouse e teclado; editar categoria sob filtro e conferir que o cartão entra/sai da seleção; conferir rolagem e foco.
- Abrir links pela mídia e confirmar que o selo não bloqueia o clique.
- Reproduzir, pausar, retomar e reiniciar vídeos; trocar filtros durante reprodução e confirmar encerramento da mídia removida e pausa fora da área visível.
- Curtir e comentar em cada categoria, fechar/reabrir a aplicação e confirmar resultados na interface.

Fixação ao desktop e distribuição institucional não foram alteradas nesta etapa. Não houve validação visual interativa nesta execução.
