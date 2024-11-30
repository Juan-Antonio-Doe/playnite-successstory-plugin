﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Playnite.SDK.Data;
using CommonPluginsShared;
using SuccessStory.Services;
using CommonPluginsShared.Converters;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Documents;
using System.Globalization;
using CommonPluginsControls.Controls;
using System.Windows.Media.Effects;

namespace SuccessStory.Models
{
    public class Achievement : ObservableObject
    {
        private SuccessStoryDatabase PluginDatabase => SuccessStory.PluginDatabase;

        private string _name;
        public string Name { get => _name; set => _name = value?.Trim(); }
        public string ApiName { get; set; } = string.Empty;
        public string Description { get; set; }
        public string UrlUnlocked { get; set; }
        public string UrlLocked { get; set; }

        // TODO
        private DateTime? _dateUnlocked;
        public DateTime? DateUnlocked
        {
            get => _dateUnlocked == default(DateTime) ? null : _dateUnlocked;
            set => _dateUnlocked = value;
        }

        public bool IsHidden { get; set; } = false;
        /// <summary>
        /// Rarity indicator
        /// </summary>
        public float Percent { get; set; } = 100;
        public float GamerScore { get; set; } = 0;

        [DontSerialize]
        public string ImageCategoryIcon
        {
            get
            {
                string ImagePath = Path.Combine(PluginDatabase.Paths.PluginPath, "Resources", CategoryIcon);
                return File.Exists(ImagePath) ? ImagePath : ImageSourceManagerPlugin.GetImagePath(CategoryIcon);
            }
        }

        public int CategoryOrder { get; set; }
        public string CategoryIcon { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ParentCategory { get; set; } = string.Empty;

        public string CategoryRpcs3 { get; set; } = string.Empty;
        public string CategoryShadPS4 { get; set; } = string.Empty;


        /// <summary>
        /// Image for unlocked achievement
        /// </summary>
        [DontSerialize]
        public string ImageUnlocked
        {
            get
            {
                string TempUrlUnlocked = UrlUnlocked;
                if (TempUrlUnlocked?.Contains("rpcs3", StringComparison.InvariantCultureIgnoreCase) ?? false)
                {
                    TempUrlUnlocked = Path.Combine(PluginDatabase.Paths.PluginUserDataPath, UrlUnlocked);
                    return TempUrlUnlocked;
                }
                if (TempUrlUnlocked?.Contains("shadps4", StringComparison.InvariantCultureIgnoreCase) ?? false)
                {
                    TempUrlUnlocked = Path.Combine(PluginDatabase.Paths.PluginUserDataPath, UrlUnlocked);
                    return TempUrlUnlocked;
                }
                if (TempUrlUnlocked?.Contains("hidden_trophy", StringComparison.InvariantCultureIgnoreCase) ?? false)
                {
                    TempUrlUnlocked = Path.Combine(PluginDatabase.Paths.PluginPath, "Resources", UrlUnlocked);
                    return TempUrlUnlocked;
                }
                if (TempUrlUnlocked?.Contains("GenshinImpact", StringComparison.InvariantCultureIgnoreCase) ?? false)
                {
                    TempUrlUnlocked = Path.Combine(PluginDatabase.Paths.PluginPath, "Resources", UrlUnlocked);
                    return TempUrlUnlocked;
                }
                if (TempUrlUnlocked?.Contains("default_icon", StringComparison.InvariantCultureIgnoreCase) ?? false)
                {
                    TempUrlUnlocked = Path.Combine(PluginDatabase.Paths.PluginPath, "Resources", UrlUnlocked);
                    return TempUrlUnlocked;
                }
                if ((TempUrlUnlocked?.Contains("steamcdn-a.akamaihd.net", StringComparison.InvariantCultureIgnoreCase) ?? false) && TempUrlUnlocked.Length < 75)
                {
                    TempUrlUnlocked = Path.Combine(PluginDatabase.Paths.PluginPath, "Resources", "default_icon.png");
                    return TempUrlUnlocked;
                }

                return ImageSourceManagerPlugin.GetImagePath(UrlUnlocked, 256);
            }
        }

        /// <summary>
        /// Image for locked achievement
        /// </summary>
        [DontSerialize]
        public string ImageLocked => UrlLocked != null && UrlLocked.Contains("steamcdn-a.akamaihd.net") && UrlLocked.Length < 75
                    ? ImageUnlocked
                    : !UrlLocked.IsNullOrEmpty() && UrlLocked != UrlUnlocked ? ImageSourceManagerPlugin.GetImagePath(UrlLocked, 256) : ImageUnlocked;


        [DontSerialize]
        public bool ImageUnlockedIsCached => HttpFileCachePlugin.FileWebIsCached(UrlUnlocked);
        [DontSerialize]
        public bool ImageLockedIsCached => HttpFileCachePlugin.FileWebIsCached(UrlLocked);


        /// <summary>
        /// Get the icon according to the achievement state
        /// </summary>
        [DontSerialize]
        public string Icon => IsUnlock ? ImageUnlocked : ImageLocked;

        /// <summary>
        /// Indicates if there is no locked icon
        /// </summary>
        [DontSerialize]
        public bool IsGray => !IsUnlock && ((UrlLocked != null && UrlLocked.Contains("steamcdn-a.akamaihd.net") && UrlLocked.Length < 75) || UrlLocked.IsNullOrEmpty() || !UrlUnlocked.IsNullOrEmpty() || UrlLocked == UrlUnlocked);

        [DontSerialize]
        public bool EnableRaretyIndicator => PluginDatabase.PluginSettings.Settings.EnableRaretyIndicator;

        [DontSerialize]
        public bool DisplayRaretyValue => !NoRarety && (!PluginDatabase.PluginSettings.Settings.EnableRaretyIndicator
                    ? PluginDatabase.PluginSettings.Settings.EnableRaretyIndicator
                    : PluginDatabase.PluginSettings.Settings.DisplayRarityValue);

        public bool NoRarety { get; set; } = false;

        [DontSerialize]
        public string NameWithDateUnlock
        {
            get
            {
                string NameWithDateUnlock = Name;
                if (DateWhenUnlocked != null)
                {
                    LocalDateTimeConverter converter = new LocalDateTimeConverter();
                    NameWithDateUnlock += " (" + converter.Convert(DateWhenUnlocked, null, null, CultureInfo.CurrentCulture) + ")";
                }
                return NameWithDateUnlock;
            }
        }

        [DontSerialize]
        public object AchToolTipCompactList
        {
            get
            {
                StackPanel stackPanel = new StackPanel();

                TextBlockTrimmed textBlockTrimmed = new TextBlockTrimmed
                {
                    Text = NameWithDateUnlock,
                    FontWeight = FontWeights.Bold
                };
                if (!IsUnlock && IsHidden && !PluginDatabase.PluginSettings.Settings.ShowHiddenTitle)
                {
                    textBlockTrimmed.Effect = new BlurEffect
                    {
                        Radius = 4,
                        KernelType = KernelType.Box
                    };
                }
                _ = stackPanel.Children.Add(textBlockTrimmed);

                if (PluginDatabase.PluginSettings.Settings.IntegrationCompactShowDescription)
                {
                    TextBlock textBlock = new TextBlock
                    {
                        Text = Description
                    };
                    if (!IsUnlock && IsHidden && !PluginDatabase.PluginSettings.Settings.ShowHiddenDescription)
                    {
                        textBlock.Effect = new BlurEffect
                        {
                            Radius = 4,
                            KernelType = KernelType.Box
                        };
                    }
                    _ = stackPanel.Children.Add(textBlock);
                }

                return stackPanel;
            }
        }

        [DontSerialize]
        public object AchToolTipCompactPartial
        {
            get
            {
                StackPanel stackPanel = new StackPanel();

                TextBlockTrimmed textBlockTrimmed = new TextBlockTrimmed
                {
                    Text = NameWithDateUnlock,
                    FontWeight = FontWeights.Bold
                };
                if (!IsUnlock && IsHidden && !PluginDatabase.PluginSettings.Settings.ShowHiddenTitle)
                {
                    textBlockTrimmed.Effect = new BlurEffect
                    {
                        Radius = 4,
                        KernelType = KernelType.Box
                    };
                }
                _ = stackPanel.Children.Add(textBlockTrimmed);

                if (PluginDatabase.PluginSettings.Settings.IntegrationCompactPartialShowDescription)
                {
                    TextBlock textBlock = new TextBlock
                    {
                        Text = Description
                    };
                    if (!IsUnlock && IsHidden && !PluginDatabase.PluginSettings.Settings.ShowHiddenDescription)
                    {
                        textBlock.Effect = new BlurEffect
                        {
                            Radius = 4,
                            KernelType = KernelType.Box
                        };
                    }
                    _ = stackPanel.Children.Add(textBlock);
                }

                return stackPanel;
            }
        }

        [DontSerialize]
        public bool IsUnlock => DateWhenUnlocked != null || DateUnlocked.ToString().Contains("1982");

        private bool isVisible = true;
        [DontSerialize]
        public bool IsVisible { get => isVisible; set => SetValue(ref isVisible, value); }

        [DontSerialize]
        public DateTime? DateWhenUnlocked
        {
            get => DateUnlocked == null || DateUnlocked == default || DateUnlocked.ToString().Contains("0001") || DateUnlocked.ToString().Contains("1982")
                    ? null
                    : (DateTime?)((DateTime)DateUnlocked).ToLocalTime();
            set => DateUnlocked = value;
        }

        [DontSerialize]
        public string DateWhenUnlockedString => (string)new LocalDateTimeConverter().Convert(DateWhenUnlocked, null, null, CultureInfo.CurrentCulture);


        public AchProgression Progression { get; set; }


        [DontSerialize]
        public string IconText => PluginDatabase.PluginSettings.Settings.IconLocked;
        [DontSerialize]
        public string IconCustom
        {
            get
            {
                if (PluginDatabase.PluginSettings.Settings.IconCustomOnlyMissing && IsGray)
                {
                    if (IsGray)
                    {
                        return PluginDatabase.PluginSettings.Settings.IconCustomLocked;
                    }
                }
                else
                {
                    return PluginDatabase.PluginSettings.Settings.IconCustomLocked;
                }

                return string.Empty;
            }
        }
    }

    public class AchProgression
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Value { get; set; }

        [DontSerialize]
        public string Progression => Value + " / " + Max;
    }
}
