using UnityEngine;

namespace ARWalking.UI
{
    public static class UiSafeAreaSimulation
    {
        public static bool Enabled { get; set; }
        public static Rect SimulatedArea { get; set; }

        public static Rect Resolve(Rect deviceSafeArea)
        {
#if UNITY_EDITOR
            return Enabled ? SimulatedArea : deviceSafeArea;
#else
            return deviceSafeArea;
#endif
        }
    }
}
