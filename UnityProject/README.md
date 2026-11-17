# Familia Carreta Joy - Unity Port

Unity port of the original Three.js/WebView Android game "Carreta Joy".

## Original Game
- Repository: https://github.com/hellisonvidal/Familia-Carreta-Joy
- Original: Android app with WebView running Three.js (WebGL)
- Features: Open world city, characters, truck driving, parkour, dance rhythm game, procedural music

## Unity Version
This is a complete port to Unity using C#, featuring:
- **Procedural City Generation** - 6x6 blocks with streets, buildings, parks, plazas
- **Character System** - 10 characters (2 kids with real heads, 8 adults with procedural bodies + Mixamo retargeting)
- **Truck System** - 9 variants with unique visuals and physics
- **Physics** - Walking, jumping, climbing, parkour, truck roof physics
- **Dance Rhythm Game** - 4-direction dance pads with timing judgment
- **Procedural Music** - Real-time synthesis (Funk, Samba, Sertanejo, Axé, Rock)
- **Day/Night Cycle** - Dynamic lighting, sky, fog
- **Gamepad Support** - PlayStation/Xbox/Generic controllers
- **Touch Controls** - Mobile joystick + camera drag
- **Multiple Cities** - São Paulo, Rio de Janeiro, Belo Horizonte, Salvador, Curitiba

## Project Structure
```
Assets/
├── Scripts/
│   ├── Core/           # GameState, GameManager, CameraController, InputManager, Bootstrap
│   ├── Characters/     # CharacterController, CharacterBody, CharacterDefinition, PoseDefinitions
│   ├── City/           # CityController (terrain, buildings, collisions, props)
│   ├── Truck/          # TruckController (driving, autopilot, lights, variants)
│   ├── Music/          # MusicController (procedural synthesis)
│   ├── Dance/          # DanceController (rhythm game)
│   ├── Gameplay/       # LifeController (pedestrians, cars, particles)
│   ├── UI/             # UIManager (menus, HUD, dialogs)
│   └── Editor/         # Editor tools
├── Scenes/
│   └── MainScene.unity
├── Prefabs/
├── Models/             # GLB models (imported from original)
├── Animations/         # Animation clips
├── Materials/
├── Resources/
└── StreamingAssets/
```

## Requirements
- Unity 2022.3 LTS or later
- Input System package
- TextMeshPro package
- UGUI package

## Setup
1. Open project in Unity 2022.3+
2. Install packages via Package Manager (Input System, TextMeshPro, UGUI)
3. Import GLB models from original `app/src/main/assets/www/models/` to `Assets/Models/`
4. Import animations from original `app/src/main/assets/www/anims/` to `Assets/Animations/`
5. Set up animation clips in CharacterBody/MixamoDriver
6. Open `Assets/Scenes/MainScene.unity`
7. Press Play

## Key Scripts

### GameManager
Central coordinator - handles game flow, save/load, scene transitions.

### CityController
Procedurally generates the entire city:
- Terrain with heightmap noise
- 6x6 grid of blocks with streets
- Buildings (towers, casarao, shops with climbable roofs)
- Plazas with dance floors
- Landmarks per city (MASP, Cristo Redentor, etc.)
- Traffic streets with AI cars
- Parkour park with containers, ramps
- Spatial collision grid

### CharacterController
Full character physics and animation:
- Walking, running, jumping (with coyote time)
- Double jump, wall jump
- Tricks: mortal, flip, estrela, pirueta
- Dash/esquiva, slide, dive
- Climbing buildings and truck
- Truck driving mode
- Dance moves
- AI for NPCs (follow player, party, escort)
- Mixamo animation retargeting

### TruckController
- Player driving (throttle, brake, steer)
- Autopilot (follows ring road, returns to route)
- Collision detection (4 spheres along length)
- 9 variants with unique meshes/effects
- Dynamic lights (bulbs, beams, lasers, disco, party)
- Night mode effects

### MusicController
Real-time procedural audio synthesis:
- 5 styles: Funk (130 BPM), Samba (140), Sertanejo (120), Axé (135), Rock (150)
- Drum synthesis (kick, snare, hihat)
- Bass, lead, pad synthesis
- No audio files - all generated at runtime

### DanceController
Rhythm game on truck roof / plazas:
- 4-direction pads (left, up, down, right)
- Timing windows: Perfect (0.08s), Good (0.15s), Ok (0.25s)
- Streak bonuses (every 8 = mortal together)
- Combo Dance mode (3x points)

## Porting Notes

### Coordinate System
- Original: Three.js (Y-up, right-handed)
- Unity: Y-up, left-handed
- Conversion: Z-axis flipped in some calculations

### Animation Retargeting
Original uses Mixamo retargeting via XBot skeleton. Unity version uses:
- Procedural body with joint hierarchy matching Mixamo bone names
- MixamoDriver samples animations and applies to procedural bones
- Shoulder rotation limits for skinned bodies

### City Generation
Direct port of the JavaScript algorithms:
- Perlin noise terrain with city-specific phases
- Block-based building placement
- Spatial hash grid for collisions
- Ring road for truck autopilot

### Input System
Unity's new Input System + custom touch/joystick:
- Floating joystick (spawns at touch position)
- Camera drag on right side
- Gamepad support with native Android passthrough
- Keyboard for testing

## Building
1. File > Build Settings
2. Add MainScene to scenes
3. Target platform: Android
4. Configure Player Settings:
   - Minimum API Level: 24 (Android 7.0)
   - Target API Level: 34
   - Scripting Backend: IL2CPP
   - Target Architectures: ARM64
5. Build

## Credits
Original game by Hellison Vidal
Unity port by Claude (Anthropic)

Models from Meshy (original)
Animations from Mixamo
Music synthesis inspired by the original procedural audio