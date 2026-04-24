// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    public static class PersonalModuleRegistryHelper
    {
        private const string ModuleRegistryFileName = "ModuleRegistry.json";
        private const string EnabledModulesProfileFileName = "EnabledModules.personal.json";

        private static readonly Lazy<IReadOnlySet<ModuleType>> EnabledModuleTypes = new(LoadEnabledModuleTypes);

        private static readonly IReadOnlyDictionary<string, ModuleType> RegistryIdToModuleType = new Dictionary<string, ModuleType>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdvancedPaste"] = ModuleType.AdvancedPaste,
            ["AlwaysOnTop"] = ModuleType.AlwaysOnTop,
            ["Awake"] = ModuleType.Awake,
            ["CmdPal"] = ModuleType.CmdPal,
            ["ColorPicker"] = ModuleType.ColorPicker,
            ["CropAndLock"] = ModuleType.CropAndLock,
            ["CursorWrap"] = ModuleType.CursorWrap,
            ["EnvironmentVariables"] = ModuleType.EnvironmentVariables,
            ["FancyZones"] = ModuleType.FancyZones,
            ["FileLocksmith"] = ModuleType.FileLocksmith,
            ["FindMyMouse"] = ModuleType.FindMyMouse,
            ["GrabAndMove"] = ModuleType.GrabAndMove,
            ["Hosts"] = ModuleType.Hosts,
            ["ImageResizer"] = ModuleType.ImageResizer,
            ["KeyboardManager"] = ModuleType.KeyboardManager,
            ["Launcher"] = ModuleType.PowerLauncher,
            ["LightSwitch"] = ModuleType.LightSwitch,
            ["MeasureTool"] = ModuleType.MeasureTool,
            ["MouseHighlighter"] = ModuleType.MouseHighlighter,
            ["MouseJump"] = ModuleType.MouseJump,
            ["MousePointerCrosshairs"] = ModuleType.MousePointerCrosshairs,
            ["MouseWithoutBorders"] = ModuleType.MouseWithoutBorders,
            ["NewPlus"] = ModuleType.NewPlus,
            ["Peek"] = ModuleType.Peek,
            ["PowerAccent"] = ModuleType.PowerAccent,
            ["PowerDisplay"] = ModuleType.PowerDisplay,
            ["PowerOCR"] = ModuleType.PowerOCR,
            ["PowerPreview"] = ModuleType.ImageResizer,
            ["PowerRename"] = ModuleType.PowerRename,
            ["RegistryPreview"] = ModuleType.RegistryPreview,
            ["ShortcutGuide"] = ModuleType.ShortcutGuide,
            ["Workspaces"] = ModuleType.Workspaces,
            ["ZoomIt"] = ModuleType.ZoomIt,
        };

        private static readonly IReadOnlyDictionary<string, ModuleType[]> SettingsPageNameToModuleTypes = new Dictionary<string, ModuleType[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdvancedPastePage"] = [ModuleType.AdvancedPaste],
            ["AlwaysOnTopPage"] = [ModuleType.AlwaysOnTop],
            ["AwakePage"] = [ModuleType.Awake],
            ["CmdPalPage"] = [ModuleType.CmdPal],
            ["ColorPickerPage"] = [ModuleType.ColorPicker],
            ["CropAndLockPage"] = [ModuleType.CropAndLock],
            ["EnvironmentVariablesPage"] = [ModuleType.EnvironmentVariables],
            ["FancyZonesPage"] = [ModuleType.FancyZones],
            ["FileLocksmithPage"] = [ModuleType.FileLocksmith],
            ["HostsPage"] = [ModuleType.Hosts],
            ["ImageResizerPage"] = [ModuleType.ImageResizer],
            ["KeyboardManagerPage"] = [ModuleType.KeyboardManager],
            ["LightSwitchPage"] = [ModuleType.LightSwitch],
            ["MeasureToolPage"] = [ModuleType.MeasureTool],
            ["MouseUtilsPage"] = [ModuleType.FindMyMouse, ModuleType.MouseHighlighter, ModuleType.MouseJump, ModuleType.MousePointerCrosshairs, ModuleType.CursorWrap],
            ["MouseWithoutBordersPage"] = [ModuleType.MouseWithoutBorders],
            ["NewPlusPage"] = [ModuleType.NewPlus],
            ["PeekPage"] = [ModuleType.Peek],
            ["PowerAccentPage"] = [ModuleType.PowerAccent],
            ["PowerDisplayPage"] = [ModuleType.PowerDisplay],
            ["PowerLauncherPage"] = [ModuleType.PowerLauncher],
            ["PowerOcrPage"] = [ModuleType.PowerOCR],
            ["PowerPreviewPage"] = [ModuleType.ImageResizer],
            ["PowerRenamePage"] = [ModuleType.PowerRename],
            ["RegistryPreviewPage"] = [ModuleType.RegistryPreview],
            ["ShortcutGuidePage"] = [ModuleType.ShortcutGuide],
            ["WorkspacesPage"] = [ModuleType.Workspaces],
            ["ZoomItPage"] = [ModuleType.ZoomIt],
        };

        public static bool IsModuleVisible(ModuleType moduleType)
        {
            if (moduleType == ModuleType.GeneralSettings)
            {
                return true;
            }

            var enabledModuleTypes = EnabledModuleTypes.Value;
            return enabledModuleTypes.Count == 0 || enabledModuleTypes.Contains(moduleType);
        }

        public static bool IsSettingsPageVisible(string pageTypeName)
        {
            if (string.IsNullOrEmpty(pageTypeName))
            {
                return true;
            }

            if (!SettingsPageNameToModuleTypes.TryGetValue(pageTypeName, out var moduleTypes))
            {
                return true;
            }

            return moduleTypes.Any(IsModuleVisible);
        }

        private static IReadOnlySet<ModuleType> LoadEnabledModuleTypes()
        {
            foreach (var registryPath in GetModuleRegistryCandidatePaths())
            {
                var profilePath = Path.Combine(Path.GetDirectoryName(registryPath) ?? string.Empty, EnabledModulesProfileFileName);
                var enabledRegistryIds = TryLoadEnabledModuleProfile(profilePath);
                if (enabledRegistryIds == null)
                {
                    continue;
                }

                var registryIds = TryLoadRegistryIds(registryPath);
                if (registryIds == null)
                {
                    continue;
                }

                var enabledModuleTypes = registryIds
                    .Where(enabledRegistryIds.Contains)
                    .Where(id => RegistryIdToModuleType.ContainsKey(id))
                    .Select(id => RegistryIdToModuleType[id])
                    .ToHashSet();

                if (enabledModuleTypes.Count > 0)
                {
                    return enabledModuleTypes;
                }
            }

            return new HashSet<ModuleType>();
        }

        private static HashSet<string> TryLoadEnabledModuleProfile(string profilePath)
        {
            try
            {
                if (!File.Exists(profilePath))
                {
                    return null;
                }

                var enabledRegistryIds = JsonSerializer.Deserialize<string[]>(File.ReadAllText(profilePath));
                if (enabledRegistryIds == null || enabledRegistryIds.Length == 0 || enabledRegistryIds.Any(string.IsNullOrWhiteSpace))
                {
                    return null;
                }

                return enabledRegistryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return null;
            }
        }

        private static HashSet<string> TryLoadRegistryIds(string registryPath)
        {
            try
            {
                if (!File.Exists(registryPath))
                {
                    return null;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(registryPath));
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var registryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var moduleElement in document.RootElement.EnumerateArray())
                {
                    if (moduleElement.ValueKind != JsonValueKind.Object ||
                        !moduleElement.TryGetProperty("id", out var idElement) ||
                        idElement.ValueKind != JsonValueKind.String)
                    {
                        return null;
                    }

                    var id = idElement.GetString();
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        return null;
                    }

                    registryIds.Add(id);
                }

                return registryIds.Count > 0 ? registryIds : null;
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> GetModuleRegistryCandidatePaths()
        {
            var executableFolder = AppContext.BaseDirectory;
            yield return Path.Combine(executableFolder, "modules", ModuleRegistryFileName);
            yield return Path.Combine(executableFolder, "..", "modules", ModuleRegistryFileName);

            var current = new DirectoryInfo(executableFolder);
            while (current != null)
            {
                yield return Path.Combine(current.FullName, "src", "common", "modules", ModuleRegistryFileName);
                current = current.Parent;
            }
        }
    }
}
