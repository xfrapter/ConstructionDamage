using Facepunch;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("ConstructionDamage", "BlackWolf", "1.0.5")]
    class ConstructionDamage : RustPlugin
    {
        #region CONFIGURATION
        private bool Changed;
        private bool enablesound;
        private string? soundeffect;
        private string? destroyedSoundEffect;
        private float damageTimeout;
        private float explosiveDamageTimeout;
        private float rapidDamageTimeout;
        private float destroyedTimeout;
        private bool showBuildingGrade;
        private bool showDamageType;
        private float combineInterval;
        private float fireDamageTimeout;
        
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }        

        protected override void LoadDefaultConfig()
        {
            enablesound = Convert.ToBoolean(GetConfig("Sound", "EnableSoundEffect", true), System.Globalization.CultureInfo.InvariantCulture);
            soundeffect = Convert.ToString(GetConfig("Sound", "Sound Effect", "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab"), System.Globalization.CultureInfo.InvariantCulture);
            destroyedSoundEffect = Convert.ToString(GetConfig("Sound", "DestroyedSoundEffect", "assets/prefabs/npc/patrol helicopter/effects/rocket_explosion.prefab"), System.Globalization.CultureInfo.InvariantCulture);
            showBuildingGrade = Convert.ToBoolean(GetConfig("Display", "ShowBuildingGrade", true), System.Globalization.CultureInfo.InvariantCulture);
            showDamageType = Convert.ToBoolean(GetConfig("Display", "ShowDamageType", true), System.Globalization.CultureInfo.InvariantCulture);
            GetVariable(Config, "DamageDisplayTimeout", out damageTimeout, 0.5f);
            GetVariable(Config, "ExplosiveDamageTimeout", out explosiveDamageTimeout, 2.0f);
            GetVariable(Config, "RapidDamageTimeout", out rapidDamageTimeout, 0.2f);
            GetVariable(Config, "DestroyedTimeout", out destroyedTimeout, 3.0f);
            GetVariable(Config, "FireDamageTimeout", out fireDamageTimeout, 1.0f);
            GetVariable(Config, "CombineInterval", out combineInterval, 0.1f);
            SaveConfig();
        }

        public static void GetVariable<T>(DynamicConfigFile config, string name, out T value, T defaultValue)
        {
            config[name] = value = config[name] == null ? defaultValue : (T)Convert.ChangeType(config[name], typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }
        #endregion
        
        #region FIELDS
        Random rnd = new Random();
        List<BasePlayer> damagedisplayon = new List<BasePlayer>();
        Dictionary<BasePlayer, List<KeyValuePair<float, DamageInfo>>> damageHistory = new Dictionary<BasePlayer, List<KeyValuePair<float, DamageInfo>>>();
        Dictionary<BasePlayer, Dictionary<string, CombinedDamage>> combinedDamage = new Dictionary<BasePlayer, Dictionary<string, CombinedDamage>>();
        Dictionary<ulong, Dictionary<string, float>> lastFireDamageTime = new Dictionary<ulong, Dictionary<string, float>>();

        class DamageInfo
        {
            public int damage;
            public string? buildingGrade;
            public string? damageType;
            public double xs;
            public double ys;
            public double xe;
            public double ye;
            public int num;
            public bool isDestroyed;
            public float createdTime;
        }

        class CombinedDamage
        {
            public int totalDamage;
            public int hitCount;
            public float lastHitTime;
            public string? buildingGrade;
            public string? damageType;
            public Timer? displayTimer;
        }
        
        Dictionary<BasePlayer, Oxide.Plugins.Timer> destTimers = new Dictionary<BasePlayer, Oxide.Plugins.Timer>();
        #endregion

        #region COMMANDS
        [ChatCommand("constructiondamage")]
        [ChatCommand("cdmg")]
        void cmdConstructionDamage(BasePlayer player)
        {
            if (!damagedisplayon.Contains(player))
            {
                damagedisplayon.Add(player);
                SendReply(player, "<color=cyan>ConstructionDamage</color>: <color=orange>Construction damage display enabled.</color>");
            }
            else
            {
                damagedisplayon.Remove(player);
                SendReply(player, "<color=cyan>ConstructionDamage</color>: <color=orange>Construction damage display disabled.</color>");
            }
        }
        #endregion

        #region OXIDE HOOKS
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                damagedisplayon.Remove(player);
                damageHistory.Remove(player);
                if (combinedDamage.ContainsKey(player))
                {
                    foreach (var dmg in combinedDamage[player].Values)
                    {
                        dmg.displayTimer?.Destroy();
                    }
                }
                combinedDamage.Remove(player);
            }
            lastFireDamageTime.Clear();
        }

        void OnServerInitialized()
        {            
            LoadDefaultConfig();
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                damagedisplayon.Add(current);
            }            
            timer.Every(0.1f, OnDamageTimer);
        }        

        void OnPlayerConnected(BasePlayer player)
        {
            damagedisplayon.Add(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            
            damagedisplayon.Remove(player);
            damageHistory.Remove(player);
            if (combinedDamage.ContainsKey(player))
            {
                foreach (var dmg in combinedDamage[player].Values)
                {
                    dmg.displayTimer?.Destroy();
                }
            }
            combinedDamage.Remove(player);
            lastFireDamageTime.Remove(player.userID);
        }

        string DamageGUI = "[{\"name\":\"constructiondamage{0}\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{1}\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 1\",\"distance\":\"0.3 -0.3\"},{\"type\":\"RectTransform\",\"anchormin\":\"{2} {3}\",\"anchormax\":\"{4} {5}\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]";
        
        static string HandleArgs(string json, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var value = args[i];
                var strValue = value is IFormattable formattable 
                    ? formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                    : value.ToString();
                json = json.Replace("{" + i + "}", strValue);
            }
            return json;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || !damagedisplayon.Contains(attacker)) return;

            if (!IsValidEntity(entity)) return;

            var buildingGrade = GetBuildingGrade(entity);
            var damageType = hitInfo.damageTypes.GetMajorityDamageType().ToString();
            var damage = System.Convert.ToInt32(Math.Round(hitInfo.damageTypes.Total(), 0, MidpointRounding.AwayFromZero));

            if (enablesound && destroyedSoundEffect != null)
            {
                try
                {
                    var effect = GameManager.server.FindPrefab(destroyedSoundEffect);
                    if (effect != null)
                    {
                        Effect.server.Run(destroyedSoundEffect, attacker.transform.position, Vector3.zero, attacker.net.connection);
                    }
                }
                catch
                {
                    // Silently fail if effect is invalid
                }
            }

            DamageNotifier(attacker, damage, buildingGrade, "DESTROYED", destroyedTimeout, true);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;
            
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker == null || !damagedisplayon.Contains(attacker)) return;

            if (!IsValidEntity(entity)) return;

            var buildingGrade = GetBuildingGrade(entity);
            var damageType = GetDamageType(hitInfo);
            var damage = System.Convert.ToInt32(Math.Round(hitInfo.damageTypes.Total(), 0, MidpointRounding.AwayFromZero));

            // Skip rapid fire damage if it's too soon
            if (damageType == "Fire")
            {
                if (!lastFireDamageTime.ContainsKey(attacker.userID))
                {
                    lastFireDamageTime[attacker.userID] = new Dictionary<string, float>();
                }

                var key = $"{entity.net.ID}_{buildingGrade}";
                if (lastFireDamageTime[attacker.userID].ContainsKey(key) && 
                    Time.time - lastFireDamageTime[attacker.userID][key] < fireDamageTimeout)
                {
                    return;
                }
                lastFireDamageTime[attacker.userID][key] = Time.time;
            }

            if (enablesound && soundeffect != null)
            {
                try
                {
                    var effect = GameManager.server.FindPrefab(soundeffect);
                    if (effect != null)
                    {
                        Effect.server.Run(soundeffect, attacker.transform.position, Vector3.zero, attacker.net.connection);
                    }
                }
                catch
                {
                    // Silently fail if effect is invalid
                }
            }

            bool isExplosive = damageType == "Explosion";
            bool isRapidDamage = damageType == "Bullet";
            bool isFireDamage = damageType == "Fire";

            if (isRapidDamage)
            {
                HandleRapidDamage(attacker, damage, buildingGrade, damageType);
            }
            else
            {
                float timeout = isExplosive ? explosiveDamageTimeout : (isFireDamage ? fireDamageTimeout : damageTimeout);
                NextTick(() => DamageNotifier(attacker, damage, buildingGrade, damageType, timeout));
            }
        }

        private static bool IsValidEntity(BaseCombatEntity entity)
        {
            return entity is BuildingBlock || 
                   entity is Door || 
                   entity is StorageContainer || 
                   entity is BuildingPrivlidge ||
                   entity is AutoTurret ||
                   entity is BaseOven ||
                   entity is SamSite ||
                   entity is GunTrap ||
                   entity is BearTrap ||
                   entity is Barricade ||
                   entity is SimpleBuildingBlock;
        }

        private static string GetDamageType(HitInfo hitInfo)
        {
            var majorityType = hitInfo.damageTypes.GetMajorityDamageType();
            
            if (majorityType == DamageType.Heat)
                return "Fire";

            if (hitInfo.WeaponPrefab != null)
            {
                var weaponName = hitInfo.WeaponPrefab.ShortPrefabName;
                if (weaponName.Contains("molotov") || weaponName.Contains("incendiary"))
                    return "Fire";
            }

            return majorityType.ToString();
        }

        private static string GetBuildingGrade(BaseCombatEntity entity)
        {
            if (entity is BuildingBlock buildingBlock)
            {
                return buildingBlock.grade.ToString();
            }
            
            if (entity is Door door)
            {
                if (door.ShortPrefabName.Contains("wood"))
                    return "Wood";
                if (door.ShortPrefabName.Contains("metal"))
                    return "Sheet Metal";
                if (door.ShortPrefabName.Contains("armored") || door.ShortPrefabName.Contains("garage"))
                    return "Armored";
            }

            // Handle other entity types
            if (entity is StorageContainer)
                return GetStorageType(entity as StorageContainer);
            if (entity is BuildingPrivlidge)
                return "Tool Cupboard";
            if (entity is AutoTurret)
                return "Auto Turret";
            if (entity is BaseOven)
                return "Furnace";
            if (entity is SamSite)
                return "SAM Site";
            if (entity is GunTrap)
                return "Shotgun Trap";
            if (entity is BearTrap)
                return "Bear Trap";
            if (entity is Barricade)
                return "Barricade";
            
            return "Default";
        }

        private static string GetStorageType(StorageContainer? container)
        {
            if (container == null) return "Storage";

            switch (container.ShortPrefabName)
            {
                case var name when name.Contains("box.wooden"):
                    return "Wooden Box";
                case var name when name.Contains("box.repair"):
                    return "Repair Bench";
                case var name when name.Contains("coffin"):
                    return "Coffin";
                case var name when name.Contains("fridge"):
                    return "Fridge";
                case var name when name.Contains("locker"):
                    return "Locker";
                case var name when name.Contains("vendingmachine"):
                    return "Vending Machine";
                case var name when name.Contains("woodbox_deployed"):
                    return "Large Wood Box";
                default:
                    return "Storage";
            }
        }
        #endregion

        #region Core
        void HandleRapidDamage(BasePlayer player, int damage, string buildingGrade, string damageType)
        {
            if (!combinedDamage.ContainsKey(player))
            {
                combinedDamage[player] = new Dictionary<string, CombinedDamage>();
            }

            var key = $"{buildingGrade}_{damageType}";
            if (!combinedDamage[player].ContainsKey(key))
            {
                combinedDamage[player][key] = new CombinedDamage
                {
                    totalDamage = 0,
                    hitCount = 0,
                    lastHitTime = 0,
                    buildingGrade = buildingGrade,
                    damageType = damageType
                };
            }

            var combined = combinedDamage[player][key];
            combined.totalDamage += damage;
            combined.hitCount++;
            combined.lastHitTime = Time.time;

            combined.displayTimer?.Destroy();
            combined.displayTimer = timer.Once(rapidDamageTimeout, () =>
            {
                DamageNotifier(player, combined.totalDamage, buildingGrade, $"{damageType} x{combined.hitCount}", damageTimeout);
                combinedDamage[player].Remove(key);
            });
        }

        void OnDamageTimer()
        {            
            var toRemove = new List<BasePlayer>();
            float time = Time.time;
            
            foreach (var dmgHistoryKVP in damageHistory)
            {
                var player = dmgHistoryKVP.Key;
                var damages = dmgHistoryKVP.Value;
                bool needsRedraw = false;
                
                // Remove expired damages
                for (var i = damages.Count - 1; i >= 0; i--)
                {
                    var item = damages[i];
                    if (item.Key < time)
                    {
                        CuiHelper.DestroyUi(player, "constructiondamage" + item.Value.num.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        damages.RemoveAt(i);
                        needsRedraw = true;
                    }
                }
                
                if (damages.Count == 0)
                {
                    toRemove.Add(player);
                }
                else if (needsRedraw)
                {
                    DrawDamageNotifier(player);
                }
            }
            
            foreach (var player in toRemove)
            {
                damageHistory.Remove(player);
            }
        }

        void DamageNotifier(BasePlayer player, int damage, string buildingGrade, string damageType, float timeout = 0.5f, bool isDestroyed = false)
        {
            List<KeyValuePair<float, DamageInfo>> damages;
            if (!damageHistory.TryGetValue(player, out damages))
                damageHistory[player] = damages = new List<KeyValuePair<float, DamageInfo>>();
            
            // Use different x position for destroyed notifications
            double xPos = isDestroyed ? 0.3919792 : 0.4919792;
            
            var damageInfo = new DamageInfo 
            { 
                damage = damage,
                buildingGrade = buildingGrade,
                damageType = damageType,
                xs = xPos,
                ys = 0.4531481,
                xe = xPos + 0.1830208,
                ye = 0.5587038,
                num = rnd.Next(0, 10000),
                isDestroyed = isDestroyed,
                createdTime = Time.time
            };

            damages.Insert(0, new KeyValuePair<float, DamageInfo>(Time.time + timeout, damageInfo));
           
            DrawDamageNotifier(player);

            // Schedule cleanup
            timer.Once(timeout, () =>
            {
                CuiHelper.DestroyUi(player, "constructiondamage" + damageInfo.num.ToString(System.Globalization.CultureInfo.InvariantCulture));
            });
        }        

        void DrawDamageNotifier(BasePlayer player)
        {						
            List<KeyValuePair<float, DamageInfo>> damages;
            if (!damageHistory.TryGetValue(player, out damages) || damages.Count == 0) return;
			
            // Sort damages by creation time, newest first
            damages.Sort((a, b) => b.Value.createdTime.CompareTo(a.Value.createdTime));

            float baseY = 0.4531481f;
            float spacing = 0.03f; // Spacing between damage numbers

            for (var i = 0; i < damages.Count; i++)
            {
                var item = damages[i];
                string displayText;
                if (item.Value.isDestroyed)
                {
                    displayText = "DEST";
                    if (showBuildingGrade && item.Value.buildingGrade != null)
                        displayText += $" [{item.Value.buildingGrade}]";
                }
                else
                {
                    displayText = $"-{item.Value.damage}";
                    if (showBuildingGrade && item.Value.buildingGrade != null)
                        displayText += $" [{item.Value.buildingGrade}]";
                    if (showDamageType && item.Value.damageType != null)
                        displayText += $" ({item.Value.damageType})";
                }

                string color = item.Value.isDestroyed ? "red" : item.Value.buildingGrade switch
                {
                    "Wood" => "#8b4513",
                    "Stone" => "#808080",
                    "Sheet Metal" => "#c0c0c0",
                    "Armored" => "#4682b4",
                    "TopTier" => "#ffd700",
                    "Twigs" => "#cd7f32",
                    _ => "white"
                };

                float yPos = baseY + (spacing * i);
                
                CuiHelper.DestroyUi(player, "constructiondamage" + item.Value.num.ToString(System.Globalization.CultureInfo.InvariantCulture));
                CuiHelper.AddUi(player, HandleArgs(DamageGUI, 
                    item.Value.num,
                    $"<color={color}>{displayText}</color>",
                    item.Value.xs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    yPos.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    item.Value.xe.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    (yPos + 0.03f).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }			            
        }        
        #endregion
    }
}