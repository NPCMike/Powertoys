// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.OOBE
{
    public static class OobeModuleVisibilityHelper
    {
        private static readonly IReadOnlyDictionary<string, string[]> OobeModuleTagToSettingsPages = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdvancedPaste"] = ["AdvancedPastePage"],
            ["AlwaysOnTop"] = ["AlwaysOnTopPage"],
            ["Awake"] = ["AwakePage"],
            ["CmdNotFound"] = ["CmdNotFoundPage"],
            ["CmdPal"] = ["CmdPalPage"],
            ["ColorPicker"] = ["ColorPickerPage"],
            ["CropAndLock"] = ["CropAndLockPage"],
            ["EnvironmentVariables"] = ["EnvironmentVariablesPage"],
            ["FancyZones"] = ["FancyZonesPage"],
            ["FileLocksmith"] = ["FileLocksmithPage"],
            ["FileExplorer"] = ["PowerPreviewPage"],
            ["GrabAndMove"] = ["GrabAndMovePage"],
            ["Hosts"] = ["HostsPage"],
            ["ImageResizer"] = ["ImageResizerPage"],
            ["KBM"] = ["KeyboardManagerPage"],
            ["LightSwitch"] = ["LightSwitchPage"],
            ["MeasureTool"] = ["MeasureToolPage"],
            ["MouseUtils"] = ["MouseUtilsPage"],
            ["MouseWithoutBorders"] = ["MouseWithoutBordersPage"],
            ["NewPlus"] = ["NewPlusPage"],
            ["Peek"] = ["PeekPage"],
            ["PowerDisplay"] = ["PowerDisplayPage"],
            ["PowerRename"] = ["PowerRenamePage"],
            ["QuickAccent"] = ["PowerAccentPage"],
            ["RegistryPreview"] = ["RegistryPreviewPage"],
            ["Run"] = ["PowerLauncherPage"],
            ["ShortcutGuide"] = ["ShortcutGuidePage"],
            ["TextExtractor"] = ["PowerOcrPage"],
            ["Workspaces"] = ["WorkspacesPage"],
            ["ZoomIt"] = ["ZoomItPage"],
        };

        public static bool IsVisible(string moduleTag)
        {
            if (string.IsNullOrEmpty(moduleTag) || string.Equals(moduleTag, "Overview", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!OobeModuleTagToSettingsPages.TryGetValue(moduleTag, out var settingsPages))
            {
                return true;
            }

            foreach (var settingsPage in settingsPages)
            {
                if (PersonalModuleRegistryHelper.IsSettingsPageVisible(settingsPage))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
