# Validação de importação e exportação de anúncios

## Executar a versão atualizada

Requer Windows e .NET 10 com WPF. Feche a versão antiga do Mural antes de abrir a versão atualizada.

```powershell
dotnet build MuralInstitucional.csproj -c Release
dotnet run --project Tests/MuralInstitucional.Checks.csproj -c Release
dotnet run --project MuralInstitucional.csproj -c Release --no-build
```

Os testes usam diretórios temporários isolados. Não modificam o cadastro real, não abrem o Explorer ou navegador e não reproduzem vídeo. Instanciar a prévia nos testes valida o XAML, mas não substitui a verificação visual.

## Teste entre dois computadores

1. No computador A, cadastre um anúncio com imagem PNG/JPG, título, descrição, URL HTTP/HTTPS e distribuição específica. Registre curtidas, comentário e interesse/cooperação local para verificar que ficam apenas em A.
2. Abra o menu ⋯ do cartão e escolha **Exportar anúncio (ZIP)**. Salve em uma pasta de teste. Confira a mensagem de sucesso. Cancelar o diálogo deve apenas informar o cancelamento.
3. Opcionalmente abra o ZIP no gerenciador de arquivos: deve conter somente `manifest.json`, `announcement.json` e `media.png`, `media.jpg` ou `media.jpeg`. Não deve haver caminhos do computador A, curtidas, comentários, solicitações, interesses, consentimentos, históricos ou justificativas nos JSONs.
4. Transfira manualmente o ZIP para B, por exemplo em pendrive. Não é necessário servidor ou conexão entre aplicativos.
5. No computador B, clique **Importar anúncio (ZIP)** junto de **+ Novo anúncio**. Confira título, descrição, tipo, publicação original, URL, imagem e distribuição na prévia. Cancele primeiro: nenhum cartão deve ser criado.
6. Abra novamente o ZIP e clique **Confirmar importação**. O cartão deve aparecer imediatamente, inclusive se havia um filtro de tipo ativo; a importação seleciona Todos. A origem deve aparecer como **RECEBIDO VIA ZIP • ORIGEM NÃO AUTENTICADA**.
7. Confira que o cartão não recebeu curtidas, comentários ou cooperações de A. Em Configurações, confira os caminhos do cadastro e mídia. O novo registro tem `Id` local diferente e `ExchangeOriginId` separado. Nenhuma interação existente em B deve mudar.
8. Feche e abra B. O anúncio e a mídia devem continuar disponíveis. Mova ou remova apenas o ZIP transferido: isso não deve afetar a mídia importada, que foi copiada para a pasta gerenciada.
9. Importe novamente o mesmo ZIP. A prévia deve avisar a duplicidade e oferecer **Criar cópia independente** ou Cancelar. Cancelar mantém o cadastro. Confirmar cria outro cartão com novo ID local, preservando o identificador de origem; nenhum cartão ou cooperação existente é sobrescrito.
10. Reexporte o cartão recebido em B e abra a prévia em A. O identificador de origem deve permitir reconhecer o anúncio original.
11. Repita com AVI, MP4 ou WMV. O vídeo não deve tocar ao abrir a prévia ou importar. Clique **Reproduzir prévia do vídeo** para conferir a mídia; depois confirme e teste Reproduzir, Pausar e Reiniciar no cartão. A disponibilidade dos codecs depende do Windows.
12. Exporte um exemplo demonstrativo com pôster gerado. Como esse pôster não é um arquivo físico, o ZIP terá apenas os JSONs; a prévia informa ausência de mídia. A importação deve preservar texto e dados públicos.
13. Confira larguras de janela próximas de 1000, 600 e 320 pixels: controles de gerenciamento devem quebrar linha e cartões devem manter respectivamente três, duas e uma coluna, com mídia quadrada. Confira a prévia com texto longo, rolagem e botões visíveis.

## Falhas e preservação

Em um perfil de teste, tente importar um arquivo que não é ZIP, um ZIP truncado, versão desconhecida, arquivos extras ou caminhos como `../arquivo`. Deve aparecer erro compreensível antes de qualquer cadastro. Confira que cartões, cooperações, JSON e mídias anteriores continuam intactos.

Teste também cancelamento pelo botão Cancelar e pelo X da prévia. Os temporários devem ser removidos. Uma mídia associada que desapareceu antes da exportação deve produzir erro, preservando um ZIP anterior no destino.

Para testar concorrência, abra duas instâncias no mesmo perfil; salve um anúncio na primeira e tente confirmar uma importação na segunda. A segunda deve recusar a gravação por dados desatualizados, sem substituir o JSON nem manter a nova cópia de mídia. Reabra para recarregar o cadastro.

## Formato ZIP versão 1

São aceitas duas ou três entradas na raiz, com nomes exatos e sem diretórios:

- `manifest.json`: `Version` (1), `OriginId` (GUID não vazio) e `ExportedAt` (data/hora com offset).
- `announcement.json`: `Title`, `Description` (string ou null), `Type` (`Demanda`, `Oferta`, `Comunicado`), `ExternalUrl` (HTTP/HTTPS sem credenciais ou null), `PublishedAt` (data/hora original com offset), `Distribution` (`Kind` e `Criteria`), `MediaKind` (0 = imagem, 1 = vídeo), `Media` (nome permitido ou null).
- Mídia opcional: `media.png`, `media.jpg`, `media.jpeg`, `media.avi`, `media.mp4` ou `media.wmv`, coerente com `MediaKind`.

