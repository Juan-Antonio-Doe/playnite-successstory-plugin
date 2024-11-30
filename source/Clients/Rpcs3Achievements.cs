﻿using Playnite.SDK.Models;
using CommonPluginsShared;
using SuccessStory.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using CommonPlayniteShared.Common;
using System.Globalization;
using static CommonPluginsShared.PlayniteTools;
using System.Text.RegularExpressions;
using Paths = CommonPlayniteShared.Common.Paths;

namespace SuccessStory.Clients
{
    public class Rpcs3Achievements : GenericAchievements
    {
        public Rpcs3Achievements() : base("RPCS3")
        {

        }
                
        public override GameAchievements GetAchievements(Game game)
        {
            GameAchievements gameAchievements = SuccessStory.PluginDatabase.GetDefault(game);
            List<Achievement> AllAchievements = new List<Achievement>();


            if (IsConfigured())
            {
                List<string> TrophyDirectories = FindTrophyGameFolder(game);
                string TrophyFile = "TROPUSR.DAT";
                string TrophyFileDetails = "TROPCONF.SFM";


                // Directory control
                if (TrophyDirectories.Count == 0)
                {
                    Logger.Warn($"No Trophy directoy found for {game.Name}");
                    return gameAchievements;
                }

                foreach (string TrophyDirectory in TrophyDirectories)
                {
                    AllAchievements = new List<Achievement>();

                    string trophyFilePath = Paths.FixPathLength(Path.Combine(TrophyDirectory, TrophyFile));
                    string trophyFileDetailsPath = Paths.FixPathLength(Path.Combine(TrophyDirectory, TrophyFileDetails));

                    if (!File.Exists(trophyFilePath))
                    {
                        Logger.Warn($"File {TrophyFile} not found for {game.Name} in {trophyFilePath}");
                        continue;
                    }
                    if (!File.Exists(trophyFileDetailsPath))
                    {
                        Logger.Warn($"File {TrophyFileDetails} not found for {game.Name} in {trophyFileDetailsPath}");
                        continue;
                    }

                    
                    int TrophyCount = 0;
                    List<string> TrophyHexData = new List<string>();
                    

                    // Trophies details
                    XDocument TrophyDetailsXml = XDocument.Load(trophyFileDetailsPath);

                    string GameName = TrophyDetailsXml.Descendants("title-name").FirstOrDefault().Value.Trim();

                    // eFMann - Get all trophies, including those in groups                    
                    var groupsDict = TrophyDetailsXml.Descendants("group")
                        .ToDictionary(
                        g => g.Attribute("id")?.Value,
                        g => g.Element("name")?.Value
                        );

                    foreach (XElement TrophyXml in TrophyDetailsXml.Descendants("trophy"))
                    {
                        _ = int.TryParse(TrophyXml.Attribute("id").Value, out int TrophyDetailsId);
                        string TrophyType = TrophyXml.Attribute("ttype").Value;
                        string Name = TrophyXml.Element("name").Value;
                        string Description = TrophyXml.Element("detail").Value;

                        // Get group name from cache instead of querying XML
                        string groupId = TrophyXml.Attribute("gid")?.Value;
                        string groupName = groupId != null && groupsDict.ContainsKey(groupId)
                            ? groupsDict[groupId]
                            : null;

                        int Percent = 100;
                        float GamerScore = 15;
                        if (TrophyType.IsEqual("S"))
                        {
                            Percent = (int)PluginDatabase.PluginSettings.Settings.RarityUncommon;
                            GamerScore = 30;
                        }
                        if (TrophyType.IsEqual("G"))
                        {
                            Percent = (int)PluginDatabase.PluginSettings.Settings.RarityRare;
                            GamerScore = 90;
                        }
                        if (TrophyType.IsEqual("P"))
                        {
                            Percent = (int)PluginDatabase.PluginSettings.Settings.RarityUltraRare;
                            GamerScore = 180;
                        }

                        AllAchievements.Add(new Achievement
                        {
                            ApiName = string.Empty,
                            Name = Name,
                            Description = Description,
                            UrlUnlocked = CopyTrophyFile(TrophyDirectory, "TROP" + TrophyDetailsId.ToString("000") + ".png"),
                            UrlLocked = string.Empty,
                            DateUnlocked = default(DateTime),
                            Percent = Percent,
                            GamerScore = GamerScore,

                            CategoryRpcs3 = TrophyDirectories.Count > 1 ? GameName : null
                        });
                    }

                    
                    TrophyCount = AllAchievements.Count;


                    // Trophies data
                    byte[] TrophyByte = File.ReadAllBytes(trophyFilePath);
                    string hex = Tools.ToHex(TrophyByte);
                    List<string> splitHex = hex.Split(new[] { "0000000400000050000000", "0000000600000060000000" }, StringSplitOptions.None).ToList();
                    TrophyHexData = splitHex.Count >= TrophyCount
                        ? splitHex.GetRange(splitHex.Count - TrophyCount, TrophyCount)
                        : new List<string>();


                    /*
                    if (TrophyCount > 0 && splitHex.Count >= TrophyCount)
                    {
                        // Take only the entries we need from the end of the list
                        TrophyHexData = splitHex.Skip(splitHex.Count - TrophyCount).Take(TrophyCount).ToList();
                    } 
                    */

                    foreach (string HexData in TrophyHexData)
                    {
                        if (HexData.Length < 58) continue;

                        string stringHexId = HexData.Substring(0, 2);
                        int Id = (int)long.Parse(stringHexId, NumberStyles.HexNumber);

                        if (Id >= AllAchievements.Count) continue;

                        string Unlocked = HexData.Substring(18, 8);
                        bool IsUnlocked = Unlocked == "00000001";

                        if (IsUnlocked)
                        {
                            try
                            {
                                string dtHex = HexData.Substring(44, 14);
                                DateTime dt = new DateTime(long.Parse(dtHex, NumberStyles.AllowHexSpecifier) * 10L);
                                AllAchievements[Id].DateUnlocked = dt;
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, false, PluginDatabase.PluginName);
                                AllAchievements[Id].DateUnlocked = new DateTime(1982, 12, 15, 0, 0, 0, 0);
                            }
                        }
                    }

                    AllAchievements.ForEach(x => gameAchievements.Items.Add(x));
                }
            }
            else
            {
                ShowNotificationPluginNoConfiguration();
            }

