# Ajustes visuais de Curtir e Configurações

O coração é um `Path` vetorial WPF de 20 × 20, com contorno #E53935. Um DataTrigger ligado a `LikedLocally` preenche o coração com a mesma cor quando curtido. O contador usa `LikeCount` e o evento `Like_Click` permanece igual. A engrenagem usa o glifo E713 da fonte de sistema Segoe MDL2 Assets, antes do texto Configurações, mantendo `Settings_Click`. Sem dependências adicionais ou alterações na lógica, nas colunas e na integração com o desktop.

## Validação automatizada

Comandos (saída isolada para evitar o executável padrão em uso):

```powershell
dotnet build MuralInstitucional.csproj -p:BaseOutputPath=artifacts/visual-buttons/
dotnet run --project Tests/MuralInstitucional.Checks.csproj -p:BaseOutputPath=artifacts/visual-buttons/
```

## Testes visuais pendentes

Resultado em 15/10/2026: build aprovado sem avisos ou erros; 132 verificações automatizadas aprovadas, incluindo curtidas, persistência e cooperação. A suíte não substitui a inspeção visual dos ícones.

- Conferir coração de 20 px: contorno vermelho sem curtida e preenchimento vermelho com curtida. Clicar duas vezes, verificar incremento/decremento do contador e persistência após reabrir.
- Conferir a engrenagem antes de Configurações, alinhamento, ausência de corte e abertura/fechamento do painel pelo botão.
- Conferir os dois ícones em escala de 100%, 150% e 200%, nas larguras de uma, duas e três colunas; verificar ausência de sobreposição dos contadores e demais botões.
- Conferir que “Tenho interesse em cooperar”, comentários e controles de vídeo continuam com aparência e comportamento anteriores.
