namespace Echo.Game.Audio
{
    /// <summary>A short pulse on death only (tech spec 11). The caller checks the haptics setting.</summary>
    public static class Haptics
    {
        public static void Pulse()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            UnityEngine.Handheld.Vibrate();
#endif
        }
    }
}
