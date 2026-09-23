# VR Target Challenge — Neon Pop

Projeto Unity de tiro em balões em realidade virtual, com pontuação, combo e rodadas de 45 segundos. Desenvolvido para testes com Meta Quest 3 via Link no Windows.

## Abrir o projeto

1. Instale o **Unity 6000.6.2f1** pelo Unity Hub.
2. Clone este repositório e adicione a pasta no Unity Hub.
3. Abra o projeto e aguarde a importação dos assets e dos pacotes.
4. Abra `Assets/Scenes/Fase.unity`.
5. Para jogar em VR no PC, conecte o Quest pelo aplicativo da Meta e use o runtime OpenXR da Meta.

Os pacotes necessários estão declarados em `Packages/manifest.json`. O projeto usa URP, OpenXR e XR Interaction Toolkit.

## Jogabilidade

- Pegue a arma e atire no balão central para iniciar a contagem regressiva.
- Estoure os balões para ganhar pontos; acertos consecutivos aumentam o bônus de combo.
- Os estouros reproduzem o som `balloon-pop` e exibem os pontos ganhos subindo no local do balão.

## Organização

- `Assets/`: cenas, scripts, recursos e configurações de XR.
- `Packages/`: dependências do projeto.
- `ProjectSettings/`: configurações do Unity.
- `Tools/FecharMetaVR/`: código-fonte do utilitário Windows para encerrar os processos Meta/OVR. Requer administrador; após usá-lo, reinicie o serviço Oculus VR Runtime Service para reconectar o Link.

As pastas `Library`, `Logs`, `Temp`, backups locais e builds não são versionadas. O Unity recria os arquivos de cache ao abrir o projeto.

## Licença

Consulte `LICENSE`. Assets de terceiros mantêm suas respectivas licenças; consulte os avisos e arquivos de licença distribuídos com eles.
