## ConstructionDamage Plugin

A powerful Rust plugin that displays damage information when attacking buildings, doors, and other structures. Shows damage numbers, building grades, and destruction notifications in a clean, organized vertical list.

### Features

* **Damage Display**: Shows damage numbers for all attacks on structures
* **Building Grade**: Displays the grade of the structure being attacked (Wood, Stone, Sheet Metal, etc.)
* **Damage Types**: Shows the type of damage being dealt (Explosion, Bullet, Fire, etc.)
* **Vertical Stacking**: Damage numbers stack vertically in order, making them easy to read
* **Sound Effects**: Optional sound effects for hits and structure destruction
* **Smart Damage Combining**: Combines rapid-fire damage to prevent spam
* **Fire Damage Throttling**: Prevents excessive notifications from fire damage
* **Destruction Notifications**: Clear "DEST" notification when structures are destroyed

### Supported Structures

* Building Blocks (all grades)
* Doors (Wood, Sheet Metal, Armored)
* Storage Containers (Boxes, Repair Bench, etc.)
* Tool Cupboards
* Auto Turrets
* Furnaces
* SAM Sites
* Shotgun Traps
* Bear Traps
* Barricades

### Commands

* `/cdmg` or `/constructiondamage` - Toggle the damage display on/off

### Configuration

```json
{
  "Sound": {
    "EnableSoundEffect": true,
    "Sound Effect": "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab",
    "DestroyedSoundEffect": "assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab"
  },
  "Display": {
    "ShowBuildingGrade": true,
    "ShowDamageType": true
  },
  "DamageDisplayTimeout": 0.5,
  "ExplosiveDamageTimeout": 2.0,
  "RapidDamageTimeout": 0.2,
  "DestroyedTimeout": 3.0,
  "FireDamageTimeout": 1.0,
  "CombineInterval": 0.1
}
```

### Configuration Options

* `EnableSoundEffect` - Enable/disable hit sound effects
* `Sound Effect` - Sound effect played on structure hits
* `DestroyedSoundEffect` - Sound effect played when structures are destroyed
* `ShowBuildingGrade` - Show/hide building grade information
* `ShowDamageType` - Show/hide damage type information
* `DamageDisplayTimeout` - How long regular damage numbers stay on screen
* `ExplosiveDamageTimeout` - How long explosive damage numbers stay on screen
* `RapidDamageTimeout` - Timeout for combining rapid-fire damage
* `DestroyedTimeout` - How long destruction notifications stay on screen
* `FireDamageTimeout` - Minimum interval between fire damage notifications
* `CombineInterval` - Time window for combining rapid damage

### Display Colors

* Destroyed: Red
* Wood: Brown (#8b4513)
* Stone: Gray (#808080)
* Sheet Metal: Silver (#c0c0c0)
* Armored: Steel Blue (#4682b4)
* TopTier: Gold (#ffd700)
* Twigs: Bronze (#cd7f32)
* Default: White

### Installation

1. Download the plugin
2. Place it in your server's `carbon/plugins` directory
3. Configure the settings in the generated config file if needed
4. Restart your server or reload the plugin

### Usage Examples

* **Regular Damage**: `-50 [Stone] (Bullet)`
* **Explosive Damage**: `-250 [Sheet Metal] (Explosion)`
* **Rapid Fire**: `-150 [Wood] (Bullet x5)`
* **Fire Damage**: `-10 [Wood] (Fire)`
* **Destruction**: `DEST [Armored]`

### Notes

* Damage numbers stack vertically with the newest at the top
* Rapid-fire damage (like explosive ammo) is combined to prevent spam
* Fire damage is throttled to prevent excessive notifications
* Destruction notifications appear slightly to the left to avoid overlap
* All timeouts and display options are configurable



### Version History

* 1.0.5 - Added vertical stacking and improved cleanup
* 1.0.4 - Added support for more entity types and fixed sound effects
* 1.0.3 - Added fire damage handling and throttling
* 1.0.2 - Added building grade colors and destruction notifications
* 1.0.1 - Added damage type display and explosive timeouts
* 1.0.0 - Initial release
