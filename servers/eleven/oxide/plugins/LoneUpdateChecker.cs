﻿using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Lone Update Checker", "Nikedemos", "1.0.0")]
    [Description("Checks for available updates of Lone.design plugins")]
    public class LoneUpdateChecker : RustPlugin
    {
        #region CONST/STATIC
        public const string API_URL = "https://api.lone.design/";

        public static LoneUpdateChecker Instance;

        public static Hash<string, VersionNumber> CurrentPluginVersions;

        public static StringBuilder StringBuilderInstance;
        public static ApiResponse RecentApiResponse;
        public static JsonSerializerSettings ErrorHandling;

        public static VersionNumber InvalidVersionNumber;

        public static Timer RequestTimer;
        #endregion
        #region HOOKS
        private void OnServerInitialized()
        {
            timer.Once(1F, OnServerInitializedAfterDelay);
        }

        public void OnServerInitializedAfterDelay()
        {
            Instance = this;

            InvalidVersionNumber = new VersionNumber(0, 0, 0);
            StringBuilderInstance = new StringBuilder();
            ErrorHandling = new JsonSerializerSettings { Error = (se, ev) => { ev.ErrorContext.Handled = true; } };
            CurrentPluginVersions = new Hash<string, VersionNumber>();
            LoadConfigData();
            ProcessConfigData();

            RequestSend();


            if (Configuration.CheckForUpdatesPeriodically)
            {
                var everyMinute = Configuration.HowManyMinutesBetweenPeriodicalUpdates;
                Instance.PrintWarning($"The updates will be checked every {everyMinute} minute(s)");
                RequestTimer = timer.Repeat(everyMinute * 60F, 14400, () => RequestSend());
            }
        }

        private void Unload()
        {
            if (IsObjectNull(Instance))
            {
                return;
            }

            if (!IsObjectNull(RequestTimer))
            {
                RequestTimer.Destroy();
            }

            RequestTimer = null;
            StringBuilderInstance = null;
            RecentApiResponse = null;
            ErrorHandling = null;
            CurrentPluginVersions = null;
            Instance = null;

        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (IsObjectNull(Instance))
            {
                return;
            }

            if (!Configuration.CheckForUpdatesWhenPluginsLoad)
            {
                return;
            }

            RequestSend($"{plugin.Name}.cs");

        }
        #endregion

        #region CONFIG
        public class ConfigData
        {
            public VersionNumber Version = new VersionNumber(1, 0, 0);
            public bool CheckForUpdatesWhenPluginsLoad = true;
            public bool CheckForUpdatesPeriodically = true;
            public uint HowManyMinutesBetweenPeriodicalUpdates = 30;
        }

        private ConfigData Configuration;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default config...");
            Configuration = new ConfigData();
            SaveConfigData();
        }

        private void ProcessConfigData()
        {
            bool needsSave = false;

            if (Version != Configuration.Version)
            {
                Instance.PrintWarning($"It looks like you have just updated {Title} from {Version} to {Configuration.Version}!");
                Configuration.Version = Version;

                needsSave = true;
            }

            if (Configuration.HowManyMinutesBetweenPeriodicalUpdates < 30)
            {
                Configuration.HowManyMinutesBetweenPeriodicalUpdates = 30;
                needsSave = true;
            }

            if (needsSave)
            {
                SaveConfigData();
            }
        }

        private void LoadConfigData()
        {
            bool needsSave = false;

            PrintWarning("Loading configuration file...");
            try
            {
                Configuration = Config.ReadObject<ConfigData>();
            }
            catch(Exception e)
            {
                Instance.PrintError($"\nERROR: COULD NOT READ THE CONFIG FILE!\n{e.Message}\n{e.StackTrace}\nGenerating default config...\n");
                Configuration = new ConfigData();
                needsSave = true;
            }

            if (IsObjectNull(Configuration))
            {
                Instance.PrintError($"\nERROR: CONFIG IS NULL!\nGenerating default config...\n");
                needsSave = true;
            }

            if (needsSave)
            {
                SaveConfigData();
            }
        }

        private void SaveConfigData()
        {
            Config.WriteObject(Configuration, true);
        }

        #endregion

        #region WEB REQUEST
        public class ApiResponse : List<ApiPluginInfo>
        {

        }

        public struct ApiPluginInfo
        {
            public string Name;

            public string PluginName;

            public string Version;
        }

        public static void RequestSend(string requestPlugins = default(string))
        {
            Action<int, string> callback;

            if (requestPlugins == default(string))
            {
                callback = RequestCallbackBulk;
                Instance.PrintWarning("Checking for all plugin updates...");
            }
            else
            {
                callback = RequestCallbackSingle;
                Instance.PrintWarning($"{requestPlugins} was just loaded in, checking for updates...");
            }

            RecentApiResponse = null;

            string requestURL = BuildRequestURLWhilePopulatingPluginList(requestPlugins);

            Instance.webrequest.Enqueue(requestURL, string.Empty, callback, Instance, Core.Libraries.RequestMethod.GET, null, 10F);
        }

        public static void RequestCallbackCommon(int code, string response, bool single)
        {
            if (code != 200)
            {
                Instance.PrintError($"ERROR HANDLING RESPONSE FROM THE API.\nHTTP CODE {code}:\n{response}\n");
                return;
            }

            try
            {
                RecentApiResponse = JsonConvert.DeserializeObject<ApiResponse>(response, ErrorHandling);
            }
            catch (Exception e)
            {
                Instance.PrintError($"ERROR: COULD NOT DESERIALIZE THE RESPONSE FROM THE API:\n{response}\nThe following seems to be the issue:\n{e.Message}\n{e.StackTrace}\n");
                return;
            }

            if (RecentApiResponse.Count == 0)
            {
                if (!single)
                {
                    Instance.PrintWarning($"It doesn't look like you have any Lone.design plugins installed (or the plugins that you do, have not had their product page metadata set up correctly). Get some top-notch plugins at https://lone.design and/or let the plugin devs know that their Lone.design plugin is not showing up in the database.");
                }
                else
                {
                    Instance.PrintWarning($"It doesn't look like this plugin exists in the Lone.design database. It might not be a Lone plugin or the plugin dev has not had their product page metadata set up correctly.");
                }

                return;
            }

            ApiPluginInfo currentInfo;
            VersionNumber versionPresent;
            VersionNumber versionFromAPI;

            int outdatedPluginsFound = 0;

            StringBuilderInstance.Clear();
            StringBuilderInstance.AppendLine();

            for (var i = 0; i < RecentApiResponse.Count; i++)
            {
                currentInfo = RecentApiResponse[i];

                if (!CurrentPluginVersions.ContainsKey(currentInfo.PluginName))
                {
                    continue;
                }

                versionPresent = CurrentPluginVersions[currentInfo.PluginName];
                versionFromAPI = VersionNumberFromString(currentInfo.Version);

                if (versionFromAPI == InvalidVersionNumber)
                {
                    StringBuilderInstance.AppendLine($"{currentInfo.PluginName}: ERROR! API returned an invalid version number {currentInfo.Version}");
                    continue;
                }

                if (versionFromAPI > versionPresent)
                {
                    StringBuilderInstance.AppendLine($"{currentInfo.PluginName}: out of date! You have version {versionPresent} installed, but the newest one is {versionFromAPI}!");
                    outdatedPluginsFound++;
                }
                else
                {
                    if (versionFromAPI == versionPresent)
                    {
                        StringBuilderInstance.AppendLine($"{currentInfo.PluginName}: OK. You have the most recent version {versionPresent} installed.");
                    }
                    else
                    {
                        StringBuilderInstance.AppendLine($"{currentInfo.PluginName}: OK. You seem to have a not-yet-released version {versionPresent} installed.");
                    }
                }
            }

            StringBuilderInstance.AppendLine();

            if (outdatedPluginsFound > 0)
            {
                Instance.PrintError(StringBuilderInstance.ToString());

                if (!single)
                {
                    Instance.PrintWarning("Found updates for at least 1 Lone.design plugin, check above!\n");
                }
            }
            else
            {
                Instance.PrintWarning(StringBuilderInstance.ToString());

                if (!single)
                {
                    Instance.PrintWarning("All your Lone.design plugins seem up to date.\n");
                }
            }

            return;
        }

        public static void RequestCallbackSingle(int code, string response) => RequestCallbackCommon(code, response, true);

        public static void RequestCallbackBulk(int code, string response) => RequestCallbackCommon(code, response, false);

        public static string BuildRequestURLWhilePopulatingPluginList(string filterByName = default(string))
        {
            StringBuilderInstance.Clear();
            StringBuilderInstance.Append(API_URL);
            StringBuilderInstance.Append("search/");

            CurrentPluginVersions.Clear();

            var iterateOver = Interface.Oxide.RootPluginManager.GetPlugins().ToArray();

            Core.Plugins.Plugin currentPlugin;

            string shortFilename;

            for (var i = 0; i < iterateOver.Length; i++)
            {
                currentPlugin = iterateOver[i];

                if (string.IsNullOrEmpty(currentPlugin.Filename))
                {
                    continue;
                }

                shortFilename = $"{currentPlugin.Name}.cs";

                if (filterByName != default(string))
                {
                    if (!shortFilename.Contains(filterByName))
                    {
                        continue;
                    }
                }

                StringBuilderInstance.Append(shortFilename);

                CurrentPluginVersions.Add(shortFilename, currentPlugin.Version);

                if (i < iterateOver.Length-1)
                {
                    StringBuilderInstance.Append(",");
                }
            }

            return StringBuilderInstance.ToString();
        }

        #endregion

        #region STATIC HELPERS
        public static VersionNumber VersionNumberFromString(string versionString)
        {
            var split = versionString.Split('.');

            if (split.Length != 3)
            {
                return InvalidVersionNumber;
            }

            ushort major;
            ushort minor;
            ushort patch;

            if (!ushort.TryParse(split[0], out major))
            {
                return InvalidVersionNumber;
            }

            if (!ushort.TryParse(split[1], out minor))
            {
                return InvalidVersionNumber;
            }

            if (!ushort.TryParse(split[2], out patch))
            {
                return InvalidVersionNumber;
            }

            return new VersionNumber(major, minor, patch);
        }
        public static bool IsObjectNull(object obj) => ReferenceEquals(obj, null);
        #endregion
    }
}
