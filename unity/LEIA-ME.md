# Carreta Joy 3D (Unity)

A nova versão do jogo em **Unity 6** (pipeline URP), com gráficos melhores. Ela instala
separada da versão atual (pacote `com.familia.carretajoy.unity`), então dá para comparar as duas.

## Fase 1 (esta)
- Arthur (103 mil triângulos) e Afonso (150 mil), vindos dos modelos com esqueleto do rascunho,
  importados como **Humanoid**: qualquer animação Mixamo serve nos dois corpos sem deformar.
- Animações: parado, andar e correr (as próprias do Arthur), dança Gangnam, pulo, mortal, flip e pirueta.
- **Ponta Grossa real (v0.3.0):** ruas, relevo e bairros de verdade (OpenStreetMap + satélite), 231 mil construções,
  86 mil árvores (araucárias!), Catedral, Estação Saudade, Vila Hilda e Estádio Germano Krüger. A cidade carrega em
  pedaços de 200 m em volta do jogador. Ferramentas em `tools/pg`.
- A carreta original, com neon, luzes coloridas de verdade no ritmo, faróis à noite e paradas de festa.
- Sombras suaves, bloom (brilho das luzes), tonemapping ACES, neblina e dia/noite.
- **Turma completa (v0.2.0):** Arthur, Afonso, Chaves, Fofão, Chapolin, Máscara, Homem-Aranha, Capitão América
  e Hulk, todos jogáveis (botão TROCAR). Quem não está no controle segue o jogador, sobe na carreta, dança junto e
  repete as acrobacias.
- Controles de tela multitoque: joystick, PULAR, MORTAL, FLIP, PIRUETA, CORRER, SUBIR/DESCER, DANÇAR,
  TROCAR (passa para o próximo personagem), DIA/NOITE e SOM. O outro menino acompanha e copia as acrobacias.
- Música gerada na hora (funk/pop), mais alta perto da carreta.

- **v0.4.0:** menu inicial (Jogar, Personagem, Ponto de partida, Ajustes, Sair), pausa, ajustes salvos
  (música, gráficos Leve/Médio/Alto, 30/60 quadros, câmera, turma segue ou cada um parado, sensibilidade),
  dirigir a carreta (DIRIGIR perto da cabine; ao sair ela fica estacionada; "piloto auto" na pausa),
  minimapa (toque para ampliar), 4 câmeras, troca de personagem por lista, versão Windows com instalador.

- **v0.5.0:** câmera TPS padrão; carreta sempre no percurso (ao largar o volante ela volta sozinha);
  turma vai na carreta dançando; seletor com fotos; botões ANDAR/CORRER/MANOBRAS; controle de videogame
  (A pula, B sobe/desce, X mortal, Y flip, LB pirueta, RB anda/corre, direcional: cima dança, baixo dirige,
  esquerda câmera, direita troca; Start pausa; gatilhos aceleram/freiam). Corrigido: personagens gigantes e carreta em pé.

- **v0.6.0:** atualizador dentro do jogo (Android: PackageInstaller; Windows: baixa e abre o instalador);
  animações livres CC0 da Quaternius (`Assets/Resources/Animations/Livres`): rolar, deslizar, salto ninja,
  escalar mureta (parkour: PULAR de frente para um obstáculo de 0,5 a 2 m), 2ª dança, Afonso começa agachado.

- **v0.7.0:** rodas da carreta girando (e esterçando ao dirigir); carreta devagar com o comboio (espera quem ficou
  para trás; velocidade Devagar/Normal/Rápida em Ajustes); 4 carretas (original + Neon, Tropical e Disco, modeladas em
  `tools/carretas.py`); combos de acrobacias (MORTAL+FLIP = estrela, FLIP+MORTAL = reversão, PIRUETA+PIRUETA = giro no ar,
  ROLAR+MORTAL = mortal lateral, NINJA+FLIP = chute voador, DESLIZAR+PIRUETA = break, ROLAR+DESLIZAR = moonwalk);
  5 danças (dançando, MORTAL/FLIP/PIRUETA/NINJA trocam o estilo); pessoas na rua e sentadas na carreta
  (Quaternius CC0, `tools/pessoas.py`); trânsito com 13 carros (Kenney CC0); menus com fotos das carretas e
  miniaturas 3D dos pontos de partida (`tools/pg/miniaturas.py`); acrobacias e danças da CMU (`tools/cmuanims.py`).

Todo o projeto é montado por código (`Assets/Scripts/Editor/Builder.cs`): pipeline, materiais, cena e
ajustes do Android. Os modelos ficam em `Assets/Resources`.

## Como o APK é gerado (GitHub Actions)
O workflow `.github/workflows/unity.yml` compila no Unity 6000.3.28f1 (imagem do GameCI) e publica um
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
Abra a pasta `unity` no Unity Hub com a versão **6000.3.28f1** e o módulo **Android Build Support**.
Depois use o menu **Carreta Joy → Preparar projeto** e aperte Play, ou **Carreta Joy → Gerar APK**.
No computador: WASD/setas andam, espaço pula, Q mortal, E flip, R pirueta, Shift corre,
F sobe/desce, G dança, V dirige/sai, H buzina, C câmera, M mapa, Tab troca, N dia/noite, Esc pausa;
mouse arrasta para girar a câmera e a rodinha aproxima.

## Próximas fases
2. Mundo aberto completo: as 5 cidades com subidas e descidas, carros, pessoas, parkour e dirigir a carreta.
3. Fantasias, Afonso começando agachado, combo e dança no ritmo.
4. Menu inicial, tempo da volta (5/10/15/20 min), controle de videogame, atualizador e publicação no lugar da versão atual.
