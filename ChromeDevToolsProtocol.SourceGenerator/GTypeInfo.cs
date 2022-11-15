using System.Runtime.Serialization;

namespace ChromeDevToolsProtocol.SourceGenerator
{
    [DataContract]
    public class GTypeInfo : GBaseTypeInfo
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "experimental")]
        public bool Experimental { get; set; }

        [DataMember(Name = "deprecated")]
        public bool Deprecated { get; set; }

        [DataMember(Name = "properties")]
        public GFieldInfo[] Properties { get; set; }
    }

}
