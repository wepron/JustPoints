using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;

namespace JustPoints
{
    public class JustPointsSettings : AttributeGlobalSettings<JustPointsSettings>
    {
        public override string Id => "JustPointsSettings";
        public override string DisplayName => "Just Points";
        public override string FormatType => "json";

        // =============================================================
        // MAIN
        // =============================================================

        private bool _enableMod = true;
        [SettingPropertyGroup("{=JP_GroupMain}Main", GroupOrder = -1)]
        [SettingPropertyBool("{=JP_EnableMod}Enable mod", Order = 0, RequireRestart = false, HintText = "{=JP_EnableMod_Hint}Master switch for all features.")]
        public bool EnableMod
        {
            get => _enableMod;
            set { if (_enableMod != value) { _enableMod = value; OnPropertyChanged(nameof(EnableMod)); } }
        }

        // =============================================================
        // FOCUS POINTS
        // =============================================================

        private bool _enablePlayerFocus = false;
        [SettingPropertyGroup("{=JP_GroupFocus}Focus Points", GroupOrder = 10)]
        [SettingPropertyBool("{=JP_EnablePlayerFocus}Enable (Player)", Order = 0, RequireRestart = false, HintText = "{=JP_EnablePlayerFocus_Hint}Enable focus points from kills for player.")]
        public bool EnablePlayerFocus
        {
            get => _enablePlayerFocus;
            set { if (_enablePlayerFocus != value) { _enablePlayerFocus = value; OnPropertyChanged(nameof(EnablePlayerFocus)); } }
        }

        [SettingPropertyGroup("{=JP_GroupFocus}Focus Points")]
        [SettingPropertyInteger("{=JP_PlayerKillsPerFocus}Kills per focus point (Player)", 1, 100000, Order = 1, RequireRestart = false, HintText = "{=JP_PlayerKillsPerFocus_Hint}Kills required to gain 1 focus point for player.")]
        public int PlayerKillsPerFocus { get; set; } = 500;

        private bool _enableFamilyFocus = false;
        [SettingPropertyGroup("{=JP_GroupFocus}Focus Points")]
        [SettingPropertyBool("{=JP_EnableFamilyFocus}Enable (Family)", Order = 2, RequireRestart = false, HintText = "{=JP_EnableFamilyFocus_Hint}Enable focus points from kills for family members.")]
        public bool EnableFamilyFocus
        {
            get => _enableFamilyFocus;
            set { if (_enableFamilyFocus != value) { _enableFamilyFocus = value; OnPropertyChanged(nameof(EnableFamilyFocus)); } }
        }

        [SettingPropertyGroup("{=JP_GroupFocus}Focus Points")]
        [SettingPropertyInteger("{=JP_FamilyKillsPerFocus}Kills per focus point (Family)", 1, 100000, Order = 3, RequireRestart = false, HintText = "{=JP_FamilyKillsPerFocus_Hint}Kills required to gain 1 focus point for family members.")]
        public int FamilyKillsPerFocus { get; set; } = 500;

        private bool _enableCompanionFocus = false;
        [SettingPropertyGroup("{=JP_GroupFocus}Focus Points")]
        [SettingPropertyBool("{=JP_EnableCompanionFocus}Enable (Companions)", Order = 4, RequireRestart = false, HintText = "{=JP_EnableCompanionFocus_Hint}Enable focus points from kills for companions.")]
        public bool EnableCompanionFocus
        {
            get => _enableCompanionFocus;
            set { if (_enableCompanionFocus != value) { _enableCompanionFocus = value; OnPropertyChanged(nameof(EnableCompanionFocus)); } }
        }

        [SettingPropertyGroup("{=JP_GroupFocus}Focus Points")]
        [SettingPropertyInteger("{=JP_CompanionKillsPerFocus}Kills per focus point (Companions)", 1, 100000, Order = 5, RequireRestart = false, HintText = "{=JP_CompanionKillsPerFocus_Hint}Kills required to gain 1 focus point for companions.")]
        public int CompanionKillsPerFocus { get; set; } = 500;

        // =============================================================
        // ATTRIBUTE POINTS
        // =============================================================

        private bool _enablePlayerAttribute = false;
        [SettingPropertyGroup("{=JP_GroupAttribute}Attribute Points", GroupOrder = 20)]
        [SettingPropertyBool("{=JP_EnablePlayerAttribute}Enable (Player)", Order = 0, RequireRestart = false, HintText = "{=JP_EnablePlayerAttribute_Hint}Enable attribute points from kills for player.")]
        public bool EnablePlayerAttribute
        {
            get => _enablePlayerAttribute;
            set { if (_enablePlayerAttribute != value) { _enablePlayerAttribute = value; OnPropertyChanged(nameof(EnablePlayerAttribute)); } }
        }

        [SettingPropertyGroup("{=JP_GroupAttribute}Attribute Points")]
        [SettingPropertyInteger("{=JP_PlayerKillsPerAttribute}Kills per attribute point (Player)", 1, 100000, Order = 1, RequireRestart = false, HintText = "{=JP_PlayerKillsPerAttribute_Hint}Kills required to gain 1 attribute point for player.")]
        public int PlayerKillsPerAttribute { get; set; } = 2000;

        private bool _enableFamilyAttribute = false;
        [SettingPropertyGroup("{=JP_GroupAttribute}Attribute Points")]
        [SettingPropertyBool("{=JP_EnableFamilyAttribute}Enable (Family)", Order = 2, RequireRestart = false, HintText = "{=JP_EnableFamilyAttribute_Hint}Enable attribute points from kills for family members.")]
        public bool EnableFamilyAttribute
        {
            get => _enableFamilyAttribute;
            set { if (_enableFamilyAttribute != value) { _enableFamilyAttribute = value; OnPropertyChanged(nameof(EnableFamilyAttribute)); } }
        }

        [SettingPropertyGroup("{=JP_GroupAttribute}Attribute Points")]
        [SettingPropertyInteger("{=JP_FamilyKillsPerAttribute}Kills per attribute point (Family)", 1, 100000, Order = 3, RequireRestart = false, HintText = "{=JP_FamilyKillsPerAttribute_Hint}Kills required to gain 1 attribute point for family members.")]
        public int FamilyKillsPerAttribute { get; set; } = 2000;

        private bool _enableCompanionAttribute = false;
        [SettingPropertyGroup("{=JP_GroupAttribute}Attribute Points")]
        [SettingPropertyBool("{=JP_EnableCompanionAttribute}Enable (Companions)", Order = 4, RequireRestart = false, HintText = "{=JP_EnableCompanionAttribute_Hint}Enable attribute points from kills for companions.")]
        public bool EnableCompanionAttribute
        {
            get => _enableCompanionAttribute;
            set { if (_enableCompanionAttribute != value) { _enableCompanionAttribute = value; OnPropertyChanged(nameof(EnableCompanionAttribute)); } }
        }

        [SettingPropertyGroup("{=JP_GroupAttribute}Attribute Points")]
        [SettingPropertyInteger("{=JP_CompanionKillsPerAttribute}Kills per attribute point (Companions)", 1, 100000, Order = 5, RequireRestart = false, HintText = "{=JP_CompanionKillsPerAttribute_Hint}Kills required to gain 1 attribute point for companions.")]
        public int CompanionKillsPerAttribute { get; set; } = 2000;

        // =============================================================
        // DICTIONARIES (хранятся в JSON-настройках MCM)
        // =============================================================

        public Dictionary<string, int> AccumulatedKills { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> AccumulatedKillsForFocus { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> AccumulatedKillsForAttribute { get; set; } = new Dictionary<string, int>();

        public int GetKillsForHero(Hero hero)
        {
            if (hero == null) return 0;
            return AccumulatedKills.TryGetValue(hero.StringId, out int kills) ? kills : 0;
        }

        public void SetKillsForHero(Hero hero, int kills)
        {
            if (hero == null) return;
            AccumulatedKills[hero.StringId] = kills;
        }

        public void AddKillsForHero(Hero hero, int killsToAdd)
        {
            if (hero == null || killsToAdd <= 0) return;
            SetKillsForHero(hero, GetKillsForHero(hero) + killsToAdd);
        }

        public int GetKillsForFocus(Hero hero)
        {
            if (hero == null) return 0;
            return AccumulatedKillsForFocus.TryGetValue(hero.StringId, out int kills) ? kills : 0;
        }

        public void SetKillsForFocus(Hero hero, int kills)
        {
            if (hero == null) return;
            AccumulatedKillsForFocus[hero.StringId] = kills;
        }

        public void AddKillsForFocus(Hero hero, int killsToAdd)
        {
            if (hero == null || killsToAdd <= 0) return;
            SetKillsForFocus(hero, GetKillsForFocus(hero) + killsToAdd);
        }

        public int GetKillsForAttribute(Hero hero)
        {
            if (hero == null) return 0;
            return AccumulatedKillsForAttribute.TryGetValue(hero.StringId, out int kills) ? kills : 0;
        }

        public void SetKillsForAttribute(Hero hero, int kills)
        {
            if (hero == null) return;
            AccumulatedKillsForAttribute[hero.StringId] = kills;
        }

        public void AddKillsForAttribute(Hero hero, int killsToAdd)
        {
            if (hero == null || killsToAdd <= 0) return;
            SetKillsForAttribute(hero, GetKillsForAttribute(hero) + killsToAdd);
        }

        // =============================================================
        // LOG (для ошибок MCM)
        // =============================================================

        public static void LogMcmError(string context, Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord", "Configs", "JustPoints");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "JustPoints_mcm_errors.log");
                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{context}] {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch { }
        }
    }
}