using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Mini-Copter Options", "Pho3niX90", "2.0.6")]
    [Description("Provide a number of additional options for Mini-Copters, including storage and seats.")]
    class MiniCopterOptions : RustPlugin
    {
        bool lastRanAtNight;
        #region Prefab Modifications

        private readonly string storagePrefab = "assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab";
        private readonly string storageLargePrefab = "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab";
        private readonly string autoturretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private readonly string switchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        private readonly string searchLightPrefab = "assets/prefabs/deployable/search light/searchlight.deployed.prefab";
        private readonly string batteryPrefab = "assets/prefabs/deployable/playerioents/batteries/smallrechargablebattery.deployed.prefab";
        private readonly string flasherBluePrefab = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab";
        private readonly string lockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private readonly string spherePrefab = "assets/prefabs/visualization/sphere.prefab";

        TOD_Sky time;
        float sunrise;
        float sunset;
        float lastCheck;

        void Unload() {
            foreach (var copter in UnityEngine.Object.FindObjectsOfType<MiniCopter>()) {
                if (config.restoreDefaults) RestoreMiniCopter(copter, config.reloadStorage);
                if (config.landOnCargo) UnityEngine.Object.Destroy(copter.GetComponent<MiniShipLandingGear>());
            }

            if (config.lightTail) {
                time.Components.Time.OnHour -= OnHour;
            }
        }

        void OnHour() {
            float hour = time.Cycle.Hour;
            bool isNight = !(hour >= sunrise && hour <= sunset);
            //Puts($"OnHour: hour is now {hour}, and it is night {isNight}");
            if ((isNight == lastRanAtNight) || (lastCheck == hour)) return;
            //Puts($"OnHour Called: Night:{isNight} LastRanAtNight:{lastRanAtNight}");
            lastCheck = hour;

            MiniCopter[] minis = BaseNetworkable.FindObjectsOfType<MiniCopter>().Where(x => x.GetComponentInChildren<FlasherLight>() != null).ToArray();
            //Puts($"OnHour Called: Minis to modify {minis.Count()}");
            foreach (var mini in minis) {
                ToggleLight(mini.GetComponentInChildren<FlasherLight>());
            }
            lastRanAtNight ^= true;
        }

        void OnPlayerInput(BasePlayer player, InputState input) {
            if (!config.addSearchLight || player == null || input == null) return;
            if (player.isMounted) {
                BaseVehicle vehicle = player.GetMountedVehicle();
                if (vehicle != null && vehicle is MiniCopter && input.WasJustPressed(BUTTON.USE)) {
                    ToggleMiniLights(vehicle as MiniCopter);
                }
            }
        }

        void ToggleMiniLights(MiniCopter mini) {
            foreach (var light in mini.GetComponentsInChildren<SearchLight>()) {
                ToggleFLight(light);
            }
        }

        private void OnEntityDismounted(BaseNetworkable entity, BasePlayer player) {
            if (config.flyHackPause > 0 && entity.GetParentEntity() is MiniCopter)
                player.PauseFlyHackDetection(config.flyHackPause);
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity) {
            if (!(entity is MiniCopter) && !(entity.GetParentEntity() is MiniCopter)) return null;

            if (!IsBatEnabled()) return null;
            MiniCopter ent = entity.GetParentEntity() as MiniCopter;
            if (ent != null) {
                IOEntity ioe = GetBatteryConnected(ent);
                if (ioe != null) {
                    SendReply(player, GetMsg("Err - Diconnect Battery"), ioe.GetDisplayName());
                    return false;
                }
            }
            return null;
        }

        bool hasStorage(MiniCopter copter) => copter.GetComponentsInChildren<StorageContainer>().Any(x => x.name == storagePrefab || x.name == storageLargePrefab);

        void AddLargeStorageBox(MiniCopter copter) {
            //sides,negative left | up and down | in and out

            if (config.storageLargeContainers == 1) {
                AddStorageBox(copter, storageLargePrefab, new Vector3(0.0f, 0.07f, -1.05f), Quaternion.Euler(0, 180f, 0));
            } else if (config.storageLargeContainers >= 2) {
                AddStorageBox(copter, storageLargePrefab, new Vector3(-0.48f, 0.07f, -1.05f), Quaternion.Euler(0, 180f, 0));
                AddStorageBox(copter, storageLargePrefab, new Vector3(0.48f, 0.07f, -1.05f), Quaternion.Euler(0, 180f, 0));
            }

        }

        void AddRearStorageBox(MiniCopter copter) {
            AddStorageBox(copter, storagePrefab, new Vector3(0, 0.75f, -1f));
        }

        void AddSideStorageBoxes(MiniCopter copter) {
            AddStorageBox(copter, storagePrefab, new Vector3(0.6f, 0.24f, -0.35f));
            if (!IsBatEnabled()) AddStorageBox(copter, storagePrefab, new Vector3(-0.6f, 0.24f, -0.35f));
        }

        void AddStorageBox(MiniCopter copter, string prefab, Vector3 position) => AddStorageBox(copter, prefab, position, new Quaternion());

        void AddStorageBox(MiniCopter copter, string prefab, Vector3 position, Quaternion q) {

            StorageContainer box = GameManager.server.CreateEntity(prefab, copter.transform.position, q) as StorageContainer;

            if (prefab.Equals(storageLargePrefab) && config.largeStorageLockable) {
                box.isLockable = true;
                box.panelName = GetPanelName(config.largeStorageSize);
            }

            box.Spawn();
            box.SetParent(copter);
            box.transform.localPosition = position;

            if (prefab.Equals(storageLargePrefab) && config.largeStorageLockable) {
                box.inventory.capacity = config.largeStorageSize;
            }

            box.SendNetworkUpdateImmediate(true);
        }

        void AddFlightLigts(MiniCopter copter) {
            FlasherLight alight = GameManager.server.CreateEntity(flasherBluePrefab, copter.transform.position) as FlasherLight;
            alight.Spawn();
            DestroyGroundComp(alight);
            ToggleLight(alight);
            alight.SetParent(copter);
            alight.transform.localPosition = new Vector3(0, 1.2f, -2.0f);
            alight.transform.localRotation = Quaternion.Euler(new Vector3(33, 180, 0));
            alight.SendNetworkUpdateImmediate();
        }

        void ToggleLight(IOEntity ent) {
            ent.UpdateHasPower(!ent.IsPowered() ? 10 : 0, 1);
            ent.SetFlag(BaseEntity.Flags.On, !ent.IsPowered());
            ent.SendNetworkUpdateImmediate();
        }

        void ToggleFLight(BaseEntity ent) {
            ent.SetFlag(BaseEntity.Flags.On, !ent.IsOn());
            if (ent is SearchLight) {
                SearchLight sl = ent as SearchLight;
                sl.UpdateHasPower(ent.IsOn() ? 10 : 0, 1);
            }
            ent.SendNetworkUpdateImmediate();
        }

        void AddSearchLight(MiniCopter copter) {
            SphereEntity sph = (SphereEntity)GameManager.server.CreateEntity(spherePrefab, copter.transform.position, new Quaternion(0, 0, 0, 0), true);
            DestroyMeshCollider(sph);
            DestroyGroundComp(sph);
            sph.Spawn();
            sph.SetParent(copter);
            sph.transform.localPosition = new Vector3(0, -100, 0);
            SearchLight searchLight = GameManager.server.CreateEntity(searchLightPrefab, sph.transform.position) as SearchLight;
            DestroyMeshCollider(searchLight);
            DestroyGroundComp(searchLight);
            searchLight.Spawn();
            searchLight.SetFlag(BaseEntity.Flags.Reserved5, true, false, true);
            searchLight.SetFlag(BaseEntity.Flags.Busy, true);
            searchLight.SetParent(sph);
            searchLight.transform.localPosition = new Vector3(0, 0, 0);
            searchLight.transform.localRotation = Quaternion.Euler(new Vector3(-20, 180, 180));
            Puts(searchLight.eyePoint.transform.position.ToString());
            searchLight._maxHealth = 99999999f;
            searchLight._health = 99999999f;
            searchLight.pickup.enabled = false;
            //searchLight.isLockable = true;
            searchLight.SendNetworkUpdate();
            sph.transform.localScale += new Vector3(0.9f, 0, 0);
            sph.LerpRadiusTo(0.1f, 10f);
            timer.Once(3f, () => {
                sph.transform.localPosition = new Vector3(0, 0.24f, 1.8f);
            });
            sph.SendNetworkUpdateImmediate();
        }

        void AddLock(BaseEntity ent) {
            CodeLock alock = GameManager.server.CreateEntity(lockPrefab) as CodeLock;

            alock.Spawn();
            alock.code = "789456789123";
            alock.SetParent(ent, ent.GetSlotAnchorName(BaseEntity.Slot.Lock));
            alock.transform.localScale += new Vector3(-50, -50, -50);
            ent.SetSlot(BaseEntity.Slot.Lock, alock);
            alock.SetFlag(BaseEntity.Flags.Locked, true);
            alock.SendNetworkUpdateImmediate();
        }

        void AddTurret(MiniCopter copter) {
            AutoTurret aturret = GameManager.server.CreateEntity(autoturretPrefab, copter.transform.position) as AutoTurret;
            DestroyMeshCollider(aturret);
            DestroyGroundComp(aturret);
            aturret.Spawn();
            aturret.pickup.enabled = false;
            aturret.sightRange = config.turretRange;
            aturret.SetParent(copter);
            aturret.transform.localPosition = new Vector3(0, 0, 2.47f);
            aturret.transform.localRotation = Quaternion.Euler(0, 0, 0);
            ProtoBuf.PlayerNameID pnid = new ProtoBuf.PlayerNameID();
            BasePlayer player = BasePlayer.FindByID(copter.OwnerID);
            if (player != null) {
                pnid.userid = player.userID;
                pnid.username = player?.displayName;
                aturret.authorizedPlayers.Add(pnid);
            }
            aturret.SendNetworkUpdate();
            AddSwitch(aturret);
        }

        bool IsBatEnabled() => config.autoturretBattery && config.autoturret;

        ElectricBattery AddBattery(MiniCopter copter) {
            ElectricBattery abat = GameManager.server.CreateEntity(batteryPrefab, copter.transform.position) as ElectricBattery;
            abat.maxOutput = 12;
            abat.Spawn();
            DestroyGroundComp(abat);
            abat.pickup.enabled = false;
            abat.SetParent(copter);
            abat.transform.localPosition = new Vector3(-0.7f, 0.2f, -0.2f);
            abat.transform.localRotation = Quaternion.Euler(0, 0, 0);
            abat.SendNetworkUpdate();
            return abat;
        }

        void AddSwitch(AutoTurret aturret) {
            ElectricBattery bat = null;
            if (IsBatEnabled()) {
                bat = AddBattery(aturret.GetParentEntity() as MiniCopter);
            }

            ElectricSwitch aSwitch = aturret.GetComponentInChildren<ElectricSwitch>();
            aSwitch = GameManager.server.CreateEntity(switchPrefab, aturret.transform.position)?.GetComponent<ElectricSwitch>();
            if (aSwitch == null) return;
            aSwitch.pickup.enabled = false;
            aSwitch.SetParent(aturret);
            aSwitch.transform.localPosition = new Vector3(0f, -0.65f, 0.325f);
            aSwitch.transform.localRotation = Quaternion.Euler(0, 0, 0);
            DestroyMeshCollider(aSwitch);
            DestroyGroundComp(aSwitch);
            aSwitch.Spawn();
            aSwitch._limitedNetworking = false;
            if (!IsBatEnabled()) {
                RunWire(aSwitch, 0, aturret, 0, 12);
            } else if (bat != null) {
                RunWire(bat, 0, aSwitch, 0);
                RunWire(aSwitch, 0, aturret, 0);
            }
        }

        // https://umod.org/community/rust/12554-trouble-spawning-a-switch?page=1#post-5
        private void RunWire(IOEntity source, int s_slot, IOEntity destination, int d_slot, int power = 0) {
            destination.inputs[d_slot].connectedTo.Set(source);
            destination.inputs[d_slot].connectedToSlot = s_slot;
            destination.inputs[d_slot].connectedTo.Init();
            source.outputs[s_slot].connectedTo.Set(destination);
            source.outputs[s_slot].connectedToSlot = d_slot;
            source.outputs[s_slot].connectedTo.Init();
            source.MarkDirtyForceUpdateOutputs();
            if (power > 0) {
                destination.UpdateHasPower(power, 0);
                source.UpdateHasPower(power, 0);
            }
            source.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            destination.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        void DestroyGroundComp(BaseEntity ent) {
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
        }

        void DestroyMeshCollider(BaseEntity ent) {
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>()) {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        IOEntity GetBatteryConnected(MiniCopter ent) {
            return ent.GetComponentInChildren<ElectricBattery>()?.inputs[0]?.connectedTo.ioEnt;
        }

        /* Credit to WhiteThunder. 
         * This improves compatibility with other plugins as it allows them to block the switch from being toggled using the pre-hook.
         */
        private object OnSwitchToggled(ElectricSwitch electricSwitch, BasePlayer player) {
            if (IsBatEnabled()) return null;

            AutoTurret turret = electricSwitch.GetParentEntity() as AutoTurret;
            if (turret == null) return null;

            var mini = turret.GetParentEntity() as MiniCopter;
            if (mini == null) return null;

            if (electricSwitch.IsOn())
                PowerTurretOn(turret);
            else
                PowerTurretOff(turret);

            return null;
        }

        public void PowerTurretOn(AutoTurret turret) {
            turret.SetFlag(BaseEntity.Flags.Reserved8, true);
            turret.InitiateStartup();
        }

        private void PowerTurretOff(AutoTurret turret) {
            turret.SetFlag(BaseEntity.Flags.Reserved8, false);
            turret.InitiateShutdown();
        }

        string GetPanelName(int capacity) {
            if (capacity <= 6) {
                return "smallstash";
            } else if (capacity > 6 && capacity <= 12) {
                return "smallwoodbox";
            } else if (capacity > 12 && capacity <= 30) {
                return "largewoodbox";
            } else {
                return "genericlarge";
            }
        }


        // o.reload MiniCopterOptions

        void RestoreMiniCopter(MiniCopter copter, bool removeStorage = false) {
            copter.fuelPerSec = copterDefaults.fuelPerSecond;
            copter.liftFraction = copterDefaults.liftFraction;
            copter.torqueScale = copterDefaults.torqueScale;
            if (removeStorage) {
                foreach (var child in copter.children.FindAll(child => child.name == storagePrefab || child.name == storageLargePrefab || child.name == autoturretPrefab))
                    child.Kill();
            }
        }

        void ModifyMiniCopter(MiniCopter copter) {

            copter.fuelPerSec = config.fuelPerSec;
            copter.liftFraction = config.liftFraction;
            copter.torqueScale = new Vector3(config.torqueScalePitch, config.torqueScaleYaw, config.torqueScaleRoll);

            if (config.autoturret && copter.GetComponentInChildren<AutoTurret>() == null) {
                timer.Once(copter.isSpawned ? 0 : 0.2f, () => {
                    AddTurret(copter);
                });
            }

            //only add storage containers if non are found.
            if (!hasStorage(copter)) {
                AddLargeStorageBox(copter);

                switch (config.storageContainers) {
                    case 1:
                        AddRearStorageBox(copter);
                        break;
                    case 2:
                        AddSideStorageBoxes(copter);
                        break;
                    case 3:
                        AddRearStorageBox(copter);
                        AddSideStorageBoxes(copter);
                        break;
                }
            }

        }

        void StoreMiniCopterDefaults(MiniCopter copter) {
            if (copter.liftFraction == 0 || copter.torqueScale.x == 0 || copter.torqueScale.y == 0 || copter.torqueScale.z == 0) {
                copter.liftFraction = 0.25f;
                copter.torqueScale = new Vector3(400f, 400f, 200f);
            }
            //Puts($"Defaults for copters saved as \nfuelPerSecond = {copter.fuelPerSec}\nliftFraction = {copter.liftFraction}\ntorqueScale = {copter.torqueScale}");
            copterDefaults = new MiniCopterDefaults {
                fuelPerSecond = copter.fuelPerSec,
                liftFraction = copter.liftFraction,
                torqueScale = copter.torqueScale
            };
        }

        #endregion

        #region Hooks

        void OnItemDeployed(Deployer deployer, BaseEntity entity) {
            if (entity?.GetParentEntity() != null && (entity.GetParentEntity() is MiniCopter)) {
                CodeLock cLock = entity.GetComponentInChildren<CodeLock>();
                cLock.transform.localPosition = new Vector3(0.0f, 0.3f, 0.298f);
                cLock.transform.localRotation = Quaternion.Euler(new Vector3(0, 90, 0));
                cLock.SendNetworkUpdateImmediate();
            }
        }

        void OnServerInitialized(bool init) {
            PrintWarning("Applying settings except storage modifications to existing MiniCopters.");
            if (config.lightTail) {
                time = TOD_Sky.Instance;
                sunrise = time.SunriseTime;
                sunset = time.SunsetTime;

                time.Components.Time.OnHour += OnHour;
            }

            foreach (var copter in UnityEngine.Object.FindObjectsOfType<MiniCopter>()) {
                ModifyMiniCopter(copter);
            }
        }

        void OnEntitySpawned(MiniCopter mini) {
            //PrintComponents(mini);
            if (mini.name.Contains("trans")) return;
            StoreMiniCopterDefaults(mini);
            // Only add storage on spawn so we don't stack or mess with
            // existing player storage containers. 
            ModifyMiniCopter(mini);
            if (config.landOnCargo) mini.gameObject.AddComponent<MiniShipLandingGear>();
            if (config.addSearchLight) AddSearchLight(mini);
            if (config.lightTail) AddFlightLigts(mini);
        }

        void OnEntityKill(BaseNetworkable entity) {
            if (!config.dropStorage || !(entity is MiniCopter)) return;
            StorageContainer[] containers = entity.GetComponentsInChildren<StorageContainer>();
            foreach (StorageContainer container in containers) {
                container.DropItems();
            }
            AutoTurret[] turrets = entity.GetComponentsInChildren<AutoTurret>();
            foreach (AutoTurret turret in turrets) {
                turret.DropItems();
            }
        }
        #endregion

        #region Configuration

        private class MiniCopterOptionsConfig
        {
            // Populated with Rust defaults.
            public float fuelPerSec = 0.25f;
            public float liftFraction = 0.25f;
            public float torqueScalePitch = 400f;
            public float torqueScaleYaw = 400f;
            public float torqueScaleRoll = 200f;

            public int storageContainers = 0;
            public int storageLargeContainers = 0;
            public bool restoreDefaults = true;
            public bool reloadStorage = false;
            public bool dropStorage = true;
            public bool largeStorageLockable = true;
            public int largeStorageSize = 42;
            public int flyHackPause = 1;
            public bool autoturret = false;
            public bool landOnCargo = true;
            public bool autoturretBattery = true;
            public bool addSearchLight = true;
            public float turretRange = 30f;
            public bool lightTail = false;

            // Plugin reference
            private MiniCopterOptions plugin;

            public MiniCopterOptionsConfig(MiniCopterOptions plugin) {
                this.plugin = plugin;

                GetConfig(ref fuelPerSec, "Fuel per Second");
                GetConfig(ref liftFraction, "Lift Fraction");
                GetConfig(ref torqueScalePitch, "Pitch Torque Scale");
                GetConfig(ref torqueScaleYaw, "Yaw Torque Scale");
                GetConfig(ref torqueScaleRoll, "Roll Torque Scale");
                GetConfig(ref storageContainers, "Storage Containers");
                GetConfig(ref storageLargeContainers, "Large Storage Containers");
                GetConfig(ref restoreDefaults, "Restore Defaults");
                GetConfig(ref reloadStorage, "Reload Storage");
                GetConfig(ref dropStorage, "Drop Storage Loot On Death");
                GetConfig(ref largeStorageLockable, "Large Storage Lockable");
                GetConfig(ref largeStorageSize, "Large Storage Size (Max 42)");
                GetConfig(ref flyHackPause, "Seconds to pause flyhack when dismount from heli.");
                GetConfig(ref autoturret, "Add auto turret to heli");
                GetConfig(ref autoturretBattery, "Auto turret uses battery");
                GetConfig(ref landOnCargo, "Allow Minis to Land on Cargo");
                GetConfig(ref turretRange, "Mini Turret Range (Default 30)");
                GetConfig(ref addSearchLight, "Light: Add Searchlight to heli");
                GetConfig(ref lightTail, "Light: Add Nightitme Tail Light");

                plugin.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path) {
                if (path.Length == 0) return;

                if (plugin.Config.Get(path) == null) {
                    SetConfig(ref variable, path);
                    plugin.PrintWarning($"Added field to config: {string.Join("/", path)}");
                }

                variable = (T)Convert.ChangeType(plugin.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) => plugin.Config.Set(path.Concat(new object[] { variable }).ToArray());
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file.");

        private MiniCopterOptionsConfig config;

        struct MiniCopterDefaults
        {
            public float fuelPerSecond;
            public float liftFraction;
            public Vector3 torqueScale;
        }

        MiniCopterDefaults copterDefaults;

        private void Init() {
            config = new MiniCopterOptionsConfig(this);

            if (config.storageContainers > 3) {
                PrintWarning($"Storage Containers configuration value {config.storageContainers} exceeds the maximum, setting to 3.");
                config.storageContainers = 3;
            } else if (config.storageContainers < 0) {
                PrintWarning($"Storage Containers cannot be a negative value, setting to 0.");
                config.storageContainers = 0;
            }

            if (config.storageLargeContainers > 2) {
                PrintWarning($"Large Storage Containers configuration value {config.storageLargeContainers} exceeds the maximum, setting to 2.");
                config.storageLargeContainers = 2;
            } else if (config.storageLargeContainers < 0) {
                PrintWarning($"Large Storage Containers cannot be a negative value, setting to 0.");
                config.storageLargeContainers = 0;
            }

            if (config.largeStorageSize > 42) {
                PrintWarning($"Large Storage Containers Capacity configuration value {config.largeStorageSize} exceeds the maximum, setting to 42.");
                config.largeStorageSize = 42;
            } else if (config.largeStorageSize < 6) {
                PrintWarning($"Storage Containers Capacity cannot be a smaller than 6, setting to 6.");
            }
        }

        #endregion

        #region Chat Commands
        #endregion

        #region Helpers


        private string GetEnglishName(string shortName) { return ItemManager.FindItemDefinition(shortName)?.displayName?.english ?? shortName; }

        void PrintComponents(BaseEntity ent) {
            foreach (var sl in ent.GetComponents<Component>()) {
                Puts($"-P- {sl.GetType().Name} | {sl.name}");
                foreach (var s in sl.GetComponentsInChildren<Component>()) {
                    Puts($"-C- {s.GetType().Name} | {s.name}");
                }
            }
        }
        #endregion

        #region Classes
        public class MiniShipLandingGear : MonoBehaviour
        {
            private MiniCopter miniCopter;
            private bool pCargo;

            void Awake() {
                miniCopter = GetComponent<MiniCopter>();
            }

            void OnTriggerEnter(Collider collider) {
                if (!collider.isTrigger || !(collider.ToBaseEntity() is CargoShip) || pCargo) return;
                ParentTo(miniCopter, collider.ToBaseEntity());
            }

            void OnTriggerExit(Collider collider) {
                if (!collider.isTrigger || !(collider.ToBaseEntity() is CargoShip) || !pCargo) return;
                ParentTo(miniCopter, null);
            }

            void ParentTo(MiniCopter mini, BaseEntity parent) {
                mini.SetParent(parent, true);
                pCargo ^= true;
            }
        }
        #endregion

        #region Languages
        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["Err - Diconnect Battery"] = "First disconnect battery input from {0}",
                ["Err - Can only push minicopter"] = "You have to look at a minicopter. Pushing {0} not allowed"
            }, this);
        }
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());

        #endregion

    }
}
