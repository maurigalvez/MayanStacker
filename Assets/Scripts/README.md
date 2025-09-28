# TamalStacker - 2D Stack Builder Game

## Game Overview
A 2D stack builder game where players tap to drop blocks and try to stack them as perfectly as possible. The game features physics-based stacking, scoring based on landing accuracy, and a balance system that causes the stack to fall when it becomes too unstable.

## Core Features
- **Tap-to-Drop Mechanics**: Tap anywhere on screen to drop the current block
- **Physics-Based Stacking**: Realistic physics for block interactions
- **Scoring System**: Points awarded based on how centered blocks land
- **Balance System**: Stack becomes unstable and falls when too unbalanced
- **High Score Tracking**: Persistent high score storage
- **Responsive UI**: Score display and game over screen

## Scripts Overview

### Core Game Systems

#### GameManager.cs
- **Purpose**: Central game state management
- **Features**: 
  - Score tracking and high score persistence
  - Game state management (active, over, restart)
  - Event system for game events
  - Singleton pattern for global access

#### StackableObject.cs
- **Purpose**: Individual block behavior and physics
- **Features**:
  - Physics setup (mass, drag, bounciness)
  - Landing accuracy calculation
  - Visual feedback based on landing quality
  - Balance checking for stack stability
  - Scoring integration

#### ObjectSpawner.cs
- **Purpose**: Block spawning and movement
- **Features**:
  - Spawns blocks at top of screen
  - Horizontal movement before dropping
  - Automatic new block spawning
  - Color variation for visual appeal

#### InputManager.cs
- **Purpose**: Input handling for all platforms
- **Features**:
  - Touch input (mobile)
  - Mouse input (PC)
  - Keyboard input (spacebar)
  - Cross-platform compatibility

### UI and Camera Systems

#### UIManager.cs
- **Purpose**: User interface management
- **Features**:
  - Real-time score display
  - High score tracking
  - Game over screen
  - Instructions display
  - Restart and quit functionality

#### CameraController.cs
- **Purpose**: Camera management and following
- **Features**:
  - Optimal camera positioning
  - Stack following as it grows
  - Smooth camera movement
  - Configurable bounds

#### Ground.cs
- **Purpose**: Ground platform setup
- **Features**:
  - Automatic ground creation
  - Screen-relative sizing
  - Static collider setup

#### GameController.cs
- **Purpose**: Main game coordinator
- **Features**:
  - Component initialization
  - Game flow management
  - Event coordination
  - Debug logging

## Setup Instructions

### 1. Scene Setup
1. Create a new 2D scene in Unity
2. Set up the camera with the following settings:
   - Projection: Orthographic
   - Size: 8
   - Position: (0, 0, -10)

### 2. Create Game Objects

#### Main Camera
1. Select the Main Camera
2. Add the `CameraController` script
3. Configure settings:
   - Orthographic Size: 8
   - Camera Offset: (0, 0, -10)
   - Enable "Follow Stack" for dynamic camera

#### Ground
1. Create an empty GameObject named "Ground"
2. Add the `Ground` script
3. The script will automatically set up the ground collider and visual

#### Game Manager
1. Create an empty GameObject named "GameManager"
2. Add the `GameManager` script
3. No additional configuration needed (uses singleton pattern)

#### Object Spawner
1. Create an empty GameObject named "ObjectSpawner"
2. Add the `ObjectSpawner` script
3. Configure settings:
   - Spawn Height: 8
   - Horizontal Movement Range: 3
   - Movement Speed: 2
   - Spawn Delay: 1

#### Input Manager
1. Create an empty GameObject named "InputManager"
2. Add the `InputManager` script
3. Configure input settings as needed

#### Game Controller
1. Create an empty GameObject named "GameController"
2. Add the `GameController` script
3. Enable "Auto Find Components" for automatic setup

### 3. UI Setup

#### Canvas Setup
1. Create a Canvas (UI > Canvas)
2. Set Canvas Scaler to "Scale With Screen Size"
3. Reference Resolution: 1920x1080

#### Game UI Elements
1. **Score Text**: Create TextMeshPro text showing current score
2. **High Score Text**: Create TextMeshPro text showing best score
3. **Instructions Text**: Create TextMeshPro text with game instructions

#### Game Over Panel
1. Create a Panel for the game over screen
2. Add the following UI elements:
   - Final Score Text
   - New High Score Text (initially hidden)
   - Restart Button
   - Quit Button

#### UIManager Setup
1. Create an empty GameObject named "UIManager"
2. Add the `UIManager` script
3. Assign all UI references in the inspector

### 4. Physics Settings
1. Go to Edit > Project Settings > Physics 2D
2. Set Gravity Y to -9.81
3. Ensure 2D Physics is enabled

### 5. Tags Setup
Create the following tags in Project Settings > Tags and Layers:
- "Stackable" (for stackable objects)
- "Ground" (for the ground platform)

### 6. Optional: Create Stackable Object Prefab
1. Create a GameObject with:
   - SpriteRenderer
   - BoxCollider2D
   - Rigidbody2D
   - StackableObject script
2. Set tag to "Stackable"
3. Save as prefab
4. Assign to ObjectSpawner's "Stackable Object Prefab" field

## Gameplay Features

### Scoring System
- **Perfect Landing** (90%+ accuracy): 100 points
- **Good Landing** (60-89% accuracy): 50 points
- **Poor Landing** (<60% accuracy): 10 points

### Balance System
- Stack becomes unstable when blocks are tilted more than 15 degrees
- Game ends when the stack falls
- Camera follows the stack as it grows

### Controls
- **Mobile**: Tap anywhere on screen
- **PC**: Click mouse or press spacebar

## Customization Options

### Visual Customization
- Modify colors in `StackableObject.cs`
- Adjust object sizes in `ObjectSpawner.cs`
- Change camera settings in `CameraController.cs`

### Gameplay Customization
- Adjust scoring values in `StackableObject.cs`
- Modify movement speed in `ObjectSpawner.cs`
- Change balance sensitivity in `StackableObject.cs`

### Physics Customization
- Adjust mass, drag, and bounciness in `StackableObject.cs`
- Modify gravity in Project Settings
- Change friction and physics materials

## Troubleshooting

### Common Issues
1. **Blocks not spawning**: Check ObjectSpawner configuration
2. **Input not working**: Verify InputManager is active and game is not over
3. **UI not updating**: Ensure UIManager has correct references
4. **Camera not following**: Check CameraController settings

### Performance Tips
- Limit the number of active stackable objects
- Use object pooling for better performance
- Optimize physics settings for your target platform

## Future Enhancements
- Power-ups and special blocks
- Different block shapes and sizes
- Multiplayer support
- Achievement system
- Sound effects and music
- Particle effects for perfect landings