            gameAchievements.SetRaretyIndicator();
            return gameAchievements;
        }


        #region Configuration
        public override bool ValidateConfiguration()
        {
            if (CachedConfigurationValidationResult == null)
            {
                CachedConfigurationValidationResult = IsConfigured();

                if (!(bool)CachedConfigurationValidationResult)
                {
                    ShowNotificationPluginNoConfiguration();
                }
            }
            else if (!(bool)CachedConfigurationValidationResult)
            {
                ShowNotificationPluginErrorMessage(ExternalPlugin.SuccessStory);
            }

            return (bool)CachedConfigurationValidationResult;
        }


        public override bool IsConfigured()
        {
            if (PluginDatabase.PluginSettings.Settings.Rpcs3InstallationFolder.IsNullOrEmpty())
            {
                Logger.Warn("No RPCS3 configured folder");
                return false;
            }

            string trophyPath = Path.Combine(PluginDatabase.PluginSettings.Settings.Rpcs3InstallationFolder, "trophy");
            trophyPath = Paths.FixPathLength(trophyPath);
            if (!Directory.Exists(trophyPath))
            {
                Logger.Warn($"No RPCS3 trophy folder in {PluginDatabase.PluginSettings.Settings.Rpcs3InstallationFolder}");
                return false;
            }

            return true;
        }

        public override bool EnabledInSettings()
        {
            return PluginDatabase.PluginSettings.Settings.EnableRpcs3Achievements;
        }
        #endregion


        #region RPCS3
        private List<string> FindTrophyGameFolder(Game game)
        {
            List<string> trophyGameFolder = new List<string>();

            List<string> foldersPath = new List<string> { PluginDatabase.PluginSettings.Settings.Rpcs3InstallationFolder };
            PluginDatabase.PluginSettings.Settings.Rpcs3InstallationFolders?.ForEach(x => foldersPath.Add(x.FolderPath));

            string path = API.Instance.ExpandGameVariables(game, Path.Combine(game.InstallDirectory, ".."), GetGameEmulator(game)?.InstallDir);
            List<string> file = Tools.FindFile(path, "TROPHY.TRP", true);

            if (file.Count() == 0)
            {
                Logger.Warn($"TROPHY.TRP not found for {game.Name}");
                return new List<string>();
            }
            else if (file.Count() > 1)
            {
                Logger.Warn($"TROPHY.TRP is multiple for {game.Name}");
                file.ForEach(x =>
                {
                    Logger.Warn(x);
                });
            }

            string fileTrophyTRP = file.First();
            string fileData = FileSystem.ReadFileAsStringSafe(fileTrophyTRP);
            Match match = Regex.Match(fileData, @"<npcommid>(.*?)<\/npcommid>", RegexOptions.IgnoreCase);
            string npcommid = match.Success ? match.Groups[1].Value : null;
            if (npcommid.IsNullOrEmpty())
            {
                Logger.Warn($"npcommid not found for {game.Name} in {fileTrophyTRP}");
            }

            foldersPath.ForEach(x =>
            {
                string trophyFolder = Paths.FixPathLength(Path.Combine(x, "trophy"));
                string folder = Paths.FixPathLength(Path.Combine(trophyFolder, npcommid));
                if (Directory.Exists(folder))
                {
                    trophyGameFolder.Add(folder);
                }
                else
                {
                    Logger.Warn($"Trophy folder not found: {folder}");
                }
            });

            return trophyGameFolder;
        }

        private string CopyTrophyFile(string trophyDirectory, string trophyFile)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(trophyDirectory);
                string NameFolder = di.Name;

                string parentDir = Paths.FixPathLength(Path.Combine(PluginDatabase.Paths.PluginUserDataPath, "rpcs3"));
                string dir = Paths.FixPathLength(Path.Combine(PluginDatabase.Paths.PluginUserDataPath, "rpcs3", NameFolder));

                FileSystem.CreateDirectory(parentDir);
                FileSystem.CreateDirectory(dir);

                string targetPath = Paths.FixPathLength(Path.Combine(PluginDatabase.Paths.PluginUserDataPath, "rpcs3", NameFolder, trophyFile));
                string sourcePath = Paths.FixPathLength(Path.Combine(trophyDirectory, trophyFile));

                // Only copy if target doesn't exist
                if (!File.Exists(targetPath))
                {
                    try
                    {
                        // Try to copy without deleting first
                        File.Copy(sourcePath, targetPath, false);
                    }
                    catch (IOException)
                    {
                        // If file is in use, wait briefly and try again
                        System.Threading.Thread.Sleep(100);
                        File.Copy(sourcePath, targetPath, false);
                    }
                }

                return Path.Combine("rpcs3", NameFolder, trophyFile);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                return string.Empty;
            }
        }
        #endregion
    }
}
