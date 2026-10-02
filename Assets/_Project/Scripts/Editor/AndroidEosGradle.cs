#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Fits the EOS plugin's Android library into the Gradle project Unity 6.3 generates (Gradle
    /// 9.1, Android Gradle Plugin 9.0). Before every Android build the plugin (6.2.0) copies
    /// eos_dependencies.androidlib into Assets/Plugins/Android/EOS, and Unity copies that into the
    /// Gradle project as it is. Its build script was written for AGP 3.6: a buildscript block of its
    /// own with jcenter(), which Gradle 9 no longer has, and no namespace, which AGP 8 and later
    /// require (they no longer read it from the manifest's package attribute, and warn about that
    /// attribute); it also keeps the old SDK settings and build tools 30.0.3, which AGP 9 ignores
    /// with a warning. The SDK library beside it (eos-sdk.aar) asks the app for core library
    /// desugaring, and with minify on, R8 stops at two annotations that Tink, under the module's
    /// androidx.security, refers to but does not ship.
    /// <para>
    /// After Unity writes the Gradle project, this patches the copies there, never the plugin's own
    /// files, which are written again before every build: the module's build script and manifest,
    /// R8 rules for the module, and desugaring in the launcher. Each step sees its own work and does
    /// nothing the second time, and a missing file is skipped. A failure is logged and does not stop
    /// the build; Gradle then reports the module's original error.
    /// </para>
    /// </summary>
    public sealed class AndroidEosGradle : IPostGenerateGradleAndroidProject
    {
        /// <summary>The plugin's library module, inside unityLibrary.</summary>
        public const string ModuleFolder = "eos_dependencies.androidlib";

        /// <summary>The module's namespace when its manifest no longer names the package.</summary>
        public const string DefaultNamespace = "com.pew.eos_dependencies";

        /// <summary>The desugaring library; eos-sdk.aar asks for desugar_jdk_libs 2.0.3 or later.</summary>
        public const string DesugarLibrary = "com.android.tools:desugar_jdk_libs:2.1.5";

        /// <summary>The module's R8 rules, which reach the app (consumerProguardFiles) when minify is on.</summary>
        public const string RulesFile = "proguard-eos.txt";

        /// <summary>
        /// androidx.security:security-crypto brings Tink, whose classes refer to compile-only
        /// annotations that no dependency ships; without these lines R8 fails on them as missing.
        /// </summary>
        public const string Rules =
            "# Written by Callbreak.Editor.AndroidEosGradle on every Android build.\n" +
            "# Tink (through androidx.security:security-crypto) refers to compile-only annotations.\n" +
            "-dontwarn com.google.errorprone.annotations.**\n" +
            "-dontwarn javax.annotation.**\n";

        private static readonly Regex JCenter = new Regex(@"(?m)^[ \t]*jcenter\(\)[^\r\n]*(\r?\n)?");
        private static readonly Regex BuildTools = new Regex(@"(?m)^[ \t]*buildToolsVersion\b[^\r\n]*(\r?\n)?");

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            try
            {
                // path is the unityLibrary module; the Gradle project's root holds it.
                string changes = Patch(Path.GetFullPath(Path.Combine(path, "..")));
                if (changes.Length > 0)
                {
                    Debug.Log("[Callbreak] EOS Android module fitted to the Gradle project: " + changes);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Callbreak] The EOS Android module was not fitted to the Gradle project: " + e.Message);
            }
        }

        /// <summary>
        /// Patches the Gradle project at <paramref name="root"/>, the folder that holds unityLibrary
        /// and launcher. Returns what it changed, or "" when there was nothing to change (or no EOS
        /// module to change).
        /// </summary>
        public static string Patch(string root)
        {
            string module = Path.Combine(root, "unityLibrary", ModuleFolder);
            if (!Directory.Exists(module))
            {
                return string.Empty;
            }

            var changes = new List<string>();
            Dictionary<string, string> properties = ReadProperties(Path.Combine(root, "gradle.properties"));

            // The manifest first: the package it names becomes the module's namespace.
            string space = null;
            foreach (string manifest in new[] { Path.Combine(module, "AndroidManifest.xml"), Path.Combine(module, "src", "main", "AndroidManifest.xml") })
            {
                string package = File.Exists(manifest) ? RemovePackage(manifest) : null;
                if (package != null)
                {
                    space = package.Trim().Length > 0 ? package.Trim() : space;
                    changes.Add("manifest package removed");
                }
            }

            int compileSdk = Number(properties, "unity.compileSdkVersion");
            int minSdk = Number(properties, "unity.minSdkVersion");
            int targetSdk = Number(properties, "unity.targetSdkVersion");
            if (Rewrite(Path.Combine(module, "build.gradle"), text => FitModuleScript(text, space ?? DefaultNamespace, compileSdk, minSdk, targetSdk)))
            {
                changes.Add("module build.gradle fitted");
            }

            string rules = Path.Combine(module, RulesFile);
            if (!File.Exists(rules) || File.ReadAllText(rules) != Rules)
            {
                File.WriteAllText(rules, Rules);
                changes.Add("module R8 rules written");
            }

            if (Rewrite(Path.Combine(root, "launcher", "build.gradle"), EnableDesugaring))
            {
                changes.Add("launcher desugaring on");
            }

            return string.Join(", ", changes);
        }

        /// <summary>
        /// The module's build script as AGP 9 takes it: no buildscript block (the root project
        /// brings AGP, and the settings bring the repositories), no jcenter() or build tools, a
        /// namespace, compileSdk, minSdk and targetSdk at the levels Unity uses for the game (a level
        /// that is 0 keeps the script's own), and the module's R8 rules for the app. The settings it
        /// writes use assignments ("name = value"): Gradle 9 warns that the older "name value" form
        /// goes in Gradle 10.
        /// </summary>
        public static string FitModuleScript(string text, string space, int compileSdk, int minSdk, int targetSdk)
        {
            string newline = NewlineOf(text);
            text = RemoveBlock(text, "buildscript");
            text = JCenter.Replace(text, string.Empty);
            text = BuildTools.Replace(text, string.Empty);
            text = Rename(text, "compileSdkVersion", "compileSdk", compileSdk);
            text = Rename(text, "minSdkVersion", "minSdk", minSdk);
            text = Rename(text, "targetSdkVersion", "targetSdk", targetSdk);
            if (!Regex.IsMatch(text, @"(?m)^[ \t]*namespace\b"))
            {
                text = InsertFirst(text, "android", "namespace = \"" + space + "\"", newline);
            }

            if (minSdk > 0 && !Regex.IsMatch(text, @"(?m)^[ \t]*minSdk\b"))
            {
                text = HasBlock(text, "defaultConfig")
                    ? InsertFirst(text, "defaultConfig", "minSdk = " + minSdk, newline)
                    : InsertFirst(text, "android", "defaultConfig { minSdk = " + minSdk + " }", newline);
            }

            if (!Regex.IsMatch(text, @"\bconsumerProguardFiles\b[^\r\n]*" + Regex.Escape(RulesFile)))
            {
                string consumerRules = "consumerProguardFiles '" + RulesFile + "'";
                text = HasBlock(text, "defaultConfig")
                    ? InsertFirst(text, "defaultConfig", consumerRules, newline)
                    : InsertFirst(text, "android", "defaultConfig { " + consumerRules + " }", newline);
            }

            return text;
        }

        /// <summary>A build script with core library desugaring on and the desugaring library added.</summary>
        public static string EnableDesugaring(string text)
        {
            string newline = NewlineOf(text);
            text = Regex.Replace(text, @"\bcoreLibraryDesugaringEnabled([ \t]*=?[ \t]*)false\b", "coreLibraryDesugaringEnabled${1}true");
            if (!Regex.IsMatch(text, @"\bcoreLibraryDesugaringEnabled\b"))
            {
                text = HasBlock(text, "compileOptions")
                    ? InsertFirst(text, "compileOptions", "coreLibraryDesugaringEnabled = true", newline)
                    : InsertFirst(text, "android", "compileOptions { coreLibraryDesugaringEnabled = true }", newline);
            }

            if (!Regex.IsMatch(text, @"\bcoreLibraryDesugaring\b[ \t]*[(""']"))
            {
                string dependency = "coreLibraryDesugaring '" + DesugarLibrary + "'";
                text = Regex.IsMatch(text, @"(?m)^dependencies\s*\{")
                    ? InsertFirst(text, "dependencies", dependency, newline, topLevel: true)
                    : text + newline + "dependencies {" + newline + "    " + dependency + newline + "}" + newline;
            }

            return text;
        }

        private static bool Rewrite(string path, Func<string, string> fit)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            string text = File.ReadAllText(path);
            string fitted = fit(text);
            if (fitted == text)
            {
                return false;
            }

            File.WriteAllText(path, fitted);
            return true;
        }

        /// <summary>Takes the package attribute off the manifest; returns it, or null when there was none.</summary>
        private static string RemovePackage(string manifest)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifest);
            XmlElement root = document.DocumentElement;
            if (root == null || !root.HasAttribute("package"))
            {
                return null;
            }

            string package = root.GetAttribute("package");
            root.RemoveAttribute("package");
            document.Save(manifest);
            return package;
        }

        /// <summary>Renames a setting that holds a line of its own, with the given value when it is above 0.</summary>
        private static string Rename(string text, string from, string to, int value)
        {
            var setting = new Regex(@"(?m)^([ \t]*)" + Regex.Escape(from) + @"\b[ \t]*=?[ \t]*([^\r\n]*?)[ \t]*(?=\r?$)");
            return setting.Replace(text, match => match.Groups[1].Value + to + " = " + (value > 0 ? value.ToString() : match.Groups[2].Value));
        }

        private static bool HasBlock(string text, string name) => Regex.IsMatch(text, @"(?m)^[ \t]*" + Regex.Escape(name) + @"\s*\{");

        /// <summary>Puts a line first inside the first block of that name that opens a line of its own.</summary>
        private static string InsertFirst(string text, string block, string line, string newline, bool topLevel = false)
        {
            Match opening = Regex.Match(text, @"(?m)^(" + (topLevel ? string.Empty : @"[ \t]*") + ")" + Regex.Escape(block) + @"\s*\{[ \t]*\r?\n");
            return opening.Success
                ? text.Insert(opening.Index + opening.Length, opening.Groups[1].Value + "    " + line + newline)
                : text;
        }

        /// <summary>Removes each "name { ... }" block that starts a line; braces in strings and comments don't count.</summary>
        private static string RemoveBlock(string text, string name)
        {
            var opening = new Regex(@"(?m)^[ \t]*" + Regex.Escape(name) + @"\s*\{");
            Match match = opening.Match(text);
            while (match.Success)
            {
                int close = ClosingBrace(text, match.Index + match.Length - 1);
                if (close < 0)
                {
                    return text;
                }

                int end = close + 1;
                while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
                {
                    end++;
                }

                end += end < text.Length && text[end] == '\r' ? 1 : 0;
                end += end < text.Length && text[end] == '\n' ? 1 : 0;
                text = text.Remove(match.Index, end - match.Index);
                match = opening.Match(text, match.Index);
            }

            return text;
        }

        private static int ClosingBrace(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    i = text.IndexOf('\n', i);
                    if (i < 0)
                    {
                        return -1;
                    }
                }
                else if (c == '/' && next == '*')
                {
                    i = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (i < 0)
                    {
                        return -1;
                    }

                    i++;
                }
                else if (c == '"' || c == '\'')
                {
                    i = ClosingQuote(text, i);
                    if (i < 0)
                    {
                        return -1;
                    }
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}' && --depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ClosingQuote(string text, int open)
        {
            for (int i = open + 1; i < text.Length; i++)
            {
                if (text[i] == '\\')
                {
                    i++;
                }
                else if (text[i] == text[open])
                {
                    return i;
                }
            }

            return -1;
        }

        private static Dictionary<string, string> ReadProperties(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path))
            {
                return values;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                int equals = line.IndexOf('=');
                if (equals > 0 && !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }

            return values;
        }

        private static int Number(Dictionary<string, string> values, string key) =>
            values.TryGetValue(key, out string text) && int.TryParse(text, out int number) && number > 0 ? number : 0;

        private static string NewlineOf(string text) => text.Contains("\r\n") ? "\r\n" : "\n";
    }
}
#endif
