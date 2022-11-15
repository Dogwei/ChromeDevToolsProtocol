using System.Runtime.Serialization;

namespace ChromeDevToolsProtocol.SourceGenerator
{
    [DataContract]
    public class GDomainInfo
    {
        [DataMember(Name = "domain")]
        public string Domain { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "experimental")]
        public bool Experimental { get; set; }

        [DataMember(Name = "deprecated")]
        public bool Deprecated { get; set; }

        [DataMember(Name = "commands")]
        public GCommandInfo[] Commands { get; set; }

        [DataMember(Name = "events")]
        public GCommandInfo[] Events { get; set; }

        [DataMember(Name = "types")]
        public GTypeInfo[] Types { get; set; }
    }

    [DataContract]
    public class GVersionInfo
    {
        [DataMember(Name = "version")]
        public GVersionVersionInfo Version { get; set; }

        [DataMember(Name = "domains")]
        public GDomainInfo[] Domains { get; set; }
    }

    [DataContract]
    public class GVersionVersionInfo
    {
        [DataMember(Name = "major")]
        public string Major { get; set; }

        [DataMember(Name = "minor")]
        public string Minor { get; set; }
    }
}