using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public static class PlatformUtils
    {
        private static bool? _isPlatformSupported;

        public static bool IsPlatformSupported()
        {
            if (_isPlatformSupported is not null)
                return _isPlatformSupported.Value;

            if (
                RuntimeInformation.ProcessArchitecture is not Architecture.Arm and not Architecture.Arm64 ||
                typeof(Harmony).Assembly.GetName().Version is { Major: >= 2, Minor: >= 4 }
            )
                return (_isPlatformSupported = true).Value;

            if (SessionState.GetBool("EasyLogin::ArmPlatformCheck::DisplayedWarningMessage", false))
                return (_isPlatformSupported = false).Value;
            
            SessionState.SetBool("EasyLogin::ArmPlatformCheck::DisplayedWarningMessage", true);
            EditorApplication.delayCall += () => EditorUtility.DisplayDialog(
                "Unsupported platform",
                "You are attempting to use the ARM version of Unity.\n\n" +
                "Due to the currently installed version of the Harmony library not supporting this yet, neither Easy Login nor many other packages will work properly.\n\n" +
                "Try using the non-ARM version of Unity to possibly circumvent this issue, or replacing the Harmony version VRChat is providing with your own and restarting Unity.\n\n" +
                $"Installed: {typeof(Harmony).Assembly.GetName().Version}\nMinimum: 2.4.0.0",
                ok: "Close"
            );
            return (_isPlatformSupported = false).Value;
        }
    }
}