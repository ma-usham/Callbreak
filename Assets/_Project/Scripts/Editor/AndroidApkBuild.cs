using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// The Editor half of a Development APK build (Tools/Unity/android-apk.sh): exports the Android
    /// player as a development Gradle project into <see cref="ProjectPath"/> (the development player,
    /// which shows Google's test ads). The script then builds it with Unity's Gradle, signed with the
    /// Android debug key, into Builds/Android/Callbreak.apk; the project's own keystore and Player
    /// Settings are left alone. The result goes to <see cref="ReportPath"/>, because an export
    /// outlasts any command that starts it.
    /// </summary>
    public static class AndroidApkBuild
    {
        public const string ProjectPath = "Temp/AndroidApk";
        public const string ReportPath = "Temp/AndroidApk-report.txt";

        /// <summary>Starts the export on the next Editor tick and returns at once; the report file says how it went.</summary>
        public static string ScheduleExport()
        {
            if (File.Exists(ReportPath))
            {
                File.Delete(ReportPath);
            }

            EditorApplication.delayCall += () => Export();
            return "development export scheduled; the result will be in " + ReportPath;
        }

        public static string Export()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return Finish("not started: leave Play mode first");
            }

            bool exportWas = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var text = new StringBuilder();
            try
            {
                if (Directory.Exists(ProjectPath))
                {
                    Directory.Delete(ProjectPath, true);
                }

                EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                    locationPathName = ProjectPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                text.Append(summary.result).Append(": ")
                    .Append(summary.totalErrors).Append(" errors, ")
                    .Append(summary.totalWarnings).Append(" warnings, ")
                    .Append(summary.totalTime.TotalSeconds.ToString("0")).Append(" s, development export to ")
                    .Append(ProjectPath).AppendLine();
                foreach (BuildStep step in report.steps)
                {
                    foreach (BuildStepMessage message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            text.Append("error in ").Append(step.name).Append(": ").AppendLine(message.content);
                        }
                        else if (message.type == LogType.Warning)
                        {
                            text.Append("warning in ").Append(step.name).Append(": ").AppendLine(message.content);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                text.Append("failed: ").AppendLine(e.ToString());
            }
            finally
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = exportWas;
            }

            return Finish(text.ToString());
        }

        private static string Finish(string result)
        {
            File.WriteAllText(ReportPath, result);
            Debug.Log("[AndroidApkBuild] " + result);
            return result;
        }
    }
}
