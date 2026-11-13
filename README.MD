# Carreta Joy 🚚🎶

Jogo 3D para Android da carreta da alegria, com os irmãos **Arthur** e **Afonso**.
Eles sobem e descem pela lateral da carreta, fazem manobras na rua (mortal, flip, estrela),
parkour por cima de carros e bancos, voam em rampas e dançam juntos no teto da carreta.

## Piloto: 5 cidades

| Cidade | Paisagem | Relevo | Música |
|---|---|---|---|
| São Paulo (SP) | arranha-céus, museu vermelho suspenso, antenas | médio | Funk da Carreta |
| Rio de Janeiro (RJ) | orla, praia, calçadão de ondas, morros | plano | Samba da Orla |
| Belo Horizonte (MG) | serras, igrejinha de abóbadas | ladeiras fortes | Forró das Montanhas |
| Salvador (BA) | casarões coloridos, elevador, igreja | ladeiras | Axé do Pelourinho |
| Curitiba (PR) | araucárias, estufa de vidro, estação-tubo | suave | Eletrô Curitibano |

Todas as ruas são de asfalto, com retas, subidas, descidas e curvas. Cada cidade pode ser jogada de dia ou de noite.
À noite a carreta acende tudo: lâmpadas no ritmo da música, canhões de luz, lasers, globo e faróis.
As músicas são composições originais, sintetizadas em tempo real (sem arquivos de áudio).

## Como jogar

- **⬅ ➡** trocam de faixa (ou deslize o dedo na tela)
- **PULAR** pula (ou deslize pra cima)
- **MORTAL**, **FLIP**, **ESTRELA** no ar: termine a manobra antes de cair! Várias na mesma pulada valem mais
- **ESTRELA** no chão: rodinha que muda de faixa
- **DESCER / SUBIR**: pula da carreta pra rua ou escala a lateral
- **TROCAR**: alterna entre Arthur e Afonso (o outro é controlado pelo jogo e ajuda nos pontos)
- Na **praça**, a carreta para e começa a dança: toque nos botões coloridos no ritmo da música

## Instalar

Baixe o APK na página de **Releases** (versões) do repositório, abra no celular
e permita "instalar apps de fontes desconhecidas".

Para publicar uma versão nova: aumente `versionCode`/`versionName` em `app/build.gradle.kts`
e envie uma tag (`git tag v0.2.0 && git push origin v0.2.0`). O GitHub Actions compila e publica o APK.

## Compilar

```bash
./gradlew assembleDebug
# APK em app/build/outputs/apk/debug/app-debug.apk
```

Requer JDK 17+ e Android SDK (compileSdk 35). Android mínimo: 7.0.

## Como é feito

- App Android nativo (Kotlin) com uma `WebView` que roda o jogo 3D feito em **three.js** (WebGL).
- O jogo fica em `app/src/main/assets/www/` e funciona offline.
- Modelos 3D (Meshy AI) convertidos de FBX para GLB, com malha simplificada e texturas WebP
  (de ~310 MB para ~3,8 MB).
- Os modelos não têm esqueleto, então as animações (correr, manobras, escalar, dançar) são procedurais.
  Com modelos *rigados* (ex.: Meshy "Rig & Animate" ou Mixamo) dá pra ter braços e pernas animados.

Para testar no computador: `cd app/src/main/assets/www && python3 -m http.server` e abra `http://localhost:8000`
(teclado: setas, espaço, Z = mortal, X = estrela, C = flip, E = subir/descer, Q = trocar).
