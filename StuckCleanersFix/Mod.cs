using MelonLoader;
using MelonLoader.Utils;
using System.Reflection;



[assembly: MelonInfo(typeof(StuckCleanersFix.StuckCleanersFixMod), "StuckCleanersFix", "1.0.0", "lasersquid", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace StuckCleanersFix
{
    public class StuckCleanersFixMod : MelonMod
    {
        public HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("com.lasersquid.stuckcleanersfix");

        public override void OnInitializeMelon()
        {
            SetMod();
            LoggerInstance.Msg("Initialized.");
        }

        private List<Type> GetPatchTypes()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.Name.EndsWith("Patches"))
                .ToList<Type>();
        }

        private void SetMod()
        {
            foreach (var t in GetPatchTypes())
            {
                MethodInfo method = t.GetMethod("SetMod", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                method.Invoke(null, [this]);
            }
        }

        public void RestoreDefaults()
        {
            foreach (var t in GetPatchTypes())
            {
                try
                {
                    MethodInfo method = t.GetMethod("RestoreDefaults", BindingFlags.Public | BindingFlags.Static);
                    method.Invoke(null, null);
                }
                catch (Exception e)
                {
                    LoggerInstance.Warning($"Couldn't restore defaults for class {t.Name}: {e.GetType().Name} - {e.Message}");
                    LoggerInstance.Warning($"Source: {e.Source}");
                    LoggerInstance.Warning($"{e.StackTrace}");
                }
            }
        }
    }
}