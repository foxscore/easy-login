using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Foxscore.EasyLogin.SemanticVersioning;

namespace Foxscore.EasyLogin
{
    public static class Abstract
    {
        public class PackageJson
        {
            [JsonProperty("version")]
            public string VersionString { get; set; }
            
            [JsonProperty("url")]
            public string ZipDownloadUrl { get; set; }
            
            [JsonProperty("zipSHA256")]
            public string ZipSHA256 { get; set; }
            
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

        public class VersionOnlyConfig
        {
            [JsonProperty("version")]
            public int Version = 0;
        }
    }
}