using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ChromeDevToolsProtocol.SourceGenerator
{
    public static class Helper
    {
        static readonly char[] Digitals = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        public static string ToPascalCase(this string str)
        {
            return Regex.Replace(str, @"((^[a-z])|(-[A-Za-z]))", (match) =>
            {
                if (match.Length == 2)
                {
                    return char.ToUpper(str[match.Index + 1]).ToString();
                }
                else
                {
                    return char.ToUpper(str[match.Index]).ToString();
                }
            });
        }

        public static string ToCamelCase(this string str)
        {
            return Regex.Replace(str, @"((^[A-Z])|(-[A-Za-z]))", (match) =>
            {
                if (match.Length == 2)
                {
                    return char.ToUpper(str[match.Index + 1]).ToString();
                }
                else
                {
                    return char.ToLower(str[match.Index]).ToString();
                }
            });
        }

        public static string RemoveStart(this string str, string removed, StringComparison stringComparison = StringComparison.CurrentCulture)
        {
            if (str.StartsWith(removed, stringComparison))
            {
                return str.Remove(0, removed.Length);
            }

            return str;
        }

        public static string RemoveEnd(this string str, string removed, StringComparison stringComparison = StringComparison.CurrentCulture)
        {
            if (str.EndsWith(removed, stringComparison))
            {
                return str.Remove(str.Length - removed.Length, removed.Length);
            }

            return str;
        }

        public static void PrepareDirectory(this DirectoryInfo directoryInfo)
        {
            if (!directoryInfo.Exists)
            {
                directoryInfo.Parent?.PrepareDirectory();

                directoryInfo.Create();
            }
        }

        public static string ToHexString(this byte[] bytes)
        {
            const int byte_len = 2; // 表示一个 byte 的字符长度。

            var chars = new char[byte_len * bytes.Length];

            var index = 0;

            foreach (var item in bytes)
            {
                chars[index] = Digitals[item >> 4/* byte high */]; ++index;
                chars[index] = Digitals[item & 15/* byte low  */]; ++index;
            }

            return new string(chars);
        }

        public static byte[] FromHexString(string str)
        {
            const int byte_len = 2; // 表示一个 byte 的字符长度。

            if ((str.Length % byte_len) != 0)
            {
                throw new ArgumentException();
            }

            var bytes = new byte[str.Length / byte_len];

            for (int i = 0; i < str.Length; i += byte_len)
            {
                var high = GetHexDigit(str[i]);
                var low = GetHexDigit(str[i + 1]);


                bytes[i / byte_len] = (byte)((((uint)high) << 4) | (((uint)low) & 15));
            }

            return bytes;
        }

        private static int GetHexDigit(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return (c - 'a') + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return (c - 'A') + 10;
            }

            throw new ArgumentException();
        }

        public static byte[] ComputeHash<THashAlgorithm>(this byte[] bytes) where THashAlgorithm : HashAlgorithm
        {
            return HashAlgorithmInstancePool<THashAlgorithm>.ThreadInstance.ComputeHash(bytes);
        }

        public static string ComputeHashWithToHexString<THashAlgorithm>(this byte[] bytes) where THashAlgorithm : HashAlgorithm
        {
            return HashAlgorithmInstancePool<THashAlgorithm>.ThreadInstance.ComputeHash(bytes).ToHexString();
        }

        static class HashAlgorithmInstancePool<THashAlgorithm> where THashAlgorithm : HashAlgorithm
        {
            [ThreadStatic]
            static THashAlgorithm thread_instance;

            public static THashAlgorithm ThreadInstance
            {
                get => thread_instance ??= Create();
            }

            public static THashAlgorithm Create()
            {
                var createMethod = typeof(THashAlgorithm).GetMethod(
                    nameof(HashAlgorithm.Create),
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                    Type.DefaultBinder,
                    Type.EmptyTypes,
                    null);

                if (createMethod != null)
                {
                    return (THashAlgorithm)createMethod.Invoke(null, new object[] { })!;
                }
                else
                {
                    return Activator.CreateInstance<THashAlgorithm>();
                }
            }
        }
    }
}