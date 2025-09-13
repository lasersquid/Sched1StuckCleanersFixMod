using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

#if MONO_BUILD
using FishNet;
using ScheduleOne.DevUtilities;
using ScheduleOne.EntityFramework;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.Behaviour;
using ScheduleOne.ObjectScripts;
#else
using Il2CppFishNet;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
#endif

namespace StuckCleanersFix
{
    public class Sched1PatchesBase
    {
        protected static StuckCleanersFixMod Mod;

        public static object GetField(Type type, string fieldName, object target)
        {
#if MONO_BUILD
            return AccessTools.Field(type, fieldName).GetValue(target);
#else
            return AccessTools.Property(type, fieldName).GetValue(target);
#endif
        }

        public static void SetField(Type type, string fieldName, object target, object value)
        {
#if MONO_BUILD
            AccessTools.Field(type, fieldName).SetValue(target, value);
#else
            AccessTools.Property(type, fieldName).SetValue(target, value);
#endif
        }

        public static object GetProperty(Type type, string fieldName, object target)
        {
            return AccessTools.Property(type, fieldName).GetValue(target);
        }

        public static void SetProperty(Type type, string fieldName, object target, object value)
        {
            AccessTools.Property(type, fieldName).SetValue(target, value);
        }

        public static object CallMethod(Type type, string methodName, object target, object[] args)
        {
            return AccessTools.Method(type, methodName).Invoke(target, args);
        }

        public static void SetMod(StuckCleanersFixMod mod)
        {
            Mod = mod;
        }

        public static T CastTo<T>(object o) where T : class
        {
            if (o is T)
            {
                return (T)o;
            }
            else
            {
                return null;
            }
        }

        public static bool Is<T>(object o)
        {
            return o is T;
        }

#if !MONO_BUILD
        public static T CastTo<T>(Il2CppSystem.Object o) where T : Il2CppObjectBase
        { 
            return o.TryCast<T>();
        }

        public static bool Is<T>(Il2CppSystem.Object o) where T : Il2CppObjectBase
        {
            return o.TryCast<T>() != null;
        }
#endif

        public static UnityAction ToUnityAction(Action action)
        {
#if MONO_BUILD
            return new UnityAction(action);
#else
            return DelegateSupport.ConvertDelegate<UnityAction>(action);
#endif
        }

        public static UnityAction<T> ToUnityAction<T>(Action<T> action)
        {
#if MONO_BUILD
            return new UnityAction<T>(action);
#else
            return DelegateSupport.ConvertDelegate<UnityAction<T>>(action);
#endif
        }

        public static void Log(string message)
        {
            Mod.LoggerInstance.Msg(message);
        }

        public static void Warn(string message)
        {
            Mod.LoggerInstance.Warning(message);
        }

        public static void RestoreDefaults()
        {
            throw new NotImplementedException();
        }
    }



    [HarmonyPatch]
    public class TrashBehaviourPatches : Sched1PatchesBase
    {
        // Sometimes TargetBag gets destroyed before it gets properly thrown in the bin.
        // Check that TargetBag is not null and not destroyed before accessing its members.
        [HarmonyPatch(typeof(DisposeTrashBagBehaviour), "IsAtDestination")]
        [HarmonyPrefix]
        public static bool IsAtDestinationPrefix(DisposeTrashBagBehaviour __instance, ref bool __result)
        {
            if (__instance.heldTrash == null && __instance.TargetBag != null)
            {
                __result = Vector3.Distance(__instance.Npc.transform.position, __instance.TargetBag.transform.position) <= 2f;
                return false;
            }
            __result = Vector3.Distance(__instance.Npc.transform.position, __instance.Cleaner.AssignedProperty.DisposalArea.StandPoint.position) <= 2f;
            return false;
        }

        // A cleanup routine runs periodically that takes care of destroyed TrashItems, but it doesn't
        // check for destroyed trash bags. Add a check to the cleanup routine.
        [HarmonyPatch(typeof(TrashContainerItem), "CheckTrashItems")]
        [HarmonyPrefix]
        public static void ChecKTrashItemsPrefix(TrashContainerItem __instance, ref bool __result)
        {
            for (int i = 0; i < __instance.TrashBagsInRadius.Count; i++)
            {
                if (__instance.TrashBagsInRadius[i] == null && __instance.TrashBagsInRadius[i] is not null)
                {
                    __instance.RemoveTrashBagFromRadius(__instance.TrashBagsInRadius[i]);
                    i--;
                }
            }
        }

