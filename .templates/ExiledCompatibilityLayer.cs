namespace Company.PluginProject.Compatibility
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using LabApi.Features.Console;

    /// <summary>
    /// Advanced cross-platform compatibility engine that bridges legacy EXILED configurations 
    /// with the LabAPI architecture using native OS reparse points or runtime mirroring fail-safes.
    /// </summary>
    public static class ExiledCompatibilityLayer
    {
        /// <summary>
        /// Executes the synchronization fallback to bridge directories and load missing configurations.
        /// </summary>
        public static void ExecuteFallback(Plugin plugin)
        {
            Logger.Warn("LoadConfigs was bypassed by the native lifecycle. Deploying dynamic cross-framework routing matrix.");
            
            TryLinkEnvironments(plugin);
            plugin.LoadConfigs();
        }

        private static void TryLinkEnvironments(Plugin plugin)
        {
            try
            {
                // 1. Resolve absolute LabAPI destination path (Port-specific, dynamic, explicit)
                string labApiConfigDir = plugin.GetConfigDirectory(isGlobal: false).FullName;
                
                if (!Directory.Exists(labApiConfigDir))
                {
                    Directory.CreateDirectory(labApiConfigDir);
                }

                // 2. Extract current server runtime port safely from the native core assembly
                string portString = ServerStatic.ServerPort.ToString();
                
                // 3. Resolve Cross-Platform AppData baseline (Handles Linux $HOME/.config vs Windows %APPDATA%)
                string baseAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(baseAppData))
                {
                    baseAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }

                // 4. Evaluate precise EXILED workspace structures (Port-isolated vs Unified Plugins Folder)
                string exiledPortSpecificPath = Path.Combine(baseAppData, "EXILED", "Configs", portString, plugin.Name);
                string exiledGlobalPluginsPath = Path.Combine(baseAppData, "EXILED", "Configs", "Plugins", plugin.Name);

                string baseExiledConfigTree = Path.Combine(baseAppData, "EXILED", "Configs", portString);
                string targetExiledPath = Directory.Exists(baseExiledConfigTree) ? exiledPortSpecificPath : exiledGlobalPluginsPath;

                // 5. Reparse Point Shield: If link already deployed by OS, exit thread immediately
                if (Directory.Exists(targetExiledPath) && IsReparsePoint(targetExiledPath))
                {
                    return;
                }

                // 6. Data Preservation Matrix: If physical folder exists, migrate data before binding link
                if (Directory.Exists(targetExiledPath))
                {
                    Logger.Info("Legacy EXILED directory detected. Migrating historical configuration assets to LabAPI workspace...");
                    MigrateConfigurationFiles(targetExiledPath, labApiConfigDir);
                    
                    Directory.Delete(targetExiledPath, recursive: true);
                }

                // Ensure parent structural tree exists for legacy framework visibility
                string parentDir = Path.GetDirectoryName(targetExiledPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                // 7. Execution Loop: Attempt to link environments natively via OS kernel
                bool linkSuccess = TryCreateOSLink(targetExiledPath, labApiConfigDir);

                if (linkSuccess)
                {
                    Logger.Info($"Seamless cross-framework link deployed. [EXILED Proxy]: {targetExiledPath} ===> [LabAPI Workspace]: {labApiConfigDir}");
                }
                else
                {
                    // 8. Silent Fallback Matrix: If OS denies link creation due to restrictions, fall back to passive replication
                    Logger.Warn("OS denied native symlink privileges. Deploying passive replication fallback layer.");
                    Directory.CreateDirectory(targetExiledPath);
                    MigrateConfigurationFiles(labApiConfigDir, targetExiledPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Critical failure in compatibility matrix routing loop: {ex.Message}");
            }
        }

        private static bool TryCreateOSLink(string linkPath, string targetPath)
        {
            bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            
            ProcessStartInfo psi = isWindows 
                ? new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
                : new ProcessStartInfo("/bin/bash", $"-c \"ln -s '{targetPath}' '{linkPath}'\"");

            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            try
            {
                using (Process process = Process.Start(psi))
                {
                    process?.WaitForExit();
                }
                return Directory.Exists(linkPath);
            }
            catch
            {
                return false;
            }
        }

        private static void MigrateConfigurationFiles(string sourceFolder, string destinationFolder)
        {
            try
            {
                string[] files = Directory.GetFiles(sourceFolder, "*.yml");
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    string dest = Path.Combine(destinationFolder, name);
                    if (!File.Exists(dest))
                    {
                        File.Copy(file, dest, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Asset replication execution choked: {ex.Message}");
            }
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
            }
            catch
            {
                return false;
            }
        }
    }
}