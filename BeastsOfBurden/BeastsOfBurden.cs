using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System;



namespace BeastsOfBurden
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class BeastsOfBurden : BaseUnityPlugin
    {
        const string pluginGUID = "org.bepinex.plugins.beasts_of_burden";
        const string pluginName = "BeastsOfBurden";
        public const string pluginVersion = "1.0.5";
        public static ManualLogSource logger;

        private readonly Harmony harmony = new Harmony(pluginGUID);

        private delegate void VagonAttachToDelegate(Vagon instance, GameObject target);
        private delegate void VagonDetachDelegate(Vagon instance);
        private delegate bool BaseAIMoveToDelegate(BaseAI instance, float dt, Vector3 target, float stopDistance, bool run);

        private static readonly AccessTools.FieldRef<Character, ZNetView> CharacterNView =
            AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<Vagon, ConfigurableJoint> VagonAttachJoin =
            AccessTools.FieldRefAccess<Vagon, ConfigurableJoint>("m_attachJoin");

        private static readonly VagonAttachToDelegate VagonAttachTo =
            AccessTools.MethodDelegate<VagonAttachToDelegate>(
                AccessTools.Method(typeof(Vagon), "AttachTo", new[] { typeof(GameObject) }), null, false);
        private static readonly VagonDetachDelegate VagonDetach =
            AccessTools.MethodDelegate<VagonDetachDelegate>(
                AccessTools.Method(typeof(Vagon), "Detach"), null, false);
        private static readonly BaseAIMoveToDelegate BaseAIMoveTo =
            AccessTools.MethodDelegate<BaseAIMoveToDelegate>(
                AccessTools.Method(typeof(BaseAI), "MoveTo", new[] { typeof(float), typeof(Vector3), typeof(float), typeof(bool) }), null, false);

        static private ConfigEntry<bool> commandWolf;
        static private ConfigEntry<bool> commandBoar;
        static private ConfigEntry<bool> commandLox;

        static private ConfigEntry<bool> attachToWolf;
        static private ConfigEntry<bool> attachToBoar;
        static private ConfigEntry<bool> attachToLox;
        static private ConfigEntry<bool> attachToOtherTamed;

        static private ConfigEntry<float> detachDistanceFactor;
        static private ConfigEntry<float> detachDistancePlayer;

        static private ConfigEntry<float> followDistanceLox;
        static private ConfigEntry<float> followDistanceMediumAnimal;

        static private ConfigEntry<bool> preventTamedFearOfFire;

        void Awake()
        {
            logger = Logger;
            Logger.LogInfo($"Loading Beasts of Burden ");

            commandWolf = Config.Bind(pluginName,
                nameof(commandWolf),
                true,
                "Makes Wolf Commandable (as it is in the normal game)");
            commandBoar = Config.Bind(pluginName,
                nameof(commandBoar),
                true,
                "Makes Boar Commandable");
            commandLox = Config.Bind(pluginName,
                 nameof(commandLox),
                 true,
                 "Makes Lox Commandable");

            attachToWolf = Config.Bind(pluginName,
                nameof(attachToWolf),
                true,
                "Allow cart to attach to Wolf");

            attachToBoar = Config.Bind(pluginName,
                nameof(attachToBoar),
                true,
                "Allow cart to attach to Boar");

            attachToLox = Config.Bind(pluginName,
                 nameof(attachToLox),
                 true,
                 "Allow cart to attach to Lox");

            attachToOtherTamed = Config.Bind(pluginName,
                 nameof(attachToOtherTamed),
                 true,
                 "Experimental: Allow cart to attach to other types of tamed animals. ");

            detachDistancePlayer = Config.Bind(pluginName, 
                nameof(detachDistancePlayer),
                2f, 
                new ConfigDescription("How far the player has to be from the cart.",
                    new AcceptableValueRange<float>(1f, 5f)));

            detachDistanceFactor = Config.Bind(pluginName,
                nameof(detachDistanceFactor),
                3.5f,
                new ConfigDescription("How far something has to be from the cart to use it a multiple of their radius",

                    new AcceptableValueRange<float>(1f, 5f)));
            followDistanceLox = Config.Bind(pluginName, nameof(followDistanceLox),
                8f, new ConfigDescription("How close the lox will follow behind the player.",
                new AcceptableValueRange<float>(1f, 30f)));

            followDistanceMediumAnimal = Config.Bind(pluginName, nameof(followDistanceMediumAnimal),
                3f, new ConfigDescription("How close medium animals (wolf and boar) will follow behind the player.",
                new AcceptableValueRange<float>(1f, 30f)));

            preventTamedFearOfFire = Config.Bind<bool>(pluginName, nameof(preventTamedFearOfFire),
                false, new ConfigDescription("Prevent tamed animals from being afraid of fire."));

            harmony.PatchAll();
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        /// <summary>
        /// An enum describing what is getting attached to the cart
        /// </summary>
        public enum Beasts
        {
            lox,
            wolf,
            boar,
            player,
            other
        }


        /// <summary>
        /// Parses a character into a Beasts
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
            public static Beasts ParseCharacterType(Character c)
        {
            if (c.IsPlayer()) return Beasts.player;
            ZNetView nview = CharacterNView(c);
            if (nview != null && nview.IsValid())
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(nview.GetZDO().GetPrefab());
                switch (prefab != null ? prefab.name : string.Empty)
                {
                    case "Lox": return Beasts.lox;
                    case "Wolf": return Beasts.wolf;
                    case "Boar": return Beasts.boar;
                    default: return Beasts.other;
                }
            }
            logger.LogDebug("Character has invalid m_nview.");
            return Beasts.other;
        }


        /// <summary>
        /// Different sized characters require different attachment offsets for the cart. 
        /// This will return the appropriate offset.
        /// </summary>
        /// <param name="c">to be attached to the cart</param>
        /// <returns>vector of where the cart should attach</returns>
        public static Vector3 GetCartOffsetVectorForCharacter(Character c)
        {
            if (c)
            {
                return new Vector3(0f, 0.8f, 0f - c.GetRadius());
            }
            return new Vector3(0f, 0.8f, 0f);
        }

        /// <summary>
        /// Allows the types of animals attached to to be configurable
        /// </summary>
        /// <param name="c"></param>
        /// <returns>if cart can be attached to character type</returns>
        public static bool IsAttachableCharacter(Character c)
        {
            switch (ParseCharacterType(c))
            {
                case Beasts.lox:
                    return attachToLox.Value;
                case Beasts.wolf:
                    return attachToWolf.Value;
                case Beasts.boar:
                    return attachToBoar.Value;
                case Beasts.player:
                    return true;
                default:
                    return attachToOtherTamed.Value;
            }
        }

        /// <summary>
        /// Different character types should be different distances to the cart for it to attach.
        /// </summary>
        /// <param name="c"></param>
        /// <returns>the appropriate attach/detach distance for the provided character</returns>
        public static float GetCartDetachDistance(Character c)
        {
            if (c)
            {
                if (c.IsPlayer())
                {
                    return detachDistancePlayer.Value;
                }
                else
                {
                    return c.GetRadius() * detachDistanceFactor.Value;
                }
            
            }
            logger.LogError("Character pass was null");
            return 0f;
        }

        /// <summary>
        /// Searches nearby animals and finds the closest one to the cart that could be attached.
        /// </summary>
        /// <param name="cart"></param>
        /// <returns>Closest character to the cart that can attach to it, null if no character available</returns>
        static Character FindClosestAttachableAnimal(Vagon cart)
        {
            if (!cart)
            {
                logger.LogError("Cart pointer is null");
                return null;
            }

            Transform attachPoint = cart.m_attachPoint;
            Character closest_animal = null;
            float closest_distance = float.MaxValue;

            if (!cart.m_attachPoint)
            {
                logger.LogError("cart.m_attachPoint is null.");
                return null;
            }

            foreach (Character currentCharacter in Character.GetAllCharacters())
            {
                if(currentCharacter)
                {
                    if (!currentCharacter.IsPlayer() && currentCharacter.IsTamed() && IsAttachableCharacter(currentCharacter))
                    {
                        Vector3 cartOffset = GetCartOffsetVectorForCharacter(currentCharacter);
                        Vector3 animalPosition = currentCharacter.transform.position;

                        float distance = Vector3.Distance(animalPosition + cartOffset, attachPoint.position);
                        float detachDistance = GetCartDetachDistance(currentCharacter);
                        if (distance < detachDistance && distance < closest_distance)
                        {
                            closest_animal = currentCharacter;
                            closest_distance = distance;
                        }
                    }
                }
                else
                {
                    logger.LogWarning("null character returned by Character.GetAllCharacter() in method FindClosestTamedAnimal");
                }
            }
            if (closest_animal != null)
            {
                logger.LogDebug($"Closest animal is {closest_animal.m_name} at a distance of {closest_distance}");
            }
            return closest_animal;
        }


        /// <summary>
        /// Helper method to access the character currently attached to a cart
        /// </summary>
        /// <param name="cart"></param>
        /// <returns>Character currently attached</returns>
        static Character AttachedCharacter(Vagon cart)
        {
            if (!cart || !cart.IsAttached()) return null;
            ConfigurableJoint joint = VagonAttachJoin(cart);
            if (joint == null || joint.connectedBody == null) return null;
            return joint.connectedBody.gameObject.GetComponent<Character>();
        }


        /// <summary>
        /// Logs the contents of a given cart to the debug logger. 
        /// Used during debugging to easily differentiate between carts.
        /// </summary>
        /// <param name="cart"></param>
        static void LogCartContents(Vagon cart)
        {
            Container c = cart.m_container;
            logger.LogDebug($"Cart contents:");
            foreach (ItemDrop.ItemData item in c.GetInventory().GetAllItems())
            {
                logger.LogDebug($"\t * {item.m_shared.m_name}");
            }
        }

        /// <summary>
        /// This method is similar to Vagon.AttachTo except we don't call DetachAll as the first operation.
        /// </summary>
        /// <param name="attachTarget"></param>
        /// <param name="cart"></param>
        static void AttachCartTo(Character attachTarget, Vagon cart)
        {
            cart.m_attachOffset = GetCartOffsetVectorForCharacter(attachTarget);
            cart.m_detachDistance = GetCartDetachDistance(attachTarget);
            VagonAttachTo(cart, attachTarget.gameObject);
        }


        /// <summary>
        /// Patch for Vagon.LateUpdate that handles a situation where the attached animal is killed.
        /// </summary>
        [HarmonyPatch(typeof(Vagon), "LateUpdate")]
        class LateUpdate_Vagon_Patch
        {
            static void Prefix(Vagon __instance)
            {
                ConfigurableJoint joint = VagonAttachJoin(__instance);
                if (joint != null && joint.connectedBody == null) VagonDetach(__instance);
            }
        }

        /// <summary>
        /// Patch overriding InUse that will correctly return false if an animal is the one attached to a cart
        /// </summary>
        [HarmonyPatch(typeof(Vagon), nameof(Vagon.InUse))]
        class InUse_Vagon_Patch
        {
            static bool Prefix(ref bool __result, Vagon __instance)
            {
                if (__instance.m_container && __instance.m_container.IsInUse()) __result = true;
                else if (__instance.IsAttached())
                {
                    ConfigurableJoint joint = VagonAttachJoin(__instance);
                    __result = joint != null && joint.connectedBody != null && joint.connectedBody.gameObject.GetComponent<Player>() != null;
                }
                else __result = false;
                return false;
            }
        }

        /// <summary>
        /// Patch to FixedUpdate that will attempt to attach cart to animal if there is an appropriate one nearby.
        /// </summary>
        [HarmonyPatch(typeof(Vagon), "CanAttach")]
        class Vagon_CanAttach_Patch
        {
            static bool Prefix(Vagon __instance, GameObject go, ref bool __result)
            {
                Character character = go != null ? go.GetComponent<Character>() : null;
                if (character == null || character.IsPlayer() || !character.IsTamed() || !IsAttachableCharacter(character)) return true;
                if (__instance.transform.up.y < 0.1f || character.InDodge() || character.IsTeleporting())
                {
                    __result = false; return false;
                }
                __instance.m_attachOffset = GetCartOffsetVectorForCharacter(character);
                __instance.m_detachDistance = GetCartDetachDistance(character);
                __result = Vector3.Distance(go.transform.position + __instance.m_attachOffset, __instance.m_attachPoint.position) < __instance.m_detachDistance;
                return false;
            }
        }

        [HarmonyPatch(typeof(Vagon), "Update")]
        class Vagon_Update_Patch
        {
            static void Prefix(Vagon __instance, ref Humanoid ___m_useRequester)
            {
                if (!__instance.IsAttached() && ___m_useRequester)
                {
                    Character closestTamed = FindClosestAttachableAnimal(__instance);
                    if (closestTamed != null)
                    {
                        AttachCartTo(closestTamed, __instance);
                        ___m_useRequester = null;
                    }
                }
                Character attached = AttachedCharacter(__instance);
                if (attached != null && !attached.IsPlayer()) __instance.m_detachDistance = GetCartDetachDistance(attached);
            }
        }

        /// <summary>
        /// Changes to BaseAI UpdateAI patch.
        /// 
        /// Some of these could potentially be moved into a BaseAI.Awake but for
        /// now I'm putting them here so that if you change the value while playing
        /// it'll be automatically updated.
        /// </summary>
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
        class BaseAI_UpdateAI_patch
        {
            static bool Prefix(ref BaseAI __instance, Character ___m_character)
            {
                if (___m_character != null && ___m_character.IsTamed() && preventTamedFearOfFire.Value) __instance.m_afraidOfFire = false;
                return true;
            }
        }

            /// <summary>
            /// Patch for follow logic that allows for a greater follow distance.
            /// This is necessary because the lox tries to follow the player so closely that it constantly pushes the player
            /// Future use could include randomizing follow distance so multiple cart pulling animals are less likely to collide.
            /// </summary>
            [HarmonyPatch(typeof(BaseAI), "Follow")]
        class Tamed_Follow_patch
        {
            static bool Prefix(GameObject go, float dt, ref BaseAI __instance, Character ___m_character)
            {
                if (___m_character == null || !___m_character.IsTamed()) return true;
                MonsterAI monster = __instance as MonsterAI;
                GameObject followTarget = monster != null ? monster.GetFollowTarget() : null;
                if (followTarget == null || followTarget.GetComponent<Player>() == null) return true;
                float distance = Vector3.Distance(go.transform.position, __instance.transform.position);
                float followDistance;
                switch (ParseCharacterType(___m_character))
                {
                    case Beasts.lox: followDistance = followDistanceLox.Value; break;
                    case Beasts.wolf:
                    case Beasts.boar: followDistance = followDistanceMediumAnimal.Value; break;
                    default: return true;
                }
                bool run = distance > followDistance * 3;
                if (distance < followDistance) __instance.StopMoving();
                else BaseAIMoveTo(__instance, dt, go.transform.position, 0f, run);
                return false;
            }
        }

        /// <summary>
        /// Patch that allows this mod to specify which tamed animals are commandable.
        /// </summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        class Command_Patch
        {
            /// <summary>
            /// Sets m_commandable to true for Tameable animals allowing commands to be issued to Boar, Wolf, and Lox
            /// </summary>
            /// <param name="___m_commandable"></param>
            static void Prefix(ref bool ___m_commandable, ref Character ___m_character)
            {
                switch (ParseCharacterType(___m_character))
                {
                    case Beasts.lox:
                        ___m_commandable = commandLox.Value;
                        return;
                    case Beasts.wolf:
                        ___m_commandable = commandWolf.Value;
                        return;
                    case Beasts.boar:
                        ___m_commandable = commandBoar.Value;
                        return;
                    default:
                        return;
                }
            }
        }
    }
}
