using System.Runtime.Serialization;
using System;

namespace ChromeDevToolsProtocol.SourceGenerator
{
    [DataContract]
    public class GFieldInfo : GBaseTypeInfo
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "optional")]
        public bool Optional { get; set; }

        [DataMember(Name = "experimental")]
        public bool Experimental { get; set; }

        [DataMember(Name = "deprecated")]
        public bool Deprecated { get; set; }
    }

    [DataContract]
    public class GBaseTypeInfo
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "$ref")]
        public string Ref { get; set; }

        [DataMember(Name = "items")]
        public GBaseTypeInfo Items { get; set; }

        [DataMember(Name = "enum")]
        public string[] Enum { get; set; }

        public bool IsBasicType()
        {
            switch (Type)
            {
                case "string": return true;
                case "integer": return true;
                case "number": return true;
                case "boolean": return true;
                case "any": return true;
                case "array": return true;
                case "object": return false;
            }

            return false;
        }
    }
}