# Carreta Joy 3D (Unity)

A nova versão do jogo em **Unity 6** (pipeline URP), com gráficos melhores. Ela instala
separada da versão atual (pacote `com.familia.carretajoy.unity`), então dá para comparar as duas.

## Fase 1 (esta)
- Arthur (103 mil triângulos) e Afonso (150 mil), vindos dos modelos com esqueleto do rascunho,
  importados como **Humanoid**: qualquer animação Mixamo serve nos dois corpos sem deformar.
- Animações: parado, andar e correr (as próprias do Arthur), dança Gangnam, pulo, mortal, flip e pirueta.
- Cidade 5x5 com prédios coloridos (as janelas acendem à noite), árvores, postes, faixas e uma praça com
  pista de dança que pisca no ritmo.
- A carreta original, com neon, luzes coloridas de verdade no ritmo, faróis à noite e paradas de festa.
- Sombras suaves, bloom (brilho das luzes), tonemapping ACES, neblina e dia/noite.
- Controles de tela multitoque: joystick, PULAR, MORTAL, FLIP, PIRUETA, CORRER, SUBIR/DESCER, DANÇAR,
  TROCAR (Arthur ⇄ Afonso), DIA/NOITE e SOM. O outro menino acompanha e copia as acrobacias.
- Música gerada na hora (funk/pop), mais alta perto da carreta.

Todo o projeto é montado por código (`Assets/Scripts/Editor/Builder.cs`): pipeline, materiais, cena e
ajustes do Android. Os modelos ficam em `Assets/Resources`.

## Como o APK é gerado (GitHub Actions)
O workflow `.github/workflows/unity.yml` compila no Unity 6000.0.86f1 (imagem do GameCI) e publica um
**pré-lançamento** `unity-vX.Y.Z` com o APK `CarretaJoy3D-vX.Y.Z.apk`.

O Unity exige uma licença, mesmo a gratuita. **Configure uma vez:**

1. Crie uma conta grátis em <https://id.unity.com> (sem verificação em duas etapas, senão a ativação automática falha).
2. Num computador, instale o **Unity Hub**, entre com a conta e vá em
   *Preferências → Licenças → Adicionar → Obter uma licença Personal gratuita*.
3. Ache o arquivo da licença:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. No GitHub, abra o repositório → *Settings → Secrets and variables → Actions → New repository secret* e crie:
   - `UNITY_LICENSE`: o conteúdo inteiro do arquivo `.ulf` (abra no Bloco de Notas, copie e cole)
   - `UNITY_EMAIL`: o e-mail da conta Unity
   - `UNITY_PASSWORD`: a senha da conta Unity
5. Vá em *Actions → APK Unity → Run workflow*. A primeira vez demora uns 30–60 minutos.

## Abrir no seu computador (opcional)
Abra a pasta `unity` no Unity Hub com a versão **6000.0.86f1** e o módulo **Android Build Support**.
Depois use o menu **Carreta Joy → Preparar projeto** e aperte Play, ou **Carreta Joy → Gerar APK**.
No computador: WASD/setas andam, espaço pula, Q mortal, E flip, R pirueta, Shift corre,
F sobe/desce, G dança, Tab troca de menino, N dia/noite, e arrastar com o mouse gira a câmera.

## Próximas fases
2. Mundo aberto completo: as 5 cidades com subidas e descidas, carros, pessoas, parkour e dirigir a carreta.
3. Personagens adultos, fantasias, Afonso começando agachado, combo e dança no ritmo.
4. Menu inicial, tempo da volta (5/10/15/20 min), controle de videogame, atualizador e publicação no lugar da versão atual.
