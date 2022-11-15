using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace ChromeDevToolsProtocol.SourceGenerator
{
    public class SourceGenerator
    {
        const string ExperimentalAttributeName = "Experimental";
        const string DeprecatedAttributeName = "Obsolete";
        const string ProtocolJsFilePath = @"json\browser_protocol.json";
        const string OutputCSFilePath = @"D:\ChromeDevToolsProtocol\ChromeDevToolsProtocol\BaseClient.SourceGenerated.cs";

        readonly string namespaceName;
        readonly string clientClassName;
        readonly string baseDomainClassName;
        readonly string baseMethodParamsInterfaceName;

        readonly GVersionInfo versionInfo;

        public SourceGenerator()
        {
            namespaceName = "ChromeDevToolsProtocol";
            clientClassName = "BaseClient";
            baseDomainClassName = "BaseDomain";
            baseMethodParamsInterfaceName = "IMethodParams";

            using (var fs = new FileStream(ProtocolJsFilePath, FileMode.Open, FileAccess.Read))
            {
                versionInfo = (GVersionInfo)new DataContractJsonSerializer(typeof(GVersionInfo), new DataContractJsonSerializerSettings
                {

                }).ReadObject(fs);
            }
        }

        public void Execute()
        {
            var sb = new SourceBuilder();

            var gcTypeMap = new Dictionary<string, string>();

            foreach (var domainInfo in versionInfo.Domains)
            {
                if (domainInfo.Types != null && domainInfo.Types.Length > 0)
                {
                    foreach (var typeInfo in domainInfo.Types)
                    {
                        if (typeInfo.Enum != null && typeInfo.Enum.Length > 0)
                        {
                            gcTypeMap[$"{domainInfo.Domain}.{typeInfo.Id}"] = $"{domainInfo.Domain}Domain.{typeInfo.Id}";

                            continue;
                        }

                        if (typeInfo.IsBasicType())
                        {
                            gcTypeMap[$"{domainInfo.Domain}.{typeInfo.Id}"] = GetCType(domainInfo, typeInfo);

                            continue;
                        }

                        gcTypeMap[$"{domainInfo.Domain}.{typeInfo.Id}"] = $"{domainInfo.Domain}Domain.{typeInfo.Id}";
                    }
                }
            }

            sb.AppendCode("#pragma warning disable");

            sb.AppendNamespace(namespaceName, () =>
            {
                sb.AppendClass(clientClassName, new string[] { "public", "partial" }, blockCallback: () =>
                {
                    #region DomainFields

                    foreach (var domainInfo in versionInfo.Domains)
                    {
                        sb.AppendField($"{domainInfo.Domain.ToCamelCase()}", $"{domainInfo.Domain}Domain?", new string[] { "private" });
                    }

                    #endregion

                    #region DomainProperties

                    foreach (var domainInfo in versionInfo.Domains)
                    {
                        sb.AppendSummaryAnnotate(domainInfo.Description ?? string.Empty);
                        if (domainInfo.Experimental) sb.AppendAttribute(ExperimentalAttributeName);
                        if (domainInfo.Deprecated) sb.AppendAttribute(DeprecatedAttributeName);
                        sb.AppendGetOnlyProperty(domainInfo.Domain.ToPascalCase(), $"{domainInfo.Domain}Domain", new string[] { "public" }, () =>
                        {
                            sb.AppendCode($"return {domainInfo.Domain.ToCamelCase()} ??= new(this);");
                        });
                    }

                    #endregion

                    #region RaiseEvent Method

                    var raiseEventParameters = new (string, string, string)[]
                    {
                        ("string", "domainName", null),
                        ("string", "eventName", null),
                        ("Span<byte>", "messageBytes", null),
                    };

                    sb.AppendMethod("RaiseEvent", raiseEventParameters, "void", new string[] { "public", "partial" }, () =>
                    {
                        sb.AppendSwitch("domainName", versionInfo.Domains.Select(x => ($"\"{x.Domain}\"", new Action(() =>
                        {
                            sb.AppendCode($"{x.Domain.ToCamelCase()}?.RaiseEvent(eventName, messageBytes);");
                            sb.AppendCode("break;");
                        }))).ToArray(), () =>
                        {
                            sb.AppendCode("RaiseUnknownEvent(domainName, eventName, messageBytes);");
                            sb.AppendCode("break;");
                        });
                    });

                    #endregion
                });

                foreach (var domainInfo in versionInfo.Domains)
                {
                    sb.AppendClass($"{domainInfo.Domain.ToPascalCase()}Domain", new string[] { "public" }, baseDomainClassName, () =>
                    {
                        #region Constructor

                        sb.AppendBasedConstructor($"{domainInfo.Domain.ToPascalCase()}Domain", new[] { (clientClassName, "client") }, new string[] { "public" });

                        #endregion

                        #region DomainName Property

                        sb.AppendGetOnlyProperty("DomainName", "string", new string[] { "public", "override" }, () =>
                        {
                            sb.AppendCode($"return \"{domainInfo.Domain}\";");
                        });

                        #endregion

                        #region Events

                        if (domainInfo.Events != null && domainInfo.Events.Length > 0)
                        {
                            foreach (var eventInfo in domainInfo.Events)
                            {
                                sb.AppendSummaryAnnotate(eventInfo.Description ?? string.Empty);

                                if (eventInfo.Experimental) sb.AppendAttribute(ExperimentalAttributeName);
                                if (eventInfo.Deprecated) sb.AppendAttribute(DeprecatedAttributeName);

                                sb.AppendAutoEvent(eventInfo.Name.ToPascalCase(), $"EventHandler<{eventInfo.Name.ToPascalCase()}Params>?", new string[] { "public" });
                            }
                        }

                        #endregion

                        #region Commands

                        if (domainInfo.Commands != null && domainInfo.Commands.Length > 0)
                        {
                            foreach (var methodInfo in domainInfo.Commands)
                            {
                                var parameterName = $"{methodInfo.Name.ToCamelCase()}Params";
                                var parameterTypeName = $"{methodInfo.Name.ToPascalCase()}Params";
                                var returnTypeName = $"{methodInfo.Name.ToPascalCase()}Result";

                                var parameters = new[]
                                {
                                    (parameterTypeName, parameterName, null),
                                    ("CancellationToken", "cancellationToken", "default")
                                };

                                sb.AppendSummaryAnnotate(methodInfo.Description ?? string.Empty);

                                if (methodInfo.Experimental) sb.AppendAttribute(ExperimentalAttributeName);
                                if (methodInfo.Deprecated) sb.AppendAttribute(DeprecatedAttributeName);

                                sb.AppendMethod($"{methodInfo.Name.ToPascalCase()}Async", parameters, $"ValueTask<{returnTypeName}>", new string[] { "public", "async" }, () =>
                                {
                                    sb.AppendCode($"return await Client.SendRequestMessageAndWaitResponseResult({parameterName}, cancellationToken);");
                                });
                            }
                        }

                        #endregion

                        #region RaiseEvent Method

                        var raiseEventParameters = new (string, string, string)[]
                        {
                            ("string", "eventName", null),
                            ("Span<byte>", "messageBytes", null),
                        };

                        sb.AppendMethod("RaiseEvent", raiseEventParameters, "void", new string[] { "public", "override" }, () =>
                        {
                            if (domainInfo.Events != null && domainInfo.Events.Length > 0)
                            {
                                sb.AppendSwitch("eventName", domainInfo.Events.Select(x => ($"\"{x.Name}\"", new Action(() =>
                                {
                                    sb.AppendCode($"RaiseEvent({x.Name.ToPascalCase()}, messageBytes);");
                                    sb.AppendCode("break;");
                                }))).ToArray(), () =>
                                {
                                    sb.AppendCode("RaiseUnknownEvent(eventName, messageBytes);");
                                    sb.AppendCode("break;");
                                });
                            }
                            else
                            {
                                sb.AppendCode("RaiseUnknownEvent(eventName, messageBytes);");
                            }
                        });

                        #endregion

                        #region Event Params

                        if (domainInfo.Events != null && domainInfo.Events.Length > 0)
                        {
                            foreach (var eventInfo in domainInfo.Events)
                            {
                                sb.AppendClass($"{eventInfo.Name.ToPascalCase()}Params", new string[] { "public" }, null, () =>
                                {
                                    if (eventInfo.Parameters != null && eventInfo.Parameters.Length > 0)
                                    {
                                        eventInfo.Parameters.ToList().ForEach(AppendFieldInfo);
                                    }
                                });
                            }
                        }

                        #endregion

                        #region Command Params And Returns

                        if (domainInfo.Commands != null && domainInfo.Commands.Length > 0)
                        {
                            foreach (var methodInfo in domainInfo.Commands)
                            {
                                var parameterTypeName = $"{methodInfo.Name.ToPascalCase()}Params";
                                var returnTypeName = $"{methodInfo.Name.ToPascalCase()}Result";

                                sb.AppendClass(parameterTypeName, new string[] { "public" }, $"{baseMethodParamsInterfaceName}<{parameterTypeName}, {returnTypeName}>", () =>
                                {
                                    sb.AppendMethod("GetMethod", null, "string", new string[] { "public" }, () =>
                                    {
                                        sb.AppendCode($"return \"{domainInfo.Domain}.{methodInfo.Name}\";");
                                    });

                                    if (methodInfo.Parameters != null && methodInfo.Parameters.Length > 0)
                                    {
                                        methodInfo.Parameters.ToList().ForEach(AppendFieldInfo);
                                    }
                                });

                                sb.AppendClass(returnTypeName, new string[] { "public" }, null, () =>
                                {
                                    if (methodInfo.Returns != null && methodInfo.Returns.Length > 0)
                                    {
                                        methodInfo.Returns.ToList().ForEach(AppendFieldInfo);
                                    }
                                });
                            }
                        }

                        #endregion

                        #region Types

                        if (domainInfo.Types != null && domainInfo.Types.Length > 0)
                        {
                            foreach (var typeInfo in domainInfo.Types)
                            {
                                if (typeInfo.Enum != null && typeInfo.Enum.Length > 0)
                                {
                                    sb.AppendSummaryAnnotate(typeInfo.Description ?? string.Empty);

                                    if (typeInfo.Experimental) sb.AppendAttribute(ExperimentalAttributeName);
                                    if (typeInfo.Deprecated) sb.AppendAttribute(DeprecatedAttributeName);

                                    sb.AppendEnum(typeInfo.Id, new string[] { "public" }, () =>
                                    {
                                        int index = 1;

                                        foreach (var enumItem in typeInfo.Enum)
                                        {
                                            sb.AppendAttribute("EnumValue", (null, $"\"{Regex.Escape(enumItem)}\""));
                                            sb.AppendEnumItem(enumItem.ToPascalCase(), index.ToString());

                                            ++index;
                                        }
                                    });

                                    continue;
                                }

                                if (typeInfo.IsBasicType())
                                {
                                    continue;
                                }

                                sb.AppendSummaryAnnotate(typeInfo.Description ?? string.Empty);

                                if (typeInfo.Experimental) sb.AppendAttribute(ExperimentalAttributeName);
                                if (typeInfo.Deprecated) sb.AppendAttribute(DeprecatedAttributeName);

                                sb.AppendClass(typeInfo.Id, new string[] { "public" }, null, () =>
                                {
                                    if (typeInfo.Properties != null && typeInfo.Properties.Length > 0)
                                    {
                                        typeInfo.Properties.ToList().ForEach(AppendFieldInfo);
                                    }
                                });
                            }
                        }

                        #endregion
                    });

                    void AppendFieldInfo(GFieldInfo fieldInfo)
                    {
                        var typeName = GetCType(domainInfo,fieldInfo);

                        if (fieldInfo.Optional)
                        {
                            typeName += "?";
                        }

                        sb.AppendSummaryAnnotate(fieldInfo.Description ?? string.Empty);

                        if (fieldInfo.Enum != null && fieldInfo.Enum.Length > 0)
                        {
                            sb.AppendValueAnnotate(string.Join(",", fieldInfo.Enum));
                        }

                        if (fieldInfo.Experimental) sb.AppendAttribute(ExperimentalAttributeName);
                        if (fieldInfo.Deprecated) sb.AppendAttribute(DeprecatedAttributeName);

                        sb.AppendAutoProperty(fieldInfo.Name.ToPascalCase(), typeName, new string[] { "public" });
                    }
                }
            });

            File.WriteAllText(OutputCSFilePath, sb.ToString());

            string GetCType(GDomainInfo domainInfo,  GBaseTypeInfo baseTypeInfo)
            {
                switch (baseTypeInfo.Type)
                {
                    case "string": return "string";
                    case "integer": return "int";
                    case "number": return "double";
                    case "boolean": return "bool";
                    case "any": return "object";
                    case "array": return GetCType(domainInfo, baseTypeInfo.Items) + "[]";
                }

                if (!string.IsNullOrEmpty(baseTypeInfo.Ref))
                {
                    if (gcTypeMap.TryGetValue(baseTypeInfo.Ref, out var mapedTypeName))
                    {
                        return mapedTypeName;
                    }
                    else if (baseTypeInfo.Ref.IndexOf('.') is -1 && gcTypeMap.TryGetValue($"{domainInfo.Domain}.{baseTypeInfo.Ref}", out mapedTypeName))
                    {
                        return mapedTypeName;
                    }

                    return baseTypeInfo.Ref;
                }

                return "object";
            }
        }
    }
}