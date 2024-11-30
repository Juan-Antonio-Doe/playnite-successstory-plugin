﻿
using Playnite.SDK;
using Playnite.SDK.Models;
using SuccessStory.Models;
using SuccessStory.Services;
using System;
using CommonPluginsShared.Extensions;
using CommonPluginsShared;
using System.IO;
using System.Collections.Generic;
using Playnite.SDK.Data;
using System.Text;
using System.Security.Principal;
using CommonPlayniteShared.Common;
using static CommonPluginsShared.PlayniteTools;
using Playnite.SDK.Plugins;

namespace SuccessStory.Clients
{
    public abstract class GenericAchievements
    {
        internal static ILogger Logger => LogManager.GetLogger();

        protected static IWebView webViewOffscreen;
        internal static IWebView WebViewOffscreen
        {
            get
            {
                if (webViewOffscreen == null)
                {
                    webViewOffscreen = API.Instance.WebViews.CreateOffscreenView();
                }
                return webViewOffscreen;
            }

            set => webViewOffscreen = value;
        }

        internal static SuccessStoryDatabase PluginDatabase => SuccessStory.PluginDatabase;

        protected bool? CachedConfigurationValidationResult { get; set; }
        protected bool? CachedIsConnectedResult { get; set; }

        protected string ClientName { get; }
        protected string LocalLang { get; }
        protected string LocalLangShort { get; }

        protected string LastErrorId { get; set; }
        protected string LastErrorMessage { get; set; }

        internal string CookiesPath { get; }



        // TODO Must be removed when all store is refactored
        public GenericAchievements(string ClientName, string LocalLang = "", string LocalLangShort = "")
        {
            this.ClientName = ClientName;
            this.LocalLang = LocalLang;
            this.LocalLangShort = LocalLangShort;

            CookiesPath = Path.Combine(PluginDatabase.Paths.PluginUserDataPath, CommonPlayniteShared.Common.Paths.GetSafePathName($"{ClientName}.json"));
        }


        #region Achievements
        /// <summary>
        /// Get all achievements for a game.
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public GameAchievements GetAchievements(Guid Id)
        {
            Game game = API.Instance.Database.Games.Get(Id);
            if (game == null)
            {
                return new GameAchievements();
            }

            return GetAchievements(game);
        }

        /// <summary>
        /// Get all achievements for a game.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public abstract GameAchievements GetAchievements(Game game);
        #endregion


        #region Configuration
        /// <summary>
        /// Override to validate service-specific config and display error messages to the user
        /// </summary>
        /// <param name="playniteAPI"></param>
        /// <param name="plugin"></param>
        /// <returns>false when there are errors, true if everything's good</returns>
        public abstract bool ValidateConfiguration();


        public virtual bool IsConnected()
        {
            return false;
        }

        public virtual bool IsConfigured()
        {
            return false;
        }

        public virtual bool EnabledInSettings()
        {
            return false;
        }
        #endregion


        public virtual void ResetCachedConfigurationValidationResult()
        {
            CachedConfigurationValidationResult = null;
        }

        public virtual void ResetCachedIsConnectedResult()
        {
            CachedIsConnectedResult = null;
        }


        #region Cookies
        internal List<HttpCookie> GetCookies()
        {
            if (File.Exists(CookiesPath))
            {
                try
                {
                    return Serialization.FromJson<List<HttpCookie>>(
                        Encryption.DecryptFromFile(
                            CookiesPath,
                            Encoding.UTF8,
                            WindowsIdentity.GetCurrent().User.Value));
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, "Failed to load saved cookies");
                }
            }

            return null;
        }

        internal void SetCookies(List<HttpCookie> httpCookies)
        {
            FileSystem.CreateDirectory(Path.GetDirectoryName(CookiesPath));
            Encryption.EncryptToFile(
                CookiesPath,
                Serialization.ToJson(httpCookies),
                Encoding.UTF8,
                WindowsIdentity.GetCurrent().User.Value);
        }
        #endregion


        #region Errors
        public virtual void ShowNotificationPluginDisable(string message)
        {
            LastErrorId = $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-disabled";
            LastErrorMessage = message;
            Logger.Warn($"{ClientName} is enable then disabled in Playnite");

            API.Instance.Notifications.Add(new NotificationMessage(
                $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-disabled",
                $"{PluginDatabase.PluginName}\r\n{message}",
                NotificationType.Error
            ));
        }

        public virtual void ShowNotificationPluginNoAuthenticate(ExternalPlugin pluginSource)
        {
            string message = string.Format(ResourceProvider.GetString("LOCCommonStoresNoAuthenticate"), ClientName);
            LastErrorId = $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-noauthenticate";
            LastErrorMessage = message;
            Logger.Warn($"{ClientName}: User is not authenticated");

            API.Instance.Notifications.Add(new NotificationMessage(
                $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-noauthenticate",
                $"{PluginDatabase.PluginName}\r\n{message}",
                NotificationType.Error,
                () =>
                {
                    try
                    {
                        Plugin plugin = API.Instance.Addons.Plugins.Find(x => x.Id == PlayniteTools.GetPluginId(pluginSource));
                        if (plugin != null)
                        {
                            _ = plugin.OpenSettingsView();
                            foreach (GenericAchievements achievementProvider in SuccessStoryDatabase.AchievementProviders.Values)
                            {
                                achievementProvider.ResetCachedConfigurationValidationResult();
                                achievementProvider.ResetCachedIsConnectedResult();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }
            ));
        }

        public virtual void ShowNotificationPluginTooMuchData(string message, ExternalPlugin pluginSource)
        {
            LastErrorId = $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-toomuchdata";
            LastErrorMessage = message;
            Logger.Warn(message);

            API.Instance.Notifications.Add(new NotificationMessage(
                $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-toomuchdata",
                $"{PluginDatabase.PluginName}\r\n{message}",
                NotificationType.Error,
                () =>
                {
                    try
                    {
                        Plugin plugin = API.Instance.Addons.Plugins.Find(x => x.Id == PlayniteTools.GetPluginId(pluginSource));
                        if (plugin != null)
                        {
                            _ = plugin.OpenSettingsView();
                            foreach (GenericAchievements achievementProvider in SuccessStoryDatabase.AchievementProviders.Values)
                            {
                                achievementProvider.ResetCachedConfigurationValidationResult();
                                achievementProvider.ResetCachedIsConnectedResult();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }
            ));
        }

        public virtual void ShowNotificationPluginNoConfiguration()
        {
            string message = string.Format(ResourceProvider.GetString("LOCCommonStoreBadConfiguration"), ClientName);
            LastErrorId = $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-noconfig";
            LastErrorMessage = message;
            Logger.Warn($"{ClientName} is not configured");

            API.Instance.Notifications.Add(new NotificationMessage(
                $"{PluginDatabase.PluginName}-{ClientName.RemoveWhiteSpace()}-noconfig",
                $"{PluginDatabase.PluginName}\r\n{message}",
                NotificationType.Error,
                () => {
                    _ = PluginDatabase.Plugin.OpenSettingsView();
                    foreach (GenericAchievements achievementProvider in SuccessStoryDatabase.AchievementProviders.Values)
                    {
                        achievementProvider.ResetCachedConfigurationValidationResult();
                        achievementProvider.ResetCachedIsConnectedResult();
                    }
                }
            ));
        }

        public virtual void ShowNotificationPluginError(Exception ex)
        {
            Common.LogError(ex, false, $"{ClientName}", true, PluginDatabase.PluginName);
        }

        public virtual void ShowNotificationPluginWebError(Exception ex, string url)
        {
            Common.LogError(ex, false, $"{ClientName} - Failed to load {url}", true, PluginDatabase.PluginName);
        }


        public virtual void ShowNotificationPluginErrorMessage(ExternalPlugin pluginSource)
        {
            if (!LastErrorMessage.IsNullOrEmpty())
            {
                API.Instance.Notifications.Add(new NotificationMessage(
                    LastErrorId,
                    $"{PluginDatabase.PluginName}\r\n{LastErrorMessage}",
                NotificationType.Error,
                () =>
                {
                    try
                    {
                        Plugin plugin = API.Instance.Addons.Plugins.Find(x => x.Id == PlayniteTools.GetPluginId(pluginSource));
                        if (plugin != null)
                        {
                            _ = plugin.OpenSettingsView();
                            foreach (GenericAchievements achievementProvider in SuccessStoryDatabase.AchievementProviders.Values)
                            {
                                achievementProvider.ResetCachedConfigurationValidationResult();
                                achievementProvider.ResetCachedIsConnectedResult();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }
                ));
            }
        }
        #endregion
    }
}
