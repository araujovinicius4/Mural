# Validação executada — 13/10/2026

> **Histórico da versão anterior.** O usuário confirmou posteriormente que a janela desaparecia ao fixar, com o processo ainda ativo. Os resultados abaixo demonstram somente estrutura Win32; não validam a fixação visual. A revisão atual recusa essa topologia e não envia mensagens 0x052C ao Explorer. Consulte [DIAGNOSTICO_FIXACAO.md](DIAGNOSTICO_FIXACAO.md) e o [README atual](README.md) para o comportamento e os códigos de saída vigentes.

Ambiente: Windows build 26100 (24H2), EditionID Enterprise; SDK .NET 10.0.401, processo x64, sessão interativa 2. A chave de identificação do Windows foi apenas lida. O valor legado ProductName informou Windows 10 Enterprise; a identificação como Windows 11 é baseada no build 26100/24H2. Nenhuma configuração do Registro foi modificada.

## Resultados

| Verificação | Resultado observado |
| --- | --- |
| `dotnet build` | Sucesso, zero avisos e zero erros. |
| Posicionamento | Geometria solicitada para os 60% à direita da área útil confirmada por GetWindowRect. |
| Shell no ambiente isolado | Progman e GetShellWindow retornaram zero; fixação recusada e fechamento normal. |
| Sondagem na sessão real, mensagem inicial `(0,0)` | Progman e DefView encontrados, mas sem WorkerW visível adequado; retorno ao modo convencional. |
| Sondagem na sessão real com sequência final | WorkerW filho de Progman, visível e abaixo de DefView encontrado. Contextos DPI compatíveis, parentesco, estilos, ordem Z e geometria aprovados. |
| Verificação posterior | Passou após 1,5 segundo; verificação estrutural, sem inspeção dos pixels. |
| Desafixar | Parentesco, estilos e geometria restaurados e verificados. |
| Atalhos de recuperação | Ambos registrados na sessão real; disparo por teclado ainda requer teste manual. |
| Encerramento da sondagem | Ciclo final encerrou com código 0, sem processo de teste remanescente. |
| Falha simulada após SetParent | Restauração convencional verificada; sondagem encerrou com código 2, esperado para a falha intencional. |

Trechos do diagnóstico final:

```text
11:45:54 Child WorkerW=0x5066A; visible=True; abaixoDosIcones=True
11:45:54 Host candidato: WorkerW 0x5066A; parent=0x10132
11:45:54 Integração estrutural verificada (experimental).
11:45:56 PROBE verificação tardia: True
11:45:56 Modo convencional restaurado; mover, redimensionar e minimizar disponíveis.
11:45:56 PROBE RESULT pin=True; verified=True; unpin=True; Win+D=não testado
11:46:02 Fixação falhou: Falha simulada após SetParent para testar rollback.
11:46:02 Modo convencional restaurado; mover, redimensionar e minimizar disponíveis.
11:46:04 PROBE RESULT pin=False; verified=False; unpin=True; Win+D=não testado
```

Durante a implementação, a sondagem detectou uma verificação incorreta de SetParent(NULL). Com WS_CHILD ainda ativo, GetParent retornava o HWND do desktop; agora a rotina admite esse estado transitório, remove WS_CHILD e exige GetParent(NULL) após restaurar os estilos. O ciclo completo foi repetido com sucesso após a correção.

As sondagens reais foram executadas fora do desktop isolado da ferramenta para acessar o Explorer da sessão do usuário. Isso é uma permissão do ambiente de desenvolvimento; o aplicativo permanece `asInvoker`, sem solicitação de UAC ou mecanismos de elevação.

## Ainda não garantido

- Permanência visual após Win + D, apresentação dos pixels WPF e ausência de artefatos do compositor.
- Cobertura correta por outros aplicativos e reaparecimento após seu fechamento.
- Cliques/rolagem no modo fixado e acesso visual aos ícones, especialmente à direita.
- Disparo manual dos atalhos e fechamento pelo botão enquanto fixado.
- Comportamento em múltiplos monitores, DPI diferentes, desktops virtuais, RDP e outras builds/políticas corporativas.
- Recuperação quando o Explorer destrói o HWND filho antes de o monitor de integridade poder agir. Não há promessa de recuperação diante de encerramento/crash do shell.
- O caminho que cria uma nova janela quando a própria restauração Win32 falha foi implementado e observado durante o diagnóstico inicial; não foi forçado artificialmente na versão final.

**Conclusão de viabilidade:** a fixação estrutural em WorkerW foi demonstrada neste Windows Enterprise build 26100. O requisito completo de um mural visível após Win + D permanece experimental e depende do roteiro manual do README. A versão não está aprovada para implantação corporativa apenas com esses resultados.
