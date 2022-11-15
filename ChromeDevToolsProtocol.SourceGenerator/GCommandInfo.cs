using System.Runtime.Serialization;

namespace ChromeDevToolsProtocol.SourceGenerator
{
    [DataContract]
    public class GCommandInfo
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "experimental")]
        public bool Experimental { get; set; }

        [DataMember(Name = "deprecated")]
        public bool Deprecated { get; set; }

        [DataMember(Name = "parameters")]
        public GFieldInfo[] Parameters { get; set; }

        [DataMember(Name = "returns")]
        public GFieldInfo[] Returns { get; set; }
    }

}
