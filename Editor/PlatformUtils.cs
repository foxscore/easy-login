namespace Foxscore.EasyLogin
{
    public static class PlatformUtils
    {
        private static bool? _isPlatformSupported;
        public static bool IsPlatformSupported()
        {
#if !UNITY_EDITOR_OSX
            return true;
#else
            if (_isPlatformSupported is not null)
                return _isPlatformSupported.Value;
            
            if (RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64)
            {
                _isPlatformSupported = false;
                if (SessionState.GetBool("EasyLogin::MacArmPlatformCheck::DisplayedWarningMessage", false) == false)
                {
                    SessionState.SetBool("EasyLogin::MacArmPlatformCheck::DisplayedWarningMessage", true);
                    EditorApplication.delayCall += () => EditorUtility.DisplayDialog(
                        "Unsupported platform",
                        "You are attempting to use the ARM version of Unity on a mac.\n\nDue to the Harmony library not supporting this yet, neither Easy Login nor many other packages will work properly.\n\nTry using the non-ARM version of Unity to possibly circumvent this issue.",
                        ok: "Close"
                    );
                }
            }
            else
            {
                _isPlatformSupported = true;
            }
            
            return _isPlatformSupported.Value;
#endif
        }
    }
}