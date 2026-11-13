# Carreta Joy 🚚🎶

Jogo 3D para Android da carreta da alegria, com os irmãos **Arthur** e **Afonso** e a turma fantasiada.

## O que tem na versão 0.2

- **Mundo aberto**: cidade inteira em quarteirões, com ladeiras (subidas e descidas), praças, parque de parkour, lojinhas com telhado escalável, carros, quiosques, contêineres e rampas.
- **Ande por onde quiser** com qualquer personagem: correr, pular, mortal, flip, estrela, escalar muros baixos e carros (parkour), voar nas rampas, subir pela lateral da carreta e dançar no teto.
- **Dirija a carreta** quando quiser (botão DIRIGIR perto da cabine): acelerar, ré/freio, volante no joystick e buzina. Ao sair, o piloto automático leva a carreta de volta para o percurso.
- **Volta pela cidade com tempo**: 5, 10 (padrão), 15 ou 20 minutos. A carreta ajusta a velocidade para completar a volta no tempo escolhido.
  - Terminou com um **personagem**: todo mundo volta para a Praça Inicial.
  - Terminou **dirigindo**: o jogo acaba onde a carreta estiver, mesmo sem completar a volta.
- **Espaços de dança reservados**: cada praça (e a Praça Inicial) tem uma pista de dança colorida. Quando a carreta para, **todos os integrantes se reúnem na pista** e dançam juntos no ritmo (jogo de ritmo). No fim do jogo, a festa final é na pista da Praça Inicial.
- **Saída com acrobacias**: a cada saída da carreta (na largada e depois de cada dança), **5 integrantes descem e acompanham a carreta pela rua** fazendo mortal, flip, estrela e pirueta.
- **Personagens**: Arthur e Afonso com o **corpo real** dos modelos (esqueleto automático: correm, pulam, dançam com a roupa de verdade) + 8 adultos fantasiados com corpo humano, em versão genérica: Moleque do Barril, Bochechudo, Herói Vermelho, Cara Verde, Herói Teia, Capitão Escudo, Gigante Verde e Garras.
- **Fantasias para os meninos**: os dois podem trocar a roupa por qualquer uma das 8 fantasias (no menu ou durante o jogo).
- **Afonso** começa na pose característica (agachado, braço pra cima) e, ao mexer o joystick, levanta e sai correndo.
- **6 carretas**: Arco-Íris (original), Furacão Dourada, Neon Azul, Rosa Choque, Verde-Amarela e Trio Elétrico.
- **5 cidades**: São Paulo, Rio de Janeiro, Belo Horizonte, Salvador e Curitiba, de dia ou de noite, cada uma com sua música original.
- **Imagem mais clara**: ajustes de Brilho, Contraste e Cores no menu ⚙️.

## Controles

- 🕹️ Joystick (esquerda): anda/corre · arraste a tela para girar a câmera
- **PULAR**, e no ar **MORTAL**, **FLIP**, **ESTRELA** (termine antes de cair; várias no mesmo pulo valem mais)
- **SUBIR / DESCER**: escala a lateral da carreta / pula do teto
- **DIRIGIR / SAIR**: assume ou larga o volante · ▲ acelera · ▼ ré/freio · 📯 buzina · 💃 FESTA quando parar numa praça
- 🧒 (canto superior esquerdo): trocar de personagem e de fantasia

No computador (para testar): WASD/setas, espaço, Z = mortal, X = estrela, C = flip, E = subir/descer, F = dirigir, H = buzina, Q = personagens.

## Instalar

Baixe o APK na página de **Releases** (versões) do repositório, abra no celular e permita
"instalar apps de fontes desconhecidas". Android 7.0 ou mais novo.

Para publicar uma versão nova: aumente `versionCode` e `versionName` em `app/build.gradle.kts`
e envie para o `main`. O GitHub Actions compila e publica a versão `v<versionName>` com o APK.

## Compilar

```bash
./gradlew assembleDebug   # APK em app/build/outputs/apk/debug/app-debug.apk
```

Requer JDK 17+ e Android SDK (compileSdk 35).

## Como é feito

- App Android nativo (Kotlin) com uma `WebView` rodando o jogo 3D em **three.js** (WebGL), tudo offline em `app/src/main/assets/www/`.
- Os modelos do Meshy não têm esqueleto. O jogo cria um **esqueleto automático** (`js/skinned.js`) para os modelos em pé do Arthur e do Afonso (Afonso2), com os pesos da pele calculados por região do corpo.
- Nas fantasias, os meninos usam a **cabeça real** (recortada com `tools/extrair-cabeca.mjs`) num corpo articulado com a fantasia.
- A carreta é o modelo do Meshy; as variações recolorem a pintura e acrescentam enfeites.
- Músicas sintetizadas em tempo real (sem arquivos de áudio).

Para testar no computador: `cd app/src/main/assets/www && python3 -m http.server` e abra `http://localhost:8000`.
