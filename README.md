# Game-Test | Alpha Version 0.1

A Unity-based survival and resource management game with exploration, combat, and economic systems.

## Overview

Game-Test is a KMUTT Y4 Final Project featuring a dynamic world where players engage in resource gathering, cooking, combat, and trading. Navigate multiple zones, manage your inventory, and interact with NPCs in various environments.

## Features

### Core Systems
- **Resource Management**: Gather and manage various items including food, herbs, and tools
- **Cooking System**: Prepare dishes by combining raw materials (meat + herbs = seasoned meals)
- **Combat System**: Engage enemies with multiple weapon types (sword, axe, pickaxe)
- **Inventory System**: Organize and track collected items
- **Economy**: Buy and sell items at shops and restaurants

### Environments
- **Zone Village**: Central hub area with NPCs and shops
- **Zone1 & Zone2 (Cave)**: Exploration areas with resource gathering opportunities
- **Interior Locations**:
  - Player Home: Safe space for rest and inventory management
  - Restaurant: Dining and food trading
  - City: Urban exploration and commerce

### Player Systems
- Character movement and interaction
- Health and combat mechanics
- Inventory management with UI
- Day/night cycle considerations

## Project Structure

```
Assets/
├── Data/                  # Game content assets
│   ├── Enemy/            # Enemy configurations
│   ├── Environment/      # Environmental data
│   ├── FoodCooks/        # Cooking recipes
│   ├── FoodGathering/    # Gatherable resources
│   ├── FoodShops/        # Shop configurations
│   └── FoodSupplies/     # Supply data
├── Script/               # Game logic
│   ├── CombatWeapons/   # Weapon mechanics
│   ├── Core/            # Core game systems
│   ├── Enemy/           # Enemy AI and behavior
│   ├── Environment/     # Environmental interactions
│   ├── InventoryItems/  # Inventory logic
│   ├── Player/          # Player mechanics
│   ├── Systems/         # Game systems (UI, management)
│   └── UI/              # User interface
├── Scenes/              # Game scenes
│   ├── MainMenu.unity
│   ├── Zone Village.unity
│   ├── Zone1.unity
│   ├── Zone2 Cave.unity
│   ├── InsideHome.unity
│   ├── InsideRestaurantScene.unity
│   ├── CityScene.unity
│   └── SampleScene.unity
├── Animation/           # Character and object animations
├── Font/                # Custom fonts
├── ImportAssets/        # External imported assets
└── Animation/           # Visual effects

ProjectSettings/        # Unity project configuration
```

## Getting Started

### Prerequisites
- Unity 2022 LTS or newer
- Git for version control

### Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd "Alpha-Version"
```

2. Open the project in Unity:
   - Launch Unity Hub
   - Click "Open Project"
   - Navigate to the project folder and select it

3. Wait for Unity to import all assets (first time may take several minutes)

4. Open the MainMenu scene:
   - Navigate to `Assets/Scenes/MainMenu.unity`
   - Press Play to run the game

### Running the Game

**Play from MainMenu:**
- Open `Assets/Scenes/MainMenu.unity`
- Press the Play button (or Ctrl+P)

**Play from a specific zone:**
- Open any scene from `Assets/Scenes/`
- Press Play to start in that location

## Gameplay Guide

### Basic Controls
- **Movement**: WASD or Arrow keys
- **Interact**: E key (near NPCs/objects)
- **Inventory**: I key
- **Attack**: Left Mouse Button (when weapon equipped)
- **Pause/Menu**: ESC key

### Resource Gathering
1. Navigate to gathering zones (Zone1 or Zone2)
2. Find resource nodes
3. Use appropriate tools (pickaxe for stone, axe for wood, etc.)
4. Items appear in your inventory

### Cooking
1. Go to a cooking station
2. Select raw materials from inventory
3. Combine compatible items to create dishes
4. Cook combinations: Meat + Herb = Herb Grilled Meat

### Combat
1. Equip a weapon (sword, axe, or pickaxe)
2. Approach enemies
3. Left-click to attack
4. Manage health and stamina

### Trading
1. Visit shops (Zone Village, City)
2. Buy items with currency
3. Sell gathered resources for profit
4. Restaurants offer special food items

## Alpha Version Status

### Current Version (0.1)
- ✅ Core player movement and controls
- ✅ Basic resource gathering system
- ✅ Combat mechanics with multiple weapons
- ✅ Cooking and food preparation
- ✅ Multi-zone exploration
- ✅ NPC interactions and dialog
- ✅ Inventory and shop systems
- ✅ MainMenu and UI framework
- ⚠️ Balanced economy system (in progress)
- ⚠️ Advanced enemy AI (refinement ongoing)
- ⚠️ Quest system (basic implementation)

### Known Limitations
- Performance optimization ongoing
- Some animations may be placeholder
- Dialog system basic implementation
- Limited sound effects
- No multiplayer support in alpha

## Development

### Building

**Build for Windows:**
1. File > Build Settings
2. Select Platform: Windows
3. Click Build
4. Choose output folder

**Build for WebGL:**
1. File > Build Settings
2. Select Platform: WebGL
3. Configure WebGL template
4. Click Build

### Version Control
```bash
# Check status
git status

# Create a feature branch
git checkout -b feature/new-feature

# Commit changes
git commit -m "Description of changes"

# Push to repository
git push origin feature/new-feature
```

## Technical Details

### Engine & Tools
- **Engine**: Unity 2022 LTS
- **Language**: C#
- **UI Framework**: Unity UI (TextMesh Pro)
- **Camera**: Cinemachine 2.9.7

### Dependencies
- Cinemachine (Virtual Camera)
- TextMesh Pro (Text Rendering)
- InputSystem (Modern input handling)

## Credits

**Project**: KMUTT Y4 Final Project  
**Developer**: Chaithawat Saklang  
**Institution**: King Mongkut's University of Technology Thonburi

## License

This project is part of an academic assignment. For licensing details, contact the development team.

## Support & Feedback

For issues or feature requests:
- Check existing issues in the repository
- Create a new issue with detailed description
- Include reproduction steps for bugs
- Provide environment details (OS, Unity version)

## Roadmap

### Beta Version (Planned)
- Enhanced graphics and animations
- Expanded quest system
- Advanced enemy behaviors
- Sound and music implementation
- Performance optimizations
- Balance adjustments based on feedback

### Future Versions
- Multiplayer/Co-op features
- Story campaign
- Additional areas and NPCs
- Crafting system expansion
- Achievement system

---

**Last Updated**: May 2026  
**Version**: Alpha 0.1
