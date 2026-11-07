# Espaço Livre

Aplicativo nativo para Windows 10/11, em português, feito com C# / WPF e .NET 10.

## Executar

1. Instale o **SDK .NET 10** para Windows: https://dotnet.microsoft.com/download/dotnet/10.0
2. Extraia o ZIP inteiro em uma pasta.
3. Clique duas vezes em `Iniciar.bat` ou abra `EspacoLivre.csproj` no Visual Studio com suporte a .NET 10 e desenvolvimento desktop.

## Gerar um executável independente

Execute `Publicar.bat`. O aplicativo ficará em `dist/EspacoLivre.exe`, junto com os demais arquivos necessários. Copie a pasta `dist` inteira. O destino padrão é Windows x64.

## O que funciona

- Indicador de capacidade e espaço livre da unidade.
- Escolha de pasta ou unidade, análise em segundo plano e cancelamento.
- Até 500 maiores pastas e 1.000 maiores arquivos, ordenados por tamanho.
- Duplo clique em uma pasta para analisá-la separadamente.
- Abrir a localização de um arquivo no Explorer.
- Envio individual à Lixeira, com confirmação do app e diálogo do Windows.
- Atalhos para Downloads, temporários do usuário e armazenamento do Windows.
- Bloqueio de exclusão em Windows, Program Files, ProgramData e arquivos marcados como sistema. Não há exclusão automática.

## Limites desta versão

A análise usa enumeração de diretórios, **não MFT**. A velocidade depende da quantidade de arquivos e pode ser lenta em unidades inteiras. Links e pontos de nova análise são ignorados para evitar ciclos e travessia para outras áreas; itens inacessíveis são ignorados pelo sistema. O contador de erros não inclui todos os itens ignorados silenciosamente. Tamanhos são lógicos (base 1024), podem contar hard links mais de uma vez e diferem do espaço físico usado por arquivos compactados, esparsos e arquivos de nuvem. Metadados, espaço reservado e arquivos sem acesso não entram na soma. Pastas incluem suas subpastas: não some as linhas.

Os resultados são um retrato do momento: arquivos podem mudar durante ou depois da análise. Revise arquivos em AppData, temporários e pastas de aplicativos antes de excluir. As proteções são preventivas, não garantem que um arquivo fora das áreas protegidas seja dispensável. O diálogo do Windows pode informar indisponibilidade da Lixeira: cancele se não desejar uma exclusão permanente. Não é necessário executar como administrador.

Interface inspirada no conceito apresentado, com listas reais no lugar do mapa ilustrativo. Não inclui desinstalador, identificação de duplicados, limpeza automática nem leitura de MFT.

Este projeto pode ser compilado no Linux com EnableWindowsTargeting; a interface e integração com Explorer/Lixeira precisam ser validadas no Windows.

## Verificação

Compilar: `dotnet build EspacoLivre.csproj -c Release`

Testar o analisador (também no Linux): `dotnet run --project tests/ScannerTests.csproj`
