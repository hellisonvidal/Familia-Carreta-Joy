# Mapa de Ponta Grossa (dados reais)

Gera `unity/Assets/Resources/Map/` a partir do OpenStreetMap e do relevo de satélite.

1. `python3 fetch.py` (em `tiles/`): baixa o mapa da área urbana (14 x 14 km) da API do OpenStreetMap
2. `python dem_full.py`: baixa o relevo (Terrarium, AWS) → `dem_full.npy`
3. `python build_map.py`: ruas, prédios, casas geradas ao longo das ruas, árvores, percurso da carreta,
   pedaços de 200 m (`pg_map.bytes`), relevo (`pg_height.bytes`) e mapa de cores do chão (`pg_ground.png`)
4. `python readmap.py X Z RAIO saida.png`: confere o arquivo gerado (vista de cima)

Requer numpy, shapely, mapbox_earcut, pillow e matplotlib. Dados © colaboradores do OpenStreetMap (ODbL).