Todos os parâmetros dos JSONs são obrigatórios, inclusive os campos que permitem null. Não se aceitam propriedades duplicadas ou desconhecidas. Distribuição segue as regras atuais: General sem critérios, Unit com um critério, Sectors e TopicsOrSkills com um ou mais, até 30 valores distintos de até 100 caracteres. A distribuição descreve critérios; importar não envia conteúdo a setores ou computadores.

Anúncios locais usam seu GUID como origem na primeira exportação; anúncios recebidos preservam `ExchangeOriginId` ao reexportar. Cada importação confirmada gera outro `Id` local. O modelo local acrescenta somente `ExchangeOriginId` opcional e `IsImported`, com valores padrão compatíveis com JSON antigo; a versão do repositório permanece 1. Edições preservam essa identificação e as interações locais. A identidade declarada no ZIP serve para detectar duplicidades e não prova autoria, autenticidade ou permissão.

## Limites e proteções

- ZIP comprimido e soma descomprimida: até 250 MiB + 128 KiB; diretório central até 4 KiB; duas ou três entradas.
- Cada JSON: até 64 KiB; profundidade até 16.
- Imagem: PNG/JPG/JPEG, até 20 MiB e 40 megapixels. Vídeo: AVI/MP4/WMV, até 250 MiB. Assinaturas e decodificação de imagem seguem o serviço existente; codecs de vídeo são verificados pelo player do Windows ao reproduzir.
- Razão máxima de descompressão de 200:1 para entradas acima de 64 KiB. A exportação armazena a mídia sem compressão adicional para evitar falsos positivos com mídias muito compressíveis.
- Rejeição de ZIP64, multivolume, caminhos absolutos ou relativos, separadores, arquivos extras, nomes duplicados, links simbólicos, entradas especiais Unix e atributos de redirecionamento/diretório.
- Validação de tamanho real e CRC32 de cada entrada. CRC detecta corrupção; não é assinatura digital.
- Não há extração genérica: a mídia é escrita com nome gerado em diretório temporário exclusivo. Nenhum caminho fornecido pelo pacote é usado para extração ou gravação no cadastro.
- Após confirmação, a mídia é copiada pelo serviço gerenciado e o JSON é salvo pelo repositório existente com lock, fingerprint e substituição atômica. Falhas de persistência removem apenas a nova mídia. O limite existente de 100 anúncios e 5 MB de JSON continua válido.
- Exportação escreve primeiro um temporário, revalida o pacote completo e só então substitui o ZIP de destino escolhido no diálogo. Destinos devem ter extensão `.zip`.

## Limitações conhecidas

Sem sincronização, atualização automática, autenticação ou envio pela rede. Copiar um anúncio repetido mantém o mesmo identificador de origem, mas cria cadastro e interações locais independentes. Não há assinatura ou verificação de identidade do remetente.

Pôsteres demonstrativos gerados em memória não são incorporados. Mídia física associada ausente ou inválida bloqueia a exportação. O editor atual exige selecionar mídia para salvar; ao editar um anúncio importado sem mídia, será necessário escolher uma mídia válida.

Pacotes são processados no fluxo da interface; vídeos próximos do limite podem provocar espera durante validação/cópia. Vídeos dependem dos codecs locais; a validação de assinatura não garante reprodução de todos os arquivos. Uma interrupção forçada do processo pode deixar temporários na pasta temporária do Windows; os caminhos normais de sucesso, cancelamento e falha fazem limpeza.

## Arquivos envolvidos

Criados: `Services/AnnouncementExchangeService.cs`, `AnnouncementImportWindow.xaml`, `AnnouncementImportWindow.xaml.cs`, `Tests/ExchangeChecks.cs` e este documento.

Modificados: `Models/Announcement.cs`, `Services/AnnouncementManagementService.cs`, `ViewModels/BoardViewModel.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Controls/AnnouncementCard.xaml`, `Controls/AnnouncementCard.xaml.cs` e `Tests/Program.cs`.

Nenhum commit ou push faz parte desta entrega.

## Resultado automatizado desta entrega

- `dotnet build MuralInstitucional.csproj -c Release --no-restore`: aprovado, zero avisos e zero erros.
- `dotnet run --project Tests/MuralInstitucional.Checks.csproj -c Release`: aprovado, 312 verificações, incluindo a suíte anterior e os novos testes de intercâmbio.
- Cobertura nova: imagem/vídeo/sem mídia, dados públicos/distribuição/data/URL, exclusão de interações, IDs e reexportação, duplicidade/cópia, prévia sem gravação, cancelamento/temporários, corrupção/CRC, versões, propriedades inesperadas/duplicadas, caminhos, links, ZIP bombs, limites de JSON/ZIP/imagem/cadastro, falha de commit, concorrência, preservação byte a byte do JSON/mídias, persistência de origem e edição, compatibilidade com JSON antigo.
- O build Debug encontrou o executável em uso por outra instância (MSB3027/MSB3021). A aplicação aberta foi preservada; a validação foi concluída em Release.
- Validação visual entre máquinas e reprodução real de vídeo: pendentes, conforme o roteiro acima.
