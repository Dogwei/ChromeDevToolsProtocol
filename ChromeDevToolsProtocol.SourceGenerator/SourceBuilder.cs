using System;
using System.IO;
using System.Text;

namespace ChromeDevToolsProtocol.SourceGenerator
{
    public class SourceBuilder
    {
        readonly StringBuilder sb;

        public SourceBuilder()
        {
            sb = new StringBuilder();
        }

        public void AppendSummaryAnnotate(string innerXml)
        {
            using var sr = new StringReader(innerXml);

            sb.Append("/// <summary>");
            sb.AppendLine();

            while (sr.ReadLine() is string line)
            {
                sb.Append("/// ");
                sb.Append(line);
                sb.AppendLine();
            }

            sb.Append("/// </summary>");
            sb.AppendLine();
        }

        public void AppendValueAnnotate(string innerXml)
        {
            using var sr = new StringReader(innerXml);

            sb.Append("/// <value>");
            sb.AppendLine();

            while (sr.ReadLine() is string line)
            {
                sb.Append("/// ");
                sb.Append(line);
                sb.AppendLine();
            }

            sb.Append("/// </value>");
            sb.AppendLine();
        }

        public void AppendCode(string code)
        {
            sb.Append(code);
            sb.AppendLine();
        }

        public void AppendSwitch(string memberName, (string constCode, Action blockCallback)[] cases, Action defaultBlockCallback = null)
        {
            sb.Append("switch");
            sb.Append("(");
            sb.Append(memberName);
            sb.Append(")");

            AppendScope(() =>
            {
                foreach (var (caseConstCode, caseBlockCallback) in cases)
                {
                    sb.Append("case");
                    sb.Append(" ");
                    sb.Append(caseConstCode);
                    sb.Append(":");

                    caseBlockCallback();
                }

                if (defaultBlockCallback != null)
                {
                    sb.Append("default");
                    sb.Append(":");

                    defaultBlockCallback();
                }
            });
        }

        public void AppendAutoEvent(string eventName, string eventTypeName, string[] modifiers)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append("event");
            sb.Append(" ");
            sb.Append(eventTypeName);
            sb.Append(" ");
            sb.Append(eventName);
            sb.Append(";");
            sb.AppendLine();
        }

        public void AppendField(string fieldName, string fieldTypeName, string[] modifiers)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append(fieldTypeName);
            sb.Append(" ");
            sb.Append(fieldName);
            sb.Append(";");
            sb.AppendLine();
        }

        public void AppendGetOnlyProperty(string propertyName, string propertyTypeName, string[] modifiers, Action getBlockCallback)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append(propertyTypeName);
            sb.Append(" ");
            sb.Append(propertyName);

            AppendScope(() =>
            {
                sb.Append("get");

                AppendScope(getBlockCallback);
            });
        }

        public void AppendAutoProperty(string propertyName, string propertyTypeName, string[] modifiers)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append(propertyTypeName);
            sb.Append(" ");
            sb.Append(propertyName);

            AppendScope(() =>
            {
                sb.Append("get");
                sb.Append(";");
                sb.Append("set");
                sb.Append(";");
            });
        }

        public void AppendMethod(string methodName, (string typeName, string name, string defaultValue)[] parameters, string returnTypeName, string[] modifiers, Action blockCallback = null)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append(returnTypeName);
            sb.Append(" ");
            sb.Append(methodName);

            sb.Append("(");

            if (parameters != null && parameters.Length > 0)
            {
                foreach (var (parameterTypeName, parameterName, parameterDefaultValue) in parameters)
                {
                    sb.Append(parameterTypeName);
                    sb.Append(" ");
                    sb.Append(parameterName);

                    if (!string.IsNullOrEmpty(parameterDefaultValue))
                    {
                        sb.Append("=");
                        sb.Append(parameterDefaultValue);
                    }

                    sb.Append(",");
                }

                --sb.Length;
            }

            sb.Append(")");

            AppendScope(blockCallback);
        }

        public void AppendBasedConstructor(string typeName, (string typeName, string name)[] parameters, string[] modifiers, Action blockCallback = null)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append(typeName);

            sb.Append("(");

            if (parameters != null && parameters.Length > 0)
            {
                foreach (var (parameterTypeName, parameterName) in parameters)
                {
                    sb.Append(parameterTypeName);
                    sb.Append(" ");
                    sb.Append(parameterName);
                    sb.Append(",");
                }

                --sb.Length;
            }

            sb.Append(")");

            sb.Append(":");
            sb.Append("base");

            sb.Append("(");

            if (parameters != null && parameters.Length > 0)
            {
                foreach (var (parameterTypeName, parameterName) in parameters)
                {
                    sb.Append(parameterName);
                    sb.Append(",");
                }

                --sb.Length;
            }

            sb.Append(")");

            AppendScope(blockCallback);
        }

        public void AppendClass(string className, string[] modifiers, string baseClassName = null, Action blockCallback = null)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append("class");
            sb.Append(" ");
            sb.Append(className);

            if (!string.IsNullOrEmpty(baseClassName))
            {
                sb.Append(":");
                sb.Append(baseClassName);
            }

            AppendScope(blockCallback);
        }

        public void AppendEnum(string enumName, string[] modifiers, Action blockCallback = null)
        {
            if (modifiers != null && modifiers.Length > 0)
            {
                foreach (var modifier in modifiers)
                {
                    sb.Append(modifier);
                    sb.Append(" ");
                }
            }

            sb.Append("enum");
            sb.Append(" ");
            sb.Append(enumName);

            AppendScope(blockCallback);
        }

        public void AppendEnumItem(string name, string value = null)
        {
            sb.Append(name);

            if (value != null)
            {
                sb.Append("=");
                sb.Append(value);
            }

            sb.Append(",");
            sb.AppendLine();
        }

        public void AppendNamespace(string namespaceName, Action blockCallback = null)
        {
            sb.Append("namespace");
            sb.Append(" ");
            sb.Append(namespaceName);

            AppendScope(blockCallback);
        }

        public void AppendScope(Action blockCallback = null)
        {
            sb.AppendLine();
            sb.Append("{");
            sb.AppendLine();

            blockCallback?.Invoke();

            sb.AppendLine();
            sb.Append("}");
            sb.AppendLine();
        }

        public void AppendAttribute(string attributeName, params (string name, string value)[] parameters)
        {
            sb.Append("[");
            sb.Append(attributeName);

            if (parameters != null && parameters.Length > 0)
            {
                sb.Append("(");

                bool isFirst = true;

                foreach (var (parameterName, parameterValue) in parameters)
                {
                    if (!isFirst)
                    {
                        sb.Append(",");
                    }

                    if (parameterName != null)
                    {
                        sb.Append(parameterName);
                        sb.Append("=");
                    }

                    sb.Append(parameterValue);

                    isFirst = false;
                }

                sb.Append(")");
            }

            sb.Append("]");
            sb.AppendLine();
        }

        public override string ToString()
        {
            return sb.ToString();
        }
    }
}