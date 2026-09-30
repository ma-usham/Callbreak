using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Exports the Android player as a Gradle project into Temp/AndroidExport (menu Callbreak/Build/
    /// Export Android Project): the nearest thing to a device build that needs no device. It
    /// compiles every runtime script for Android with IL2CPP's converter, builds the three scenes
    /// with their assets and runs the build hooks (the invite-link manifest filter), and leaves the
    /// Gradle build itself out. The result is written to Temp/AndroidExport-report.txt, because a
    /// build outlasts any command that starts it.
    /// </summary>
    public static class BuildCheck
    {
        public const string ExportPath = "Temp/AndroidExport";
        public const string ReportPath = "Temp/AndroidExport-report.txt";

        [MenuItem("Callbreak/Build/Export Android Project")]
        public static void ExportAndroidProjectMenu() => ExportAndroidProject();

        /// <summary>Starts the export on the next Editor tick and returns at once; the report file says how it went.</summary>
        public static string ScheduleExport()
        {
            if (File.Exists(ReportPath))
            {
                File.Delete(ReportPath);
            }

            EditorApplication.delayCall += () => ExportAndroidProject();
            return "export scheduled; the result will be in " + ReportPath;
        }

        public static string ExportAndroidProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return Finish("not started: leave Play mode first");
            }

            bool exportWas = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var text = new StringBuilder();
            try
            {
                if (Directory.Exists(ExportPath))
                {
                    Directory.Delete(ExportPath, true);
                }

                EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                    locationPathName = ExportPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                text.Append(summary.result).Append(": ")
                    .Append(summary.totalErrors).Append(" errors, ")
                    .Append(summary.totalWarnings).Append(" warnings, ")
                    .Append(summary.totalTime.TotalSeconds.ToString("0")).Append(" s, scenes ")
                    .Append(string.Join(", ", options.scenes)).AppendLine();
                foreach (BuildStep step in report.steps)
                {
                    foreach (BuildStepMessage message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            text.Append("error in ").Append(step.name).Append(": ").AppendLine(message.content);
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
            Debug.Log("[BuildCheck] " + result);
            return result;
        }
    }
}