        // For EmptyTrashGrabberBehaviour and BagTrashCanBehaviour, AreActionConditionsMet checks for
        // proximity of 2m, whereas NavMeshUtility.GetAccessPoint assumes proximity of 1m.
        // This means cleaners get stuck sometimes. Let's fix that.
        [HarmonyPatch(typeof(EmptyTrashGrabberBehaviour), "GoToTarget")]
        [HarmonyPrefix]
        public static bool TrashGrabberGoToTargetPrefix(EmptyTrashGrabberBehaviour __instance)
        {
            if (!__instance.AreActionConditionsMet(false))
            {
                __instance.Disable_Networked(null);
                return false;
            }
            Transform accessPoint = GetAccessPointWithProximity(__instance.TargetTrashCan, __instance.Npc, EmptyTrashGrabberBehaviour.ACTION_MAX_DISTANCE);
            if (accessPoint == null)
            {
                __instance.Disable_Networked(null);
                return false;
            }
            __instance.SetDestination(accessPoint.position, true);
            return false;
        }

        // Call to GoToTarget optimized out. Replace with original method body.
        [HarmonyPatch(typeof(EmptyTrashGrabberBehaviour), "ActiveMinPass")]
        [HarmonyPrefix]
        public static bool TrashGrabberActiveMinPassPrefix(EmptyTrashGrabberBehaviour __instance)
        {
            if (!InstanceFinder.IsServer)
            {
                return false;
            }
            if (__instance.Npc.Movement.IsMoving)
            {
                return false;
            }
            if (__instance.actionCoroutine != null)
            {
                return false;
            }
            if (!__instance.AreActionConditionsMet(false))
            {
                __instance.Disable_Networked(null);
                return false;
            }
            if (__instance.IsAtDestination())
            {
                __instance.PerformAction();
                return false;
            }
            __instance.GoToTarget();
            return false;
        }

        // AreActionConditionsMet() checks for proximity of 2m, but GetAccessPoint() checks for proximity of 1m.
        [HarmonyPatch(typeof(BagTrashCanBehaviour), "GoToTarget")]
        [HarmonyPrefix]
        public static bool BagTrashGoToTargetPrefix(BagTrashCanBehaviour __instance)
        {
            if (!__instance.AreActionConditionsMet(false))
            {
                __instance.Disable_Networked(null);
                return false;
            }
            __instance.SetDestination(GetAccessPointWithProximity(__instance.TargetTrashCan, __instance.Npc, BagTrashCanBehaviour.ACTION_MAX_DISTANCE).position, true);
            return false;
        }

        // Call to GoToTarget optimized out. Replace with original method body.
        [HarmonyPatch(typeof(BagTrashCanBehaviour), "ActiveMinPass")]
        [HarmonyPrefix]
        public static bool BagTrashActiveMinPassPrefix(BagTrashCanBehaviour __instance)
        {
            if (!InstanceFinder.IsServer)
            {
                return false;
            }
            if (__instance.Npc.Movement.IsMoving)
            {
                return false;
            }
            if (__instance.actionCoroutine != null)
            {
                return false;
            }
            if (!__instance.AreActionConditionsMet(false))
            {
                __instance.Disable_Networked(null);
                return false;
            }
            if (__instance.IsAtDestination())
            {
                __instance.PerformAction();
                return false;
            }
            __instance.GoToTarget();
            return false;
        }

        // Original NavMeshUtility.GetAccessPoint assumes proximity of 1m.
        // Create replacement that takes variable proximity.
        public static Transform GetAccessPointWithProximity(TrashContainerItem entity, NPC npc, float proximity)
        {
            if (entity == null)
            {
                return null;
            }
            float num = float.MaxValue;
            Transform result = null;
            BuildableItem buildableItem = CastTo<BuildableItem>(entity);
            for (int i = 0; i < entity.AccessPoints.Length; i++)
            {
                NavMeshPath navMeshPath;
                if ((!(buildableItem != null) || buildableItem.ParentProperty.DoBoundsContainPoint(entity.AccessPoints[i].position)) && npc.Movement.CanGetTo(entity.AccessPoints[i].position, proximity, out navMeshPath))
                {
                    float num2 = (navMeshPath != null) ? NavMeshUtility.GetPathLength(navMeshPath) : Vector3.Distance(npc.transform.position, entity.AccessPoints[i].position);
                    if (num2 < num)
                    {
                        num = num2;
                        result = entity.AccessPoints[i];
                    }
                }
            }
            return result;
        }
        public static new void RestoreDefaults()
        {
            // no game objects were changed, so we don't need to do anything
        }
    }
}
