﻿using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using Facepunch;
using Newtonsoft.Json;
using VLB;

namespace Oxide.Plugins
{
    [Info("Bradley Guards", "Bazz3l", "1.4.1")]
    [Description("Call in armed reinforcements when bradley is destroyed at launch site.")]
    public class BradleyGuards : RustPlugin
    {
        [PluginReference] Plugin Kits;

        #region Fields

        private const string CH47_PREFAB = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";
        private const string AI_PREFAB = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab";
        private const string LANDING_NAME = "BradleyLandingZone";

        private readonly HashSet<ScientistNPC> _npcs = new HashSet<ScientistNPC>();
        private CH47HelicopterAIController _chinook;
        private CH47LandingZone _landingZone;
        private Quaternion _landingRotation;
        private Vector3 _monumentPosition;
        private Vector3 _landingPosition;
        private Vector3 _chinookPosition;
        private Vector3 _bradleyPosition;
        private bool _hasLaunch;
        private static PluginConfig _config;

        #endregion

        #region Config

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (_config.ToDictionary().Keys
                    .SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys)) return;
            }
            catch
            {
                PrintWarning("Loaded default config, please check your configuration file for errors.");

                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        class PluginConfig
        {
            [JsonProperty(PropertyName = "ChatIcon (chat icon SteamID64)")]
            public ulong ChatIcon;

            [JsonProperty(PropertyName = "APCHealth (set starting health)")]
            public float APCHealth;

            [JsonProperty(PropertyName = "APCCrates (amount of crates to spawn)")]
            public int APCCrates;

            [JsonProperty(PropertyName = "NPCAmount (amount of guards to spawn max 11)")]
            public int NPCAmount;

            [JsonProperty(PropertyName = "InstantCrates (unlock crates when guards are eliminated)")]
            public bool InstantCrates;

            [JsonProperty(PropertyName = "DisableChinookDamage (should chinook be able to take damage)")]
            public bool DisableChinookDamage;

            [JsonProperty(PropertyName = "GuardSettings (create different types of guards must contain atleast 1)")]
            public List<GuardSetting> GuardSettings;

            [JsonProperty("EffectiveWeaponRange (range weapons will be effective)")]
            public Dictionary<string, float> EffectiveWeaponRange = new Dictionary<string, float>
            {
                { "snowballgun", 60f },
                { "rifle.ak", 150f },
                { "rifle.bolt", 150f },
                { "bow.hunting", 30f },
                { "bow.compound", 30f },
                { "crossbow", 30f },
                { "shotgun.double", 10f },
                { "pistol.eoka", 10f },
                { "multiplegrenadelauncher", 50f },
                { "rifle.l96", 150f },
                { "rifle.lr300", 150f },
                { "lmg.m249", 150f },
                { "rifle.m39", 150f },
                { "pistol.m92", 15f },
                { "smg.mp5", 80f },
                { "pistol.nailgun", 10f },
                { "shotgun.waterpipe", 10f },
                { "pistol.python", 60f },
                { "pistol.revolver", 50f },
                { "rocket.launcher", 60f },
                { "shotgun.pump", 10f },
                { "pistol.semiauto", 30f },
                { "rifle.semiauto", 100f },
                { "smg.2", 80f },
                { "shotgun.spas12", 30f },
                { "speargun", 10f },
                { "smg.thompson", 30f }
            };

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    ChatIcon = 0,
                    APCHealth = 1000f,
                    APCCrates = 4,
                    NPCAmount = 6,
                    InstantCrates = true,
                    GuardSettings = new List<GuardSetting> {
                        new GuardSetting
                        {
                            Name = "Heavy Gunner",
                            Health = 300f,
                            MaxRoamRadius = 80f,
                            MaxAggressionRange = 200f,
                        },
                        new GuardSetting
                        {
                            Name = "Light Gunner",
                            Health = 200f,
                            MaxRoamRadius = 80f,
                            MaxAggressionRange = 150f,
                        }
                    }
                };
            }

            public string ToJson() =>
                JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() =>
                JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        class GuardSetting
        {
            [JsonProperty(PropertyName = "Name (custom display name)")]
            public string Name;

            [JsonProperty(PropertyName = "Health (set starting health)")]
            public float Health = 100f;

            [JsonProperty(PropertyName = "DamageScale (higher the value more damage)")]
            public float DamageScale = 0.2f;

            [JsonProperty(PropertyName = "MaxRoamRadius (max radius guards will roam)")]
            public float MaxRoamRadius = 30f;

            [JsonProperty(PropertyName = "MaxAggressionRange (distance guards will become aggressive)")]
            public float MaxAggressionRange = 200f;

            [JsonProperty(PropertyName = "KitName (custom kit name)")]
            public string KitName = "";

            [JsonProperty(PropertyName = "KitEnabled (enable custom kit)")]
            public bool KitEnabled = false;
        }

        #endregion

        #region Oxide

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                {"EventStart", "<color=#DC143C>Bradley Guards</color>: Tank commander sent for reinforcements, fight for your life."},
                {"EventEnded", "<color=#DC143C>Bradley Guards</color>: Reinforcements have been eliminated, loot up fast."},
            }, this);
        }

        void OnServerInitialized() =>
            GetLandingPoint();

        void Unload()
        {
            CleanUp();

            _config = null;
        }

        void OnEntitySpawned(BradleyAPC bradley) =>
            OnAPCSpawned(bradley);

        void OnEntityDeath(BradleyAPC bradley, HitInfo info) =>
            OnAPCDeath(bradley);

        void OnEntityDeath(ScientistNPC npc, HitInfo info) =>
            OnNPCDeath(npc);

        void OnEntityKill(ScientistNPC npc) =>
            OnNPCDeath(npc);

        object OnHelicopterAttacked(CH47HelicopterAIController heli, HitInfo info) =>
            OnCH47Attacked(heli, info);

        void OnFireBallDamage(FireBall fireball, ScientistNPC npc, HitInfo info)
        {
            if (!(_npcs.Contains(npc) && info.Initiator is FireBall)) return;

            info.DoHitEffects = false;
            info.damageTypes.ScaleAll(0f);
        }

        void OnEntityDismounted(BaseMountable mountable, ScientistNPC npc)
        {
            if (!_npcs.Contains(npc) || !npc.HasBrain)
                return;

            npc.Brain.Navigator.PlaceOnNavMesh();
            npc.Brain.Navigator.SetDestination(RandomCircle(_bradleyPosition, 5f));
        }

        #endregion

        #region Core

        void SpawnEvent()
        {
            _chinook = GameManager.server.CreateEntity(CH47_PREFAB, _chinookPosition, Quaternion.identity) as CH47HelicopterAIController;
            _chinook.Spawn();
            _chinook.SetLandingTarget(_landingPosition);
            _chinook.SetMinHoverHeight(1.5f);
            _chinook.CancelInvoke(new Action(_chinook.SpawnScientists));
            _chinook.GetOrAddComponent<CH47NavigationComponent>();

            for (int i = 0; i < _config.NPCAmount - 1; i++)
            {
                SpawnScientist(_config.GuardSettings.GetRandom(), _chinook.transform.position + _chinook.transform.forward * 10f, _bradleyPosition);
            }

            for (int j = 0; j < 1; j++)
            {
                SpawnScientist(_config.GuardSettings.GetRandom(), _chinook.transform.position - _chinook.transform.forward * 15f, _bradleyPosition);
            }

            MessageAll("EventStart");
        }

        void SpawnScientist(GuardSetting settings, Vector3 position, Vector3 eventPos)
        {
            ScientistNPC npc = GameManager.server.CreateEntity(AI_PREFAB, position, Quaternion.identity) as ScientistNPC;
            if (npc == null) return;
            npc.Spawn();

            _chinook.AttemptMount(npc);

            npc.startHealth = settings.Health;
            npc.displayName = settings.Name;
            npc.damageScale = settings.DamageScale;
            npc.startHealth = settings.Health;
            npc.InitializeHealth(settings.Health, settings.Health);

            _npcs.Add(npc);

            GiveKit(npc, settings);

            NextFrame(() =>
            {
                if (npc == null || npc.IsDestroyed) return;

                Vector3 roamPoint = RandomCircle(eventPos, 5f);

                npc.Brain.Navigator.Agent.agentTypeID = -1372625422;
                npc.Brain.Navigator.DefaultArea = "Walkable";
                npc.Brain.AllowedToSleep = false;
                npc.Brain.Navigator.Init(npc, npc.Brain.Navigator.Agent);
                npc.Brain.ForceSetAge(0);
                npc.Brain.states.Remove(AIState.TakeCover);
                npc.Brain.states.Remove(AIState.Flee);
                npc.Brain.states.Remove(AIState.Roam);
                npc.Brain.states.Remove(AIState.Chase);
                npc.Brain.Navigator.BestCoverPointMaxDistance = settings.MaxRoamRadius / 2;
                npc.Brain.Navigator.BestRoamPointMaxDistance = settings.MaxRoamRadius;
                npc.Brain.Navigator.MaxRoamDistanceFromHome = settings.MaxRoamRadius;
                npc.Brain.AddState(new TakeCoverState { brain = npc.Brain, Position = roamPoint });
                npc.Brain.AddState(new ChaseState { brain = npc.Brain });
                npc.Brain.AddState(new RoamState { brain = npc.Brain, Position = roamPoint });
                npc.Brain.Senses.Init(npc, 5f, settings.MaxAggressionRange, settings.MaxAggressionRange + 5f, -1f, true, true, true, settings.MaxAggressionRange, false, false, true, EntityType.Player, false);
            });
        }

        void OnNPCDeath(ScientistNPC npc)
        {
            if (!_npcs.Remove(npc) || _npcs.Count > 0) return;

            if (_config.InstantCrates)
            {
                RemoveFlames();
                UnlockCrates();
            }

            MessageAll("EventEnded");
        }

        void OnAPCSpawned(BradleyAPC bradley)
        {
            Vector3 position = bradley.transform.position;

            if (!IsInBounds(position)) return;

            bradley.maxCratesToSpawn = _config.APCCrates;
            bradley._maxHealth = bradley._health = _config.APCHealth;
            bradley.health = bradley._maxHealth;

            ClearGuards();
        }

        void OnAPCDeath(BradleyAPC bradley)
        {
            if (bradley == null || bradley.IsDestroyed) return;

            Vector3 position = bradley.transform.position;

            if (!IsInBounds(position)) return;

            _bradleyPosition = position;

            SpawnEvent();
        }

        object OnCH47Attacked(CH47HelicopterAIController heli, HitInfo info)
        {
            if (heli == null || !_config.DisableChinookDamage) return null;
            if (heli == _chinook) return true;
            return null;
        }

        void RemoveFlames()
        {
            List<FireBall> entities = Pool.GetList<FireBall>();

            Vis.Entities(_bradleyPosition, 25f, entities);

            foreach (FireBall fireball in entities)
            {
                if (fireball.IsValid() && !fireball.IsDestroyed)
                    fireball.Kill();
            }

            Pool.FreeList(ref entities);
        }

        void UnlockCrates()
        {
            List<LockedByEntCrate> entities = Pool.GetList<LockedByEntCrate>();

            Vis.Entities(_bradleyPosition, 25f, entities);

            foreach (LockedByEntCrate crate in entities)
            {
                if (!(crate.IsValid() && !crate.IsDestroyed)) continue;

                crate.SetLocked(false);

                if (crate.lockingEnt == null) continue;

                BaseEntity entity = crate.lockingEnt.GetComponent<BaseEntity>();

                if (entity.IsValid() && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            }

            Pool.FreeList(ref entities);
        }

        void CreateLandingZone()
        {
            GameObject gameObject = new GameObject(LANDING_NAME);
            gameObject.transform.SetPositionAndRotation(_landingPosition, _landingRotation);
            _landingZone = gameObject.AddComponent<CH47LandingZone>();
        }

        void CleanUp()
        {
            ClearGuards();
            ClearZones();
        }

        void ClearZones()
        {
            if (_landingZone == null) return;

            UnityEngine.Object.Destroy(_landingZone.gameObject);

            _landingZone = null;
        }

        void ClearGuards()
        {
            for (int i = 0; i < _npcs.Count; i++)
            {
                ScientistNPC npc = _npcs.ElementAt(i);

                if (npc.IsValid() && !npc.IsDestroyed)
                    npc.Kill();
            }

            _npcs.Clear();
        }

        void GetLandingPoint()
        {
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (!monument.gameObject.name.Contains("launch_site_1")) continue;

                SetLandingPoint(monument);
            }
        }

        void SetLandingPoint(MonumentInfo monument)
        {
            _monumentPosition = monument.transform.position;

            _landingRotation = monument.transform.rotation;
            _landingPosition = _monumentPosition + monument.transform.right * 125f;
            _landingPosition.y = TerrainMeta.HeightMap.GetHeight(_landingPosition);

            _chinookPosition = _monumentPosition + -monument.transform.right * 250f;
            _chinookPosition.y += 150f;

            _hasLaunch = true;

            CreateLandingZone();
        }

        bool IsInBounds(Vector3 position) => _hasLaunch && Vector3.Distance(_monumentPosition, position) <= 300f;

        #endregion

        #region AI States

        class RoamState : ScientistBrain.BasicAIState
        {
            private float _nextRoamPositionTime;
            public Vector3 Position;

            public RoamState() : base(AIState.Roam)
            {
                //
            }

            public override void StateEnter()
            {
                Reset();

                base.StateEnter();

                _nextRoamPositionTime = 0.0f;
            }

            public override float GetWeight() => 0.0f;

            private Vector3 GetDestination() => Position;

            private void SetDestination(Vector3 destination) => brain.Navigator.SetDestination(destination, BaseNavigator.NavigationSpeed.Fast);

            public override StateStatus StateThink(float delta)
            {
                if (Vector3.Distance(GetDestination(), GetEntity().transform.position) > 10.0 && _nextRoamPositionTime < Time.time)
                {
                    Vector3 insideUnitSphere = UnityEngine.Random.insideUnitSphere;
                    insideUnitSphere.y = 0.0f;
                    insideUnitSphere.Normalize();

                    SetDestination(GetDestination() + insideUnitSphere * 2f);

                    _nextRoamPositionTime = Time.time + UnityEngine.Random.Range(0.5f, 1f);
                }

                return StateStatus.Running;
            }
        }

        class ChaseState : ScientistBrain.BasicAIState
        {
            private StateStatus _status = StateStatus.Error;
            private float _nextPositionUpdateTime;

            public ChaseState() : base(AIState.Chase)
            {
                AgrresiveState = true;
            }

            public override void StateEnter()
            {
                Reset();

                base.StateEnter();

                _status = StateStatus.Error;

                if (brain.PathFinder == null)
                    return;

                _status = StateStatus.Running;

                _nextPositionUpdateTime = 0.0f;
            }

            public override void StateLeave()
            {
                base.StateLeave();

                Stop();
            }

            public override StateStatus StateThink(float delta)
            {
                if (_status == StateStatus.Error)
                    return _status;

                BaseEntity baseEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                if (baseEntity == null)
                    return StateStatus.Error;

                ScientistNPC entity = (ScientistNPC)GetEntity();

                float num2 = Vector3.Distance(baseEntity.transform.position, entity.transform.position);

                if (brain.Senses.Memory.IsLOS(baseEntity) || (double)num2 <= 30.0)
                    brain.Navigator.SetFacingDirectionEntity(baseEntity);
                else
                    brain.Navigator.ClearFacingDirectionOverride();

                brain.Navigator.SetCurrentSpeed(num2 <= 30.0
                    ? BaseNavigator.NavigationSpeed.Normal
                    : BaseNavigator.NavigationSpeed.Fast);

                if (_nextPositionUpdateTime < Time.time)
                {
                    _nextPositionUpdateTime = Time.time + UnityEngine.Random.Range(0.5f, 1f);

                    brain.Navigator.SetDestination(baseEntity.transform.position, BaseNavigator.NavigationSpeed.Normal);
                }

                return brain.Navigator.Moving
                    ? StateStatus.Running
                    : StateStatus.Finished;
            }

            private void Stop()
            {
                brain.Navigator.Stop();
                brain.Navigator.ClearFacingDirectionOverride();
            }
        }

        class TakeCoverState : ScientistBrain.BasicAIState
        {
            private StateStatus _status = StateStatus.Error;
            private BaseEntity coverFromEntity;
            public Vector3 Position;

            public TakeCoverState() : base(AIState.TakeCover)
            {
                //
            }

            public override void StateEnter()
            {
                Reset();

                base.StateEnter();

                _status = StateStatus.Running;

                if (StartMovingToCover())
                    return;

                _status = StateStatus.Error;
            }

            public override void StateLeave()
            {
                base.StateLeave();

                brain.Navigator.ClearFacingDirectionOverride();

                ClearCoverPointUsage();
            }

            private void ClearCoverPointUsage()
            {
                AIPoint aiPoint = brain.Events.Memory.AIPoint.Get(4);
                if (aiPoint == null) return;

                aiPoint.ClearIfUsedBy(GetEntity());
            }

            private bool StartMovingToCover() => brain.Navigator.SetDestination(Position, BaseNavigator.NavigationSpeed.Normal);

            public override StateStatus StateThink(float delta)
            {
                FaceCoverFromEntity();

                if (_status == StateStatus.Error)
                    return _status;

                return brain.Navigator.Moving ? StateStatus.Running : StateStatus.Finished;
            }

            private void FaceCoverFromEntity()
            {
                coverFromEntity = brain.Events.Memory.Entity.Get(brain.Events.CurrentInputMemorySlot);
                if (coverFromEntity == null)
                    return;

                brain.Navigator.SetFacingDirectionEntity(coverFromEntity);
            }
        }

        #endregion

        #region Component

        class CH47NavigationComponent : MonoBehaviour
        {
            CH47HelicopterAIController _chinook;

            void Awake()
            {
                _chinook = GetComponent<CH47HelicopterAIController>();

                InvokeRepeating(nameof(CheckDropped), 5f, 5f);
            }

            void OnDestroy()
            {
                CancelInvoke();

                if (_chinook.IsValid() && !_chinook.IsDestroyed)
                    _chinook.Invoke(_chinook.DelayedKill, 10f);
            }

            void CheckDropped()
            {
                if (_chinook.NumMounted() > 0) return;

                Destroy(this);
            }
        }

        #endregion

        #region Helpers

        string Lang(string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, this, id), args);

        void MessageAll(string key) =>
            Server.Broadcast(Lang(key, null), _config.ChatIcon);

        static Vector3 RandomCircle(Vector3 center, float radius)
        {
            float ang = UnityEngine.Random.value * 360;
            Vector3 pos;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = center.y;
            return pos;
        }

        static void GiveKit(ScientistNPC npc, GuardSetting settings)
        {
            if (settings.KitEnabled)
            {
                npc.inventory.Strip();

                Interface.Oxide.CallHook("GiveKit", npc, settings.KitName);
            }

            for (int i = 0; i < npc.inventory.containerBelt.itemList.Count; i++)
            {
                Item item = npc.inventory.containerBelt.itemList[i];
                if (item == null) continue;

                BaseProjectile projectile = (item?.GetHeldEntity() as HeldEntity) as BaseProjectile;
                if (projectile == null) return;

                if (_config.EffectiveWeaponRange.ContainsKey(item.info.shortname))
                    projectile.effectiveRange = _config.EffectiveWeaponRange[item.info.shortname];
                else
                    projectile.effectiveRange = settings.MaxAggressionRange;

                projectile.CanUseAtMediumRange = true;
                projectile.CanUseAtLongRange = true;
            }

            npc.Invoke(npc.EquipWeapon, 0.25f);
        }

        #endregion
    }
}