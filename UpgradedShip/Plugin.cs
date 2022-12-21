using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UpgradedShip
{
    [BepInPlugin("com.kuborro.plugins.fp2.upgradedship", "UpgradedShip", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Vector2 position;
        public static BossWeakpoint weakPoint;
        public static FPPlayer targetPlayer;
        public static float missileRange = 1024f;
        public static float missileDamage = 10f;
        public static int missileCount = 5;
        public static Transform baseTransform;
        private static TargetingCircle currentTargetingCircle;
        private static List<FPBaseEnemy> missileTargetedEnemies;
        public static List<TargetingReticle> reticles;
        public static AudioClip sfxMissile;
        public static AudioClip sfxMissileTargeting;
        public static int MissileID,CircleID,ReticleID;
        public static AssetBundle moddedBundle;
        public static TargetingCircle targetingCircle;
        public static TargetingReticle targetingReticle;
        public static BFFMicroMissile microMissile;

        private void Awake()
        {
            string assetPath = Path.Combine(Path.GetFullPath("."), "mod_overrides");
            moddedBundle = AssetBundle.LoadFromFile(Path.Combine(assetPath, "upgradedship.assets"));
            if (moddedBundle == null)
            {
                Logger.LogError("Failed to load AssetBundle! Mod cannot work without it, exiting. Please reinstall it.");
                return;
            }
            //HarmonyFileLog.Enabled = true;
            var harmony = new Harmony("com.kuborro.plugins.fp2.upgradedship");
            harmony.PatchAll(typeof(PatchStateStart));
            harmony.PatchAll(typeof(PatchStateMovingPre));
            harmony.PatchAll(typeof(PatchStateMovingPost));
            harmony.PatchAll(typeof(PatchMissile));

        }

        private static int CompareMissileTargets(FPBaseEnemy enemy1, FPBaseEnemy enemy2)
        {
            if (ReferenceEquals(enemy1, enemy2))
            {
                return 0;
            }
            if (enemy1 == null)
            {
                return 1;
            }
            if (enemy2 == null)
            {
                return -1;
            }
            float num = Vector2.SqrMagnitude(position - enemy1.position);
            float num2 = Vector2.SqrMagnitude(position - enemy2.position);
            if (num < num2)
            {
                return -1;
            }
            if (num > num2)
            {
                return 1;
            }
            if (enemy1.stageListPos < enemy2.stageListPos)
            {
                return -1;
            }
            if (enemy1.stageListPos > enemy2.stageListPos)
            {
                return 1;
            }
            return 0;
        }

        private static List<FPBaseEnemy> GetEnemyListInMissileRange()
        {
            List<FPBaseEnemy> list = new List<FPBaseEnemy>();
            float num = missileRange * missileRange;
            foreach (FPBaseEnemy fpbaseEnemy in FPStage.GetActiveEnemies(false, false))
            {
                if (fpbaseEnemy.gameObject.GetComponentsInParent<PlayerShip>().Length == 0 && fpbaseEnemy.health > 0f && fpbaseEnemy.CanBeTargeted() && fpbaseEnemy != weakPoint && (targetPlayer == null || (targetPlayer != null && fpbaseEnemy.faction != targetPlayer.faction)) && Vector2.SqrMagnitude(position - fpbaseEnemy.position) <= num)
                {
                    list.Add(fpbaseEnemy);
                }
            }
            return list;
        }

        private static Vector2 GetTargetOffset(FPBaseEnemy enemy)
        {
            FPHitBox hbWeakpoint = enemy.hbWeakpoint;
            if (enemy.GetComponent<MonsterCube>() != null)
            {
                hbWeakpoint = enemy.GetComponent<MonsterCube>().childWeakpoint.hbWeakpoint;
            }
            float x = UnityEngine.Random.Range(hbWeakpoint.left, hbWeakpoint.right);
            float y = UnityEngine.Random.Range(hbWeakpoint.bottom, hbWeakpoint.top);
            return new Vector2(x, y);
        }

        private static void UpdateMissileTargetedEnemies()
        {
            List<FPBaseEnemy> enemyListInMissileRange = GetEnemyListInMissileRange();
            enemyListInMissileRange.Sort(new Comparison<FPBaseEnemy>(CompareMissileTargets));
            int i = 0;
            while (i < enemyListInMissileRange.Count - 1)
            {
                if (ReferenceEquals(enemyListInMissileRange[i], enemyListInMissileRange[i + 1]))
                {
                    enemyListInMissileRange.RemoveAt(i + 1);
                }
                else
                {
                    i++;
                }
            }
            missileTargetedEnemies = new List<FPBaseEnemy>(Mathf.Min(enemyListInMissileRange.Count, missileCount));
            i = 0;
            while (i < enemyListInMissileRange.Count && missileTargetedEnemies.Count < missileCount)
            {
                if (!(enemyListInMissileRange[i] == null))
                {
                    if (!missileTargetedEnemies.Contains(enemyListInMissileRange[i]))
                    {
                        missileTargetedEnemies.Add(enemyListInMissileRange[i]);
                    }
                }
                i++;
            }
        }

        private static void OnTargetingReticleExpired(TargetingReticle reticle)
        {
            reticle.TargetingReticleExpired -= OnTargetingReticleExpired;
            if (reticles != null)
            {
                reticles.Remove(reticle);
            }
        }

        private static void OnTargetingCircleExpired(TargetingCircle circle)
        {
            circle.TargetingCircleExpired -= OnTargetingCircleExpired;
            if (currentTargetingCircle == circle)
            {
                currentTargetingCircle = null;
            }
        }

        public static void Action_SpawnMissiles(Vector2 vector)
        {
            //UpdateMissileTargetedEnemies();
            List<FPBaseEnemy> list = missileTargetedEnemies;
            if (currentTargetingCircle != null)
            {
                currentTargetingCircle.targets = missileTargetedEnemies;
                currentTargetingCircle.MarkTheTargets(true);
                foreach (TargetingReticle targetingReticle in currentTargetingCircle.reticles)
                {
                    if (!reticles.Contains(targetingReticle))
                    {
                        reticles.Add(targetingReticle);
                        targetingReticle.TargetingReticleExpired += OnTargetingReticleExpired;
                    }
                }
            }
            Vector3 localScale = baseTransform.GetChild(0).transform.localScale;
            int num = 0;
            FPAudio.PlaySfx(sfxMissile);
            for (int i = 0; i < missileCount; i++)
            {
                BFFMicroMissile bffmicroMissile = (BFFMicroMissile)FPStage.CreateStageObject(MissileID, vector.x, vector.y);
                if (currentTargetingCircle != null && i < currentTargetingCircle.reticles.Count)
                {
                    currentTargetingCircle.reticles[i].AddTargetedMissile(bffmicroMissile);
                    currentTargetingCircle.reticles[i].destroyOnMissileImpact = true;
                    currentTargetingCircle.reticles[i].persist = false;
                }
                bffmicroMissile.transform.rotation = Quaternion.Euler(0f, 0f, (float)(UnityEngine.Random.Range(-45, 45) + ((localScale.x < 0f) ? 180 : 0)));
                if (list.Count > 0)
                {
                    bffmicroMissile.AssignTarget(list[num], GetTargetOffset(list[num]));
                    num++;
                    if (num >= list.Count)
                    {
                        num = 0;
                    }
                }
                bffmicroMissile.attackPower = missileDamage;
                bffmicroMissile.faction = weakPoint.faction;
            }
            if (currentTargetingCircle != null)
            {
                currentTargetingCircle.FreeThisCircle();
            }
        }

        public static void Action_TargetingWave()
        {
            if (currentTargetingCircle != null)
            {
                currentTargetingCircle.FreeThisCircle();
            }
            currentTargetingCircle = (TargetingCircle)FPStage.CreateStageObject(CircleID, weakPoint.position.x, weakPoint.position.y);
            currentTargetingCircle.parentObject = weakPoint;
            currentTargetingCircle.activationMode = FPActivationMode.ALWAYS_ACTIVE;
            currentTargetingCircle.transform.position = new Vector3(currentTargetingCircle.transform.position.x, currentTargetingCircle.transform.position.y, 12f);
            currentTargetingCircle.active = true;
            currentTargetingCircle.parentsOtherReticles = reticles;
            UpdateMissileTargetedEnemies();
            currentTargetingCircle.targets = missileTargetedEnemies;
            currentTargetingCircle.TargetingCircleExpired += OnTargetingCircleExpired;
            FPAudio.PlaySfx(sfxMissileTargeting);
        }

    }
    class PatchStateStart
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerShip), "Start", MethodType.Normal)]
        static void Postfix(PlayerShip __instance)
        {
            UnityEngine.Object[] assets = Plugin.moddedBundle.LoadAllAssets();

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset.GetType() == typeof(GameObject))
                {
                    GameObject gameObject = (GameObject)GameObject.Instantiate(asset);
                    if (gameObject.GetComponent<BFFMicroMissile>() != null)
                    {
                        Plugin.microMissile = gameObject.GetComponent<BFFMicroMissile>();
                    }
                    if (gameObject.GetComponent<TargetingCircle>() != null)
                    {
                        Plugin.targetingCircle = gameObject.GetComponent<TargetingCircle>();
                    }
                    if (gameObject.GetComponent<TargetingReticle>() != null)
                    {
                        Plugin.targetingReticle = gameObject.GetComponent<TargetingReticle>();
                    }
                }
            }

            Plugin.CircleID = FPStage.RegisterObjectType(Plugin.targetingCircle, typeof(TargetingCircle), 20);
            Plugin.MissileID = FPStage.RegisterObjectType(Plugin.microMissile, typeof(BFFMicroMissile), 20);
            Plugin.ReticleID = FPStage.RegisterObjectType(Plugin.targetingReticle, typeof(TargetingReticle), 20);

            Plugin.position = __instance.position;
            Plugin.weakPoint = __instance.weakPoint;
            Plugin.baseTransform = __instance.gameObject.transform;

            Plugin.reticles =  new List<TargetingReticle>(Plugin.missileCount);

        }
    }

    class PatchMissile
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BFFMicroMissile), "State_Done", MethodType.Normal)]
        static void Postfix(BFFMicroMissile __instance)
        {
            __instance.activationMode = FPActivationMode.NEVER_ACTIVE;
            __instance.gameObject.SetActive(false);

        }
    }

    class PatchStateMovingPre
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerShip), "State_Moving", MethodType.Normal)]
        static void Prefix(PlayerShip __instance)
        {
            Plugin.position = __instance.position;
            if (__instance.targetPlayer != null)
            {
                Plugin.position = __instance.position;
                Plugin.weakPoint = __instance.weakPoint;
                Plugin.targetPlayer = __instance.targetPlayer;
                __instance.targetPlayer.energyRecoverRate = 0.2f;
            }
        }
    }


    class PatchStateMovingPost
    {
        static bool targetting;
        static bool cooldown;
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerShip), "State_Moving", MethodType.Normal)]
        static void Prefix(PlayerShip __instance)
        {
            if (__instance.targetPlayer != null)
            {
                if (__instance.targetPlayer.input.specialHold && !targetting && __instance.targetPlayer.energy > 10f)
                {
                    Plugin.Action_TargetingWave();
                    __instance.targetPlayer.energy -= 10f;
                    targetting = true;
                }

                if (!__instance.targetPlayer.input.specialHold && targetting)
                {
                    Plugin.Action_SpawnMissiles(__instance.position);
                    targetting = false;
                }

                if (__instance.targetPlayer.input.attackHold && __instance.targetPlayer.energy > 0.2f && !cooldown)
                {
                    __instance.weakPoint.invincibility = 10f;
                    __instance.weakPoint.health = __instance.weakPoint.initialHealth;
                    __instance.targetPlayer.energy -= 1f;
                    __instance.moveSpeed = 400f;
                }

                if (!__instance.targetPlayer.input.attackHold)
                {
                    __instance.moveSpeed = 250f;
                }

                if (__instance.targetPlayer.energy < 1f && !cooldown)
                {
                    cooldown = true;
                }
                if (cooldown && __instance.targetPlayer.energy >= 40)
                {
                    cooldown = false;
                }

            }

        }
    }
}
