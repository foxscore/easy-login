using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Version = SemanticVersioning.Version;

namespace Foxscore.EasyLogin
{
    public static class Abstract
    {
        public class PackageJson
        {
            [JsonProperty("version")]
            public string VersionString { get; set; }
            
            public Version GetSemanticVersion() => Version.Parse(VersionString);
        }
        
        public class VccConfig
        {
            [JsonProperty("showPrereleasePackages", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public bool ShowPrereleasePackages { get; set; }
        }

        public class Index
        {
            [JsonProperty("packages")]
            public IndexPackages Packages { get; set; }
        }
        
        public class IndexPackages
        {
            [JsonProperty("dev.foxscore.easy-login")]
            public IndexEasyLoginPackage EasyLogin { get; set; }
        }
        
        public class IndexEasyLoginPackage
        {
            [JsonProperty("versions")]
            public JObject Versions { get; set; }
            
            public string[] GetVersions() => Versions.Properties().Select(p => p.Name).ToArray();
        }
    }
}