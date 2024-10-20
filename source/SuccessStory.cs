﻿using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using CommonPluginsShared;
using SuccessStory.Models;
using SuccessStory.Views;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommonPlayniteShared;
using SuccessStory.Services;
using System.Windows.Automation;
using CommonPluginsShared.PlayniteExtended;
using System.Windows.Media;
using CommonPluginsShared.Controls;
using SuccessStory.Controls;
using CommonPluginsShared.Models;
using CommonPlayniteShared.Common;
using System.Reflection;
using CommonPluginsShared.Extensions;
using System.Diagnostics;
using QuickSearch.SearchItems;
using CommonPluginsStores.Steam;
using SuccessStory.Clients;
using SuccessStory.Models.RetroAchievements;
using CommonPluginsStores.Epic;
using System.Resources;

namespace SuccessStory
{
    public class SuccessStory : PluginExtended<SuccessStorySettingsViewModel, SuccessStoryDatabase>
    {
        public override Guid Id => Guid.Parse("cebe6d32-8c46-4459-b993-5a5189d60788");

        public static SteamApi SteamApi { get; set; }
        public static EpicApi EpicApi { get; set; }

        internal TopPanelItem TopPanelItem { get; set; }
        internal SidebarItem SidebarItem { get; set; }
        internal SidebarItem SidebarRaItem { get; set; }
        internal SidebarItemControl SidebarItemControl { get; set; }
        internal SidebarItemControl SidebarRaItemControl { get; set; }

        public static bool TaskIsPaused { get; set; } = false;
        private CancellationTokenSource TokenSource => new CancellationTokenSource();

        public static bool IsFromMenu { get; set; } = false;

        private bool PreventLibraryUpdatedOnStart { get; set; } = true;


        public SuccessStory(IPlayniteAPI api) : base(api)
        {
            // Manual dll load
            try
            {
                string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string PathDLL = Path.Combine(PluginPath, "VirtualizingWrapPanel.dll");
                if (File.Exists(PathDLL))
                {
                    Assembly DLL = Assembly.LoadFile(PathDLL);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            PluginDatabase.InitializeClient(this);

            // Custom theme button
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnCustomThemeButtonClick));

            // Add Event for WindowBase for get the "WindowSettings".
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler(WindowBase_LoadedEvent));

            // Initialize top & side bar
            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                TopPanelItem = new SuccessStoryTopPanelItem(this);
                SidebarItem = new SuccessStoryViewSidebar(this);
                SidebarRaItem = new SuccessStoryViewRaSidebar(this);
            }

            // Custom elements integration
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> {
                    "PluginButton", "PluginViewItem", "PluginProgressBar", "PluginCompactList",
                    "PluginCompactLocked", "PluginCompactUnlocked", "PluginChart",
                    "PluginUserStats", "PluginList"
                },
                SourceName = PluginDatabase.PluginName
            });

            // Settings integration
            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = PluginDatabase.PluginName,
                SettingsRoot = $"{nameof(PluginSettings)}.{nameof(PluginSettings.Settings)}"
            });

            // Playnite search integration
            Searches = new List<SearchSupport>
            {
                new SearchSupport("ss", "SuccessStory", new SuccessStorySearch())
            };
        }


        #region Custom event
        public void OnCustomThemeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string ButtonName = ((Button)sender).Name;
                if (ButtonName == "PART_CustomScButton")
                {
                    Common.LogDebug(true, $"OnCustomThemeButtonClick()");

                    PluginDatabase.IsViewOpen = true;
                    dynamic ViewExtension = null;

                    WindowOptions windowOptions = new WindowOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = false,
                        ShowCloseButton = true,
                        CanBeResizable = false,
                        Height = 800,
                        Width = 1110
                    };

                    if (PluginDatabase.PluginSettings.Settings.EnableOneGameView)
                    {
                        if ((PluginDatabase.GameContext.Name.IsEqual("overwatch") || PluginDatabase.GameContext.Name.IsEqual("overwatch 2")) && (PluginDatabase.GameContext.Source?.Name?.IsEqual("battle.net") ?? false))
                        {
                            ViewExtension = new SuccessStoryOverwatchView(PluginDatabase.GameContext);
                        }
                        else if (PluginSettings.Settings.EnableGenshinImpact && PluginDatabase.GameContext.Name.IsEqual("Genshin Impact"))
                        {
                            ViewExtension = new SuccessStoryCategoryView(PluginDatabase.GameContext);
                        }
                        else if (PluginSettings.Settings.EnableGuildWars2 && PluginDatabase.GameContext.Name.IsEqual("Guild Wars 2"))
                        {
                            ViewExtension = new SuccessStoryCategoryView(PluginDatabase.GameContext);
                        }
                        else
                        {
                            ViewExtension = PluginDatabase.GameContext.PluginId == PlayniteTools.GetPluginId(PlayniteTools.ExternalPlugin.SteamLibrary) && PluginSettings.Settings.SteamGroupData
                                ? (dynamic)new SuccessStoryCategoryView(PluginDatabase.GameContext)
                                : (dynamic)new SuccessStoryOneGameView(PluginDatabase.GameContext);
                        }
                    }
                    else
                    {
                        ViewExtension = PluginDatabase.PluginSettings.Settings.EnableRetroAchievementsView && PlayniteTools.IsGameEmulated(PluginDatabase.GameContext)
                            ? (dynamic)new SuccessView(true, PluginDatabase.GameContext)
                            : (dynamic)new SuccessView(false, PluginDatabase.GameContext);
                    }


                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                    _ = windowExtension.ShowDialog();
                    PluginDatabase.IsViewOpen = false;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void WindowBase_LoadedEvent(object sender, System.EventArgs e)
        {
            string WinIdProperty = string.Empty;
            try
            {
                WinIdProperty = ((Window)sender).GetValue(AutomationProperties.AutomationIdProperty).ToString();

                if (WinIdProperty == "WindowSettings" ||WinIdProperty == "WindowExtensions" || WinIdProperty == "WindowLibraryIntegrations")
                {
                    foreach (var achievementProvider in SuccessStoryDatabase.AchievementProviders.Values)
                    {
                        achievementProvider.ResetCachedConfigurationValidationResult();
                        achievementProvider.ResetCachedIsConnectedResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {WinIdProperty}", true, PluginDatabase.PluginName);
            }
        }
        private void ForceSteamAppId(Game game)
        {
            SuccessStoryForceSteamAppId ViewExtension = new SuccessStoryForceSteamAppId(game);
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow("Force Steam AppId", ViewExtension);
            windowExtension.ShowDialog();

            if (ViewExtension.SteamAppId.HasValue)
            {
                PluginSettings.Settings.ForcedSteamAppIds = PluginSettings.Settings.ForcedSteamAppIds ?? new Dictionary<Guid, int>();
                PluginSettings.Settings.ForcedSteamAppIds[game.Id] = ViewExtension.SteamAppId.Value;
                SavePluginSettings(PluginSettings.Settings);

                PluginDatabase.Refresh(game.Id);

                PlayniteApi.Dialogs.ShowMessage($"Steam AppId for {game.Name} has been set to {ViewExtension.SteamAppId.Value}.");
            }
        }

        #endregion


        #region Theme integration
        // Button on top panel
        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return TopPanelItem;
        }

        // List custom controls
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Name == "PluginButton")
            {
                return new PluginButton();
            }

            if (args.Name == "PluginViewItem")
            {
                return new PluginViewItem();
            }

            if (args.Name == "PluginProgressBar")
            {
                return new PluginProgressBar();
            }

            if (args.Name == "PluginCompactList")
            {
                return new PluginCompactList();
            }

            if (args.Name == "PluginCompactLocked")
            {
                return new PluginCompact { IsUnlocked = false };
            }

            if (args.Name == "PluginCompactUnlocked")
            {
                return new PluginCompact { IsUnlocked = true };
            }

            if (args.Name == "PluginChart")
            {
                return new PluginChart();
            }

            if (args.Name == "PluginUserStats")
            {
                return new PluginUserStats();
            }

            if (args.Name == "PluginList")
            {
                return new PluginList();
            }

            return null;
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            return new List<SidebarItem>
            {
                SidebarItem,
                SidebarRaItem
            };
        }
        #endregion


        #region Menus
        // To add new game menu items override GetGameMenuItems
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Game GameMenu = args.Games.First();
            List<Guid> Ids = args.Games.Select(x => x.Id).ToList();

            // TODO: for multiple games, either check if any of them could have achievements, or just assume so
            SuccessStoryDatabase.AchievementSource achievementSource = SuccessStoryDatabase.GetAchievementSource(PluginSettings.Settings, GameMenu);
            bool GameCouldHaveAchievements = achievementSource != SuccessStoryDatabase.AchievementSource.None;
            GameAchievements gameAchievements = PluginDatabase.Get(GameMenu, true);

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            if (!gameAchievements.IsIgnored)
            {
                if (GameCouldHaveAchievements)
                {
                    if (!PluginSettings.Settings.EnableOneGameView || (PluginSettings.Settings.EnableOneGameView && gameAchievements.HasData))
                    {
                        // Show list achievements for the selected game
                        // TODO: disable when selecting multiple games?
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = ResourceProvider.GetString("LOCSuccessStoryViewGame"),
                            Action = (gameMenuItem) =>
                            {
                                dynamic ViewExtension = null;
                                PluginDatabase.IsViewOpen = true;

                                WindowOptions windowOptions = new WindowOptions
                                {
                                    ShowMinimizeButton = false,
                                    ShowMaximizeButton = false,
                                    ShowCloseButton = true,
                                    CanBeResizable = false,
                                    Height = 800,
                                    Width = 1110
                                };

                                if (PluginDatabase.PluginSettings.Settings.EnableOneGameView)
                                {
                                    if ((PluginDatabase.GameContext.Name.IsEqual("overwatch") || PluginDatabase.GameContext.Name.IsEqual("overwatch 2")) && (PluginDatabase.GameContext.Source?.Name?.IsEqual("battle.net") ?? false))
                                    {
                                        ViewExtension = new SuccessStoryOverwatchView(GameMenu);
                                    }
                                    else if (PluginSettings.Settings.EnableGenshinImpact && GameMenu.Name.IsEqual("Genshin Impact"))
                                    {
                                        ViewExtension = new SuccessStoryCategoryView(GameMenu);
                                    }
                                    else if (PluginSettings.Settings.EnableGuildWars2 && GameMenu.Name.IsEqual("Guild Wars 2"))
                                    {
                                        ViewExtension = new SuccessStoryCategoryView(GameMenu);
                                    }
                                    else
                                    {
                                        ViewExtension = GameMenu.PluginId == PlayniteTools.GetPluginId(PlayniteTools.ExternalPlugin.SteamLibrary) && PluginSettings.Settings.SteamGroupData
                                            ? (dynamic)new SuccessStoryCategoryView(GameMenu)
                                            : (dynamic)new SuccessStoryOneGameView(GameMenu);
                                    }
                                }
                                else
                                {
                                    ViewExtension = new SuccessView(false, GameMenu);
                                }

                                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                                _ = windowExtension.ShowDialog();
                                PluginDatabase.IsViewOpen = false;
                            }
                        });

                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = "-"
                        });
                    }

                    if (!gameAchievements.IsManual || (gameAchievements.IsManual && gameAchievements.HasData))
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = ResourceProvider.GetString("LOCCommonRefreshGameData"),
                            Action = (gameMenuItem) =>
                            {
                                IsFromMenu = true;

                                if (Ids.Count == 1)
                                {
                                    PluginDatabase.Refresh(GameMenu.Id);
                                }
                                else
                                {
                                    PluginDatabase.Refresh(Ids);
                                }
                            }
                        });
                    }

                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                        Description = "Force Steam AppId",
                        Action = (mainMenuItem) =>
                        {
                            ForceSteamAppId(GameMenu);
                        }
                    });

                    if (PluginSettings.Settings.EnableRetroAchievements && achievementSource == SuccessStoryDatabase.AchievementSource.RetroAchievements)
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = ResourceProvider.GetString("LOCSuccessStoryForceRetroAchievementsId"),
                            Action = (gameMenuItem) =>
                            {
                                StringSelectionDialogResult stringSelectionDialogResult = API.Instance.Dialogs.SelectString("RetroAchievements", ResourceProvider.GetString("LOCSuccessStorySetRetroAchievementsId"), gameAchievements.RAgameID.ToString());
                                if (stringSelectionDialogResult.Result && int.TryParse(stringSelectionDialogResult.SelectedString, out int RAgameID))
                                {
                                    gameAchievements.RAgameID = RAgameID;
                                    PluginDatabase.Refresh(GameMenu.Id);
                                }
                            }
                        });
                    }

                    if (!gameAchievements.IsManual)
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = ResourceProvider.GetString("LOCSuccessStoryIgnored"),
                            Action = (mainMenuItem) =>
                            {
                                PluginDatabase.SetIgnored(gameAchievements);
                            }
                        });
                    }
                }

                if (PluginSettings.Settings.EnableManual && !GameMenu.Name.IsEqual("Genshin Impact"))
                {
                    if (!gameAchievements.HasData || !gameAchievements.IsManual)
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = ResourceProvider.GetString("LOCAddTitle"),
                            Action = (mainMenuItem) =>
                            {
                                PluginDatabase.Remove(GameMenu);
                                PluginDatabase.GetManual(GameMenu);
                            }
                        });
                    }
                    else if (gameAchievements.IsManual)
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = ResourceProvider.GetString("LOCEditGame"),
                            Action = (mainMenuItem) =>
                            {
                                SuccessStoryEditManual ViewExtension = new SuccessStoryEditManual(GameMenu);
                                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSuccessStory"), ViewExtension);
                                windowExtension.ShowDialog();
                            }
                        });

                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                            Description = ResourceProvider.GetString("LOCRemoveTitle"),
                            Action = (gameMenuItem) =>
                            {
                                Task TaskIntegrationUI = Task.Run(() =>
                                {
                                    PluginDatabase.Remove(GameMenu);
                                });
                            }
                        });
                    }
                }

                if (GameMenu.Name.IsEqual("Genshin Impact"))
                {
                    if (PluginSettings.Settings.EnableGenshinImpact)
                    {
                        if (!gameAchievements.HasData)
                        {
                            gameMenuItems.Add(new GameMenuItem
                            {
                                MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                                Description = ResourceProvider.GetString("LOCAddGenshinImpact"),
                                Action = (mainMenuItem) =>
                                {
                                    PluginDatabase.Remove(GameMenu);
                                    PluginDatabase.GetGenshinImpact(GameMenu);
                                }
                            });
                        }
                        else
                        {
                            gameMenuItems.Add(new GameMenuItem
                            {
                                MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                                Description = ResourceProvider.GetString("LOCEditGame"),
                                Action = (mainMenuItem) =>
                                {
                                    SuccessStoryEditManual ViewExtension = new SuccessStoryEditManual(GameMenu);
                                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSuccessStory"), ViewExtension);
                                    windowExtension.ShowDialog();
                                }
                            });

                            gameMenuItems.Add(new GameMenuItem
                            {
                                MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                                Description = ResourceProvider.GetString("LOCRemoveTitle"),
                                Action = (gameMenuItem) =>
                                {
                                    Task TaskIntegrationUI = Task.Run(() =>
                                    {
                                        PluginDatabase.Remove(GameMenu);
                                    });
                                }
                            });
                        }
                    }
                }

                if (achievementSource == SuccessStoryDatabase.AchievementSource.Local && gameAchievements.HasData && !gameAchievements.IsManual)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                        Description = ResourceProvider.GetString("LOCCommonDeleteGameData"),
                        Action = (gameMenuItem) =>
                        {
                            Task TaskIntegrationUI = Task.Run(() =>
                            {
                                PluginDatabase.Remove(GameMenu.Id);
                            });
                        }
                    });
                }
            }
            else
            {
                if (GameCouldHaveAchievements)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                        Description = ResourceProvider.GetString("LOCSuccessStoryNotIgnored"),
                        Action = (mainMenuItem) =>
                        {
                            PluginDatabase.SetIgnored(gameAchievements);
                        }
                    });
                }
            }

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                Description = "-"
            });
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = ResourceProvider.GetString("LOCSuccessStory"),
                Description = "Test",
                Action = (mainMenuItem) =>
                {

                }
            });
#endif
            return gameMenuItems;
        }

        // To add new main menu items override GetMainMenuItems
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            string MenuInExtensions = string.Empty;
            if (PluginSettings.Settings.MenuInExtensions)
            {
                MenuInExtensions = "@";
            }

            List<MainMenuItem> mainMenuItems = new List<MainMenuItem>
            {
                // Show list achievements for all games
                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = ResourceProvider.GetString("LOCSuccessStoryViewGames"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.IsViewOpen = true;
                        SuccessView ViewExtension = new SuccessView();

                        WindowOptions windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            CanBeResizable = true,
                            Width = 1100,
                            Height = 800
                        };

                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                        windowExtension.ShowDialog();
                        PluginDatabase.IsViewOpen = false;
                    }
                }
            };

            if (PluginSettings.Settings.EnableRetroAchievementsView && PluginSettings.Settings.EnableRetroAchievements)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = ResourceProvider.GetString("LOCSuccessStoryViewGames") + " - RetroAchievements",
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.IsViewOpen = true;

                        SuccessView ViewExtension = PluginSettings.Settings.EnableRetroAchievementsView && PlayniteTools.IsGameEmulated(PluginDatabase.GameContext)
                            ? new SuccessView(true, PluginDatabase.GameContext)
                            : new SuccessView(false, PluginDatabase.GameContext);

                        WindowOptions windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            CanBeResizable = true,
                            Width = 1100,
                            Height = 800
                        };

                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                        windowExtension.ShowDialog();
                        PluginDatabase.IsViewOpen = false;
                    }
                });
            }

            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                Description = "-"
            });

            // Download missing data for all game in database
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                Description = ResourceProvider.GetString("LOCCommonDownloadPluginData"),
                Action = (mainMenuItem) =>
                {
                    IsFromMenu = true;
                    PluginDatabase.GetSelectData();
                    IsFromMenu = false;
                }
            });

            if (PluginDatabase.PluginSettings.Settings.EnableManual)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = "-"
                });

                // Refresh rarity data for manual achievements
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = ResourceProvider.GetString("LOCSsRefreshRaretyManual"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RefreshRarety();
                    }
                });

                // Refresh estimate time data for manual achievements
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = ResourceProvider.GetString("LOCSsRefreshEstimateTimeManual"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RefreshEstimateTime();
                    }
                });
            }

            if (PluginDatabase.PluginSettings.Settings.EnableTag)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = "-"
                });

                // Add tag for selected game in database if data exists
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = ResourceProvider.GetString("LOCCommonAddTPlugin"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagSelectData();
                    }
                });
                // Add tag for all games
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = ResourceProvider.GetString("LOCCommonAddAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagAllGame();
                    }
                });
                // Remove tag for all game in database
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                    Description = ResourceProvider.GetString("LOCCommonRemoveAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RemoveTagAllGame();
                    }
                });
            }


#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                Description = "-"
            });
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSuccessStory"),
                Description = "Test",
                Action = (mainMenuItem) =>
                {

                }
            });
#endif

            return mainMenuItems;
        }
        #endregion


        #region Game event
        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            try
            {
                if (args.NewValue?.Count == 1 && PluginDatabase.IsLoaded)
                {
                    PluginDatabase.GameContext = args.NewValue[0];
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
                else
                {
                    _ = Task.Run(() =>
                    {
                        _ = SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);
                        _ = Application.Current.Dispatcher.BeginInvoke((Action)delegate
                        {
                            if (args.NewValue?.Count == 1)
                            {
                                PluginDatabase.GameContext = args.NewValue[0];
                                PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        // Add code to be executed when game is finished installing.
        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            if (PluginDatabase.PluginSettings.Settings.AutoImportOnInstalled)
            {
                _ = PluginDatabase.RefreshData(args.Game);
            }
        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            TaskIsPaused = true;
        }

        // Add code to be executed when game is started running.
        public override void OnGameStarted(OnGameStartedEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            TaskIsPaused = false;
            _ = PluginDatabase.RefreshData(args.Game);
        }
        #endregion


        #region Application event
        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // StoreAPI intialization
            SteamApi = new SteamApi(PluginDatabase.PluginName);
            SteamApi.SetLanguage(API.Instance.ApplicationSettings.Language);
            if (PluginDatabase.PluginSettings.Settings.EnableSteam)
            {
                _ = SteamApi.CurrentAccountInfos;
                if (PluginDatabase.PluginSettings.Settings.SteamApiSettings.UseAuth)
                {
                    SteamApi.CurrentAccountInfos.IsPrivate = true;
                }
            }

            EpicApi = new EpicApi(PluginDatabase.PluginName);
            EpicApi.SetLanguage(API.Instance.ApplicationSettings.Language);
            if (PluginDatabase.PluginSettings.Settings.EnableEpic)
            {
                _ = EpicApi.CurrentAccountInfos;
                if (PluginDatabase.PluginSettings.Settings.EpicSettings.UseAuth)
                {
                    EpicApi.CurrentAccountInfos.IsPrivate = true;
                }
            }


            Task.Run(() =>
            {
                Thread.Sleep(10000);
                PreventLibraryUpdatedOnStart = false;
            });

            // TODO - Removed for Playnite 11
            if (!PluginSettings.Settings.PurgeImageCache)
            {
                PluginDatabase.ClearCache();
                PluginSettings.Settings.PurgeImageCache = true;
                SavePluginSettings(PluginSettings.Settings);
            }

            // TODO TEMP
            string fileMD5List = PluginDatabase.Paths.PluginUserDataPath + "\\RA_MD5List.json";
            FileSystem.DeleteFile(fileMD5List);
            if (PluginSettings.Settings.DeleteOldRaConsole)
            {
                for (int i = 0; i < 100; i++)
                {
                    string fileConsoles = PluginDatabase.Paths.PluginUserDataPath + "\\RA_Games_" + i + ".json";
                    FileSystem.DeleteFile(fileConsoles);
                }

                PluginSettings.Settings.DeleteOldRaConsole = false;
                SavePluginSettings(PluginSettings.Settings);
            }

            // TODO TEMP
            if (!PluginSettings.Settings.IsRaretyUpdate)
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"{PluginDatabase.PluginName} - {ResourceProvider.GetString("LOCCommonProcessing")}")
                {
                    Cancelable = false,
                    IsIndeterminate = false
                };

                _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                {
                    try
                    {
                        _ = SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);
                        PluginDatabase.Database.Items.ForEach(x =>
                        {
                            x.Value.SetRaretyIndicator();
                            PluginDatabase.Database.SaveItemData(x.Value);
                        });

                        PluginSettings.Settings.IsRaretyUpdate = true;
                        SavePluginSettings(PluginSettings.Settings);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, true);
                    }

                }, globalProgressOptions);
            }


            // Cache images
            if (PluginSettings.Settings.EnableImageCache)
            {
                CancellationToken ct = TokenSource.Token;
                Task TaskCacheImage = Task.Run(() =>
                {
                    // Wait Playnite & extension database are loaded
                    _ = SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);
                    _ = SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

                    IEnumerable<GameAchievements> db = PluginDatabase.Database.Where(x => x.HasAchievements && !x.ImageIsCached);
                    int aa = db.Count();
#if DEBUG
                    Common.LogDebug(true, $"TaskCacheImage - {db.Count()} - Start");
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
#endif
                    db.ForEach(x =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        x.Items.ForEach(y =>
                        {
                            if (ct.IsCancellationRequested)
                            {
                                return;
                            }

                            try
                            {
                                if (!y.ImageLockedIsCached)
                                {
                                    string a = y.ImageLocked;
                                }
                                if (!y.ImageLockedIsCached)
                                {
                                    string b = y.ImageUnlocked;
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, true, $"Error on TaskCacheImage");
                            }
                        });
                    });

                    if (ct.IsCancellationRequested)
                    {
                        Logger.Info($"TaskCacheImage - IsCancellationRequested");
                        return;
                    }

#if DEBUG
                    stopwatch.Stop();
                    TimeSpan ts = stopwatch.Elapsed;
                    Common.LogDebug(true, $"TaskCacheImage() - End - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
#endif
                }, TokenSource.Token);
            }


            // QuickSearch support
            try
            {
                string icon = Path.Combine(PluginDatabase.Paths.PluginPath, "Resources", "star.png");

                SubItemsAction SsSubItemsAction = new SubItemsAction() { Action = () => { }, Name = "", CloseAfterExecute = false, SubItemSource = new QuickSearchItemSource() };
                CommandItem SsCommand = new CommandItem(PluginDatabase.PluginName, new List<CommandAction>(), ResourceProvider.GetString("LOCSsQuickSearchDescription"), icon);
                SsCommand.Keys.Add(new CommandItemKey() { Key = "ss", Weight = 1 });
                SsCommand.Actions.Add(SsSubItemsAction);
                QuickSearch.QuickSearchSDK.AddCommand(SsCommand);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }


            // Check Exophase if use localised achievements
            if (PluginSettings.Settings.UseLocalised)
            {
                Task.Run(() =>
                {
                    ExophaseAchievements exophaseAchievements = new ExophaseAchievements();
                    if (!exophaseAchievements.IsConnected())
                    {
                        Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                        {
                            Logger.Warn($"Exophase is disconnected");
                            string message = string.Format(ResourceProvider.GetString("LOCCommonStoresNoAuthenticate"), "Exophase");
                            API.Instance.Notifications.Add(new NotificationMessage(
                                $"{PluginDatabase.PluginName}-Exophase-disconnected",
                                $"{PluginDatabase.PluginName}\r\n{message}",
                                NotificationType.Error,
                                () => PluginDatabase.Plugin.OpenSettingsView()
                            ));
                        });
                    }
                });
            }


            // Initialize list console for RA
            if (PluginSettings.Settings.EnableRetroAchievements)
            {
                Task.Run(() =>
                {
                    List<RaConsole> ra_Consoles = RetroAchievements.GetConsoleIDs();
                    if (ra_Consoles == null)
                    {
                        Thread.Sleep(2000);
                        ra_Consoles = RetroAchievements.GetConsoleIDs();
                    }

                    ra_Consoles.ForEach(x =>
                    {
                        if (!PluginSettings.Settings.RaConsoleAssociateds.Any(y => y.RaConsoleId == x.ID))
                        {
                            // Add new RaConsole
                            PluginSettings.Settings.RaConsoleAssociateds.Add(new RaConsoleAssociated
                            {
                                RaConsoleId = x.ID,
                                RaConsoleName = x.Name,
                                Platforms = new List<Platform>()
                            });

                            // Search and add platform
                            API.Instance.Database.Platforms.ForEach(z =>
                            {
                                int RaConsoleId = RetroAchievements.FindConsole(z.Name);
                                if (RaConsoleId == x.ID)
                                {
                                    PluginSettings.Settings.RaConsoleAssociateds.Find(y => y.RaConsoleId == RaConsoleId).Platforms.Add(new Platform { Id = z.Id });
                                }
                            });
                        }
                    });

                    PluginSettings.Settings.RaConsoleAssociateds = PluginSettings.Settings.RaConsoleAssociateds.OrderBy(x => x.RaConsoleName).ToList();

                    Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                    {
                        SavePluginSettings(PluginSettings.Settings);
                    });
                });
            }
        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            TokenSource.Cancel();
        }
        #endregion


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (PluginSettings.Settings.AutoImport && !PreventLibraryUpdatedOnStart)
            {
                PluginDatabase.RefreshRecent();
                PluginSettings.Settings.LastAutoLibUpdateAssetsDownload = DateTime.Now;
                SavePluginSettings(PluginSettings.Settings);
            }
        }


        #region Settings
        public override ISettings GetSettings(bool firstRunSettings)
        {
            return PluginSettings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SuccessStorySettingsView(this);
        }
        #endregion
    }
}
