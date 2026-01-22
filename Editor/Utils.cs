using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

namespace Foxscore.EasyLogin
{
    public static class Utils
    {
        public static string GetUserAgentValue()
        {
            var version = GetPackageJson().VersionString;
            return $"EasyLogin/{version} (Unity Editor, {Environment.OSVersion.VersionString})";
        }
        
        public static void SetEasyLoginUserAgent(this HttpClient client)
        {
            client.DefaultRequestHeaders.Remove("User-Agent");
            client.DefaultRequestHeaders.Add("User-Agent", GetUserAgentValue());
        }

        [CanBeNull] private static Abstract.PackageJson _packageJsonCache;
        public static Abstract.PackageJson GetPackageJson()
        {
            if (_packageJsonCache == null)
            {
                var rawPackageJson =File.ReadAllText(Path.Combine(Application.dataPath, "..", "Packages", "dev.foxscore.easy-login", "package.json"));
                _packageJsonCache = JsonConvert.DeserializeObject<Abstract.PackageJson>(rawPackageJson);
            }
            return _packageJsonCache;
        }

        // ReSharper disable once InconsistentNaming
        public enum VRCSdkPanelTab
        {
            Account,
            Builder,
            ContentManager,
            Settings
        }
        
        private static MethodInfo _selectTabMethod;
        private static Dictionary<VRCSdkPanelTab, object> _panelTabEnumMap = new();
        public static void SelectControlPanelTab(VRCSdkPanelTab tab)
        {
            if (_selectTabMethod == null)
            {
                _selectTabMethod = AccessTools.Method(typeof(VRCSdkControlPanel), "SelectTab");
                var originalEnum = AccessTools.TypeByName("VRCSdkControlPanel+PanelTab");
                Assert.IsNotNull(originalEnum, "VRCSdkControlPanel.PanelTab not found");
                var values = Enum.GetValues(originalEnum);
                Assert.IsTrue(values.Length == Enum.GetNames(typeof(VRCSdkPanelTab)).Length, "values.Length == Enum.GetNames(typeof(VRCSdkPanelTab)).Length");
                foreach (var value in values)
                {
                    Assert.IsNotNull(value, "VRCSdkControlPanel.PanelTab value not found");
                    var mapKey = (VRCSdkPanelTab)value;
                    Assert.IsNotNull<object>(mapKey, "VRCSdkControlPanel.PanelTab map key not found");
                    _panelTabEnumMap[(VRCSdkPanelTab)value] = value;
                }
            }
            
            var mappedEnumValue = _panelTabEnumMap[tab];
            _selectTabMethod.Invoke(VRCSdkControlPanel.window, new[] { mappedEnumValue });
        }
    }
}
