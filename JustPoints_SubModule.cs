using MCM.Abstractions.Base.Global;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace JustPoints
{
    public class JustPointsSubModule : MBSubModuleBase
    {
        public static JustPointsSubModule Current { get; private set; }

        // Копилка убийств за текущий бой. Сбрасывается в OnMapEventEnded.
        private readonly Dictionary<Hero, int> _killsInCurrentBattle = new Dictionary<Hero, int>();
        private bool _isInRealBattle = false;
        private bool _isGameFullyLoaded = false;

        private const string SaveFileName = "JustPointsData.txt";
        private const string FileMarker = "#JustPoints v1.0";

        private string DataFolder
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord", "Configs", "JustPoints");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private string DataFilePath => Path.Combine(DataFolder, SaveFileName);
        private string ErrorLogPath => Path.Combine(DataFolder, "JustPoints_errors.log");

        // =====================================================================
        // ЖИЗНЕННЫЙ ЦИКЛ
        // =====================================================================

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Current = this;
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            try
            {
                if (!(game.GameType is Campaign))
                    return;

                _isGameFullyLoaded = true;

                try { LoadData(); } catch (Exception ex) { LogError("LoadData", ex); }
                try { CleanupStaleHeroes(); } catch (Exception ex) { LogError("CleanupStaleHeroes", ex); }
                try { CleanupUnspendablePoints(); } catch (Exception ex) { LogError("CleanupUnspendablePoints (OnGameStart)", ex); }

                CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
                CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
                CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            }
            catch (Exception ex) { LogError("OnGameStart", ex); }
        }

        public override void OnGameEnd(Game game)
        {
            try { SaveData(); } catch (Exception ex) { LogError("SaveData (OnGameEnd)", ex); }
            try { base.OnGameEnd(game); } catch { }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            try
            {
                base.OnMissionBehaviorInitialize(mission);
                if (mission == null) return;

                if (mission.GetMissionBehavior<JustPointsMissionBehavior>() == null)
                    mission.AddMissionBehavior(new JustPointsMissionBehavior(this));
            }
            catch (Exception ex) { LogError("OnMissionBehaviorInitialize", ex); }
        }

        // =====================================================================
        // ФЛАГ «НАСТОЯЩИЙ БОЙ»
        // =====================================================================
        // Арена, турнир, тренировка — это Mission без MapEvent.
        // Настоящий бой (поле, штурм, вылазка, рейд, засада) — MapEvent
        // с участием MainParty.
        // =====================================================================

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (mapEvent == null) return;
                var mainParty = MobileParty.MainParty?.Party;
                if (mainParty == null) return;

                if (mapEvent.InvolvedParties.Contains(mainParty))
                {
                    _isInRealBattle = true;
                    _killsInCurrentBattle.Clear();
                }
            }
            catch (Exception ex) { LogError("OnMapEventStarted", ex); }
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                _isInRealBattle = false;

                var settings = JustPointsSettings.Instance;
                if (settings == null || !settings.EnableMod)
                {
                    _killsInCurrentBattle.Clear();
                    return;
                }

                if (mapEvent == null || _killsInCurrentBattle.Count == 0)
                {
                    _killsInCurrentBattle.Clear();
                    return;
                }

                Hero mainHero;
                try { mainHero = Hero.MainHero; }
                catch { mainHero = null; }

                // Складываем боевые убийства в общий счётчик
                foreach (var kvp in _killsInCurrentBattle)
                {
                    if (kvp.Key == null) continue;
                    if (kvp.Key.HeroState == Hero.CharacterStates.Dead) continue;
                    if (kvp.Value <= 0) continue;
                    settings.AddKillsForHero(kvp.Key, kvp.Value);
                }

                // Начисляем очки по каждому герою
                foreach (var kvp in _killsInCurrentBattle)
                {
                    Hero hero = kvp.Key;
                    if (hero == null) continue;
                    if (hero.HeroState == Hero.CharacterStates.Dead) continue;
                    if (hero.HeroDeveloper == null) continue;

                    bool isPlayer = mainHero != null && hero == mainHero;
                    bool isCompanion = !isPlayer && hero.IsWanderer;
                    bool isFamily = !isPlayer && !isCompanion;

                    ProcessHeroAwards(hero, isPlayer, isFamily, isCompanion, kvp.Value, settings);
                }

                _killsInCurrentBattle.Clear();
                CleanupUnspendablePoints();
                SaveData();
            }
            catch (Exception ex) { LogError("OnMapEventEnded", ex); }
        }

        // Вызывается из JustPointsMissionBehavior.OnAgentRemoved.
        // Считает ЛИЧНЫЕ убийства героев клана игрока. Только в настоящем бою.
        public void OnAgentRemovedInternal(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            try
            {
                if (!_isInRealBattle) return;
                if (agentState != AgentState.Killed) return;

                if (affectedAgent == null || affectorAgent == null) return;
                if (!affectedAgent.IsHuman || !affectorAgent.IsHuman) return;

                if (affectedAgent.Team == null || affectorAgent.Team == null) return;
                if (!affectorAgent.Team.IsEnemyOf(affectedAgent.Team)) return;

                // Кто убийца? — только человек, никогда не маунт.
                CharacterObject killerChar = affectorAgent.Character as CharacterObject;
                if (killerChar == null) return;
                Hero killerHero = killerChar.HeroObject;
                if (killerHero == null) return;

                if (!IsClanHeroForCounting(killerHero)) return;

                if (_killsInCurrentBattle.ContainsKey(killerHero))
                    _killsInCurrentBattle[killerHero]++;
                else
                    _killsInCurrentBattle[killerHero] = 1;
            }
            catch (Exception ex) { LogError("OnAgentRemovedInternal", ex); }
        }

        private bool IsClanHeroForCounting(Hero hero)
        {
            if (hero == null) return false;
            if (hero.HeroState == Hero.CharacterStates.Dead) return false;
            if (hero.Clan == null) return false;

            Hero mainHero;
            try { mainHero = Hero.MainHero; }
            catch { return false; }
            if (mainHero == null) return false;

            return hero.Clan == mainHero.Clan;
        }

        // =====================================================================
        // ПРОВЕРКА: ЕСТЬ ЛИ КУДА ТРАТИТЬ ОЧКИ
        // =====================================================================
        // Метод отвечает на простой вопрос: есть ли у героя ХОТЬ ОДИН
        // свободный слот, чтобы вложить очередное очко?
        //
        //   true  — есть куда вкладывать. Очки (даже «висящие») — легитимные,
        //           их не трогаем, мод продолжает начислять.
        //   false — все слоты забиты. Любые очки в Unspent* — «лишние»,
        //           их можно безопасно обнулить.
        //
        // ВАЖНО: в этой проверке НЕ сравниваем Unspent с remainingCapacity.
        // Иначе при равном количестве («2 очка, 2 слота») метод ошибочно
        // решал бы, что место кончилось, и сносил бы нормальные очки.
        // =====================================================================

        private static bool CanHeroSpendMoreFocus(Hero hero)
        {
            try
            {
                if (hero?.HeroDeveloper == null) return false;

                int maxFocusPerSkill = 5;
                try
                {
                    var devModel = Campaign.Current?.Models?.CharacterDevelopmentModel;
                    if (devModel != null)
                        maxFocusPerSkill = devModel.MaxFocusPerSkill;
                }
                catch { }

                int remainingCapacity = 0;
                foreach (var skill in Skills.All)
                {
                    if (skill == null) continue;
                    int current = hero.HeroDeveloper.GetFocus(skill);
                    int free = maxFocusPerSkill - current;
                    if (free > 0) remainingCapacity += free;
                }

                return remainingCapacity > 0;
            }
            catch { return true; }
        }

        private static bool CanHeroSpendMoreAttributes(Hero hero)
        {
            try
            {
                if (hero?.HeroDeveloper == null) return false;

                int maxAttr = 10;
                try
                {
                    var devModel = Campaign.Current?.Models?.CharacterDevelopmentModel;
                    if (devModel != null)
                        maxAttr = devModel.MaxAttribute;
                }
                catch { }

                // DefaultCharacterAttributes.All в API нет — перебираем шесть базовых
                // атрибутов вручную (Vigor, Control, Endurance, Cunning, Social, Intelligence).
                var attributes = new[]
                {
                    DefaultCharacterAttributes.Vigor,
                    DefaultCharacterAttributes.Control,
                    DefaultCharacterAttributes.Endurance,
                    DefaultCharacterAttributes.Cunning,
                    DefaultCharacterAttributes.Social,
                    DefaultCharacterAttributes.Intelligence,
                };

                int remainingCapacity = 0;
                foreach (var attr in attributes)
                {
                    if (attr == null) continue;
                    int current = hero.GetAttributeValue(attr);
                    int free = maxAttr - current;
                    if (free > 0) remainingCapacity += free;
                }

                return remainingCapacity > 0;
            }
            catch { return true; }
        }

        // Принудительно обнуляет уже выданные, но невложимые очки.
        // Вызывается при загрузке сейва и после каждого боя.
        //
        // Работает ТОЛЬКО когда CanHeroSpendMore* вернул false, то есть
        // свободных слотов ноль. Если хоть один слот есть — очки не трогаем:
        // это нормальные «нераспределённые», игрок сам их разложит.
        private void CleanupUnspendablePoints()
        {
            try
            {
                var settings = JustPointsSettings.Instance;
                if (settings == null || !settings.EnableMod) return;
                if (Campaign.Current == null) return;

                Hero mainHero;
                try { mainHero = Hero.MainHero; }
                catch { return; }
                if (mainHero?.Clan == null) return;

                foreach (var hero in mainHero.Clan.Heroes)
                {
                    if (hero == null) continue;
                    if (hero.HeroState == Hero.CharacterStates.Dead) continue;
                    if (hero.HeroDeveloper == null) continue;

                    // Фокус: свободных скилл-слотов ноль — обнуляем висящие очки.
                    if (!CanHeroSpendMoreFocus(hero) && hero.HeroDeveloper.UnspentFocusPoints > 0)
                    {
                        hero.HeroDeveloper.UnspentFocusPoints = 0;
                    }

                    // Атрибуты: все шесть на максимуме — обнуляем висящие очки.
                    if (!CanHeroSpendMoreAttributes(hero) && hero.HeroDeveloper.UnspentAttributePoints > 0)
                    {
                        hero.HeroDeveloper.UnspentAttributePoints = 0;
                    }
                }
            }
            catch (Exception ex) { LogError("CleanupUnspendablePoints", ex); }
        }

        // =====================================================================
        // НАЧИСЛЕНИЕ ОЧКОВ
        // =====================================================================

        private void ProcessHeroAwards(Hero hero, bool isPlayer, bool isFamily, bool isCompanion,
            int kills, JustPointsSettings settings)
        {
            try
            {
                if (hero == null || kills <= 0) return;
                if (hero.HeroDeveloper == null) return;

                // ================== FOCUS POINTS ==================
                bool focusEnabled;
                int focusThreshold;
                if (isPlayer) { focusEnabled = settings.EnablePlayerFocus; focusThreshold = settings.PlayerKillsPerFocus; }
                else if (isFamily) { focusEnabled = settings.EnableFamilyFocus; focusThreshold = settings.FamilyKillsPerFocus; }
                else { focusEnabled = settings.EnableCompanionFocus; focusThreshold = settings.CompanionKillsPerFocus; }

                if (focusEnabled && focusThreshold > 0)
                {
                    if (!CanHeroSpendMoreFocus(hero))
                    {
                        // Свободных слотов нет — обнуляем накопитель «убийств до очка»,
                        // чтобы он не рос впустую. Само начисление не производим.
                        if (settings.GetKillsForFocus(hero) != 0)
                            settings.SetKillsForFocus(hero, 0);
                    }
                    else
                    {
                        settings.AddKillsForFocus(hero, kills);
                        int totalFocusKills = settings.GetKillsForFocus(hero);
                        int desiredAward = totalFocusKills / focusThreshold;

                        if (desiredAward > 0)
                        {
                            hero.HeroDeveloper.UnspentFocusPoints += desiredAward;
                            int usedKills = desiredAward * focusThreshold;
                            int remaining = totalFocusKills - usedKills;
                            settings.SetKillsForFocus(hero, remaining);

                            try
                            {
                                TextObject msg = new TextObject("{=JP_FocusGained}Just Points: {HERO} gained {POINTS} focus point(s). Remaining kills: {REMAIN}.");
                                msg.SetTextVariable("HERO", hero.Name);
                                msg.SetTextVariable("POINTS", desiredAward);
                                msg.SetTextVariable("REMAIN", remaining);
                                if (Campaign.Current != null)
                                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
                            }
                            catch { }
                        }
                    }
                }

                // ================== ATTRIBUTE POINTS ==================
                bool attEnabled;
                int attThreshold;
                if (isPlayer) { attEnabled = settings.EnablePlayerAttribute; attThreshold = settings.PlayerKillsPerAttribute; }
                else if (isFamily) { attEnabled = settings.EnableFamilyAttribute; attThreshold = settings.FamilyKillsPerAttribute; }
                else { attEnabled = settings.EnableCompanionAttribute; attThreshold = settings.CompanionKillsPerAttribute; }

                if (attEnabled && attThreshold > 0)
                {
                    if (!CanHeroSpendMoreAttributes(hero))
                    {
                        if (settings.GetKillsForAttribute(hero) != 0)
                            settings.SetKillsForAttribute(hero, 0);
                    }
                    else
                    {
                        settings.AddKillsForAttribute(hero, kills);
                        int totalAttKills = settings.GetKillsForAttribute(hero);
                        int desiredAward = totalAttKills / attThreshold;

                        if (desiredAward > 0)
                        {
                            hero.HeroDeveloper.UnspentAttributePoints += desiredAward;
                            int usedKills = desiredAward * attThreshold;
                            int remaining = totalAttKills - usedKills;
                            settings.SetKillsForAttribute(hero, remaining);

                            try
                            {
                                TextObject msg = new TextObject("{=JP_AttributeGained}Just Points: {HERO} gained {POINTS} attribute point(s). Remaining kills: {REMAIN}.");
                                msg.SetTextVariable("HERO", hero.Name);
                                msg.SetTextVariable("POINTS", desiredAward);
                                msg.SetTextVariable("REMAIN", remaining);
                                if (Campaign.Current != null)
                                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("ProcessHeroAwards", ex); }
        }

        // =====================================================================
        // СОБЫТИЯ КАМПАНИИ
        // =====================================================================

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            try
            {
                _isGameFullyLoaded = true;
                LoadData();
                CleanupStaleHeroes();
                CleanupUnspendablePoints();
            }
            catch (Exception ex) { LogError("OnGameLoaded", ex); }
        }

        // =====================================================================
        // СПИСОК ЧЛЕНОВ КЛАНА (для автоочистки)
        // =====================================================================

        private List<string> GetCurrentClanMemberIds()
        {
            var result = new List<string>();
            try
            {
                if (!_isGameFullyLoaded || Campaign.Current == null) return result;
                Hero mainHero;
                try { mainHero = Hero.MainHero; }
                catch { return result; }
                if (mainHero == null || mainHero.Clan == null) return result;

                foreach (var hero in mainHero.Clan.Heroes)
                {
                    if (hero != null && hero.HeroState != Hero.CharacterStates.Dead
                        && !string.IsNullOrEmpty(hero.StringId))
                        result.Add(hero.StringId);
                }
            }
            catch { }
            return result;
        }

        // =====================================================================
        // СОХРАНЕНИЕ / ЗАГРУЗКА
        // =====================================================================

        private void LoadData()
        {
            try
            {
                var settings = JustPointsSettings.Instance;
                if (settings == null) return;

                settings.AccumulatedKills.Clear();
                settings.AccumulatedKillsForFocus.Clear();
                settings.AccumulatedKillsForAttribute.Clear();

                if (Campaign.Current == null) return;
                string currentCampaignId = Campaign.Current.UniqueGameId;
                if (string.IsNullOrEmpty(currentCampaignId)) return;

                if (!File.Exists(DataFilePath)) return;

                string[] lines = File.ReadAllLines(DataFilePath);
                if (lines.Length < 2) return;

                if (!string.Equals(lines[0].Trim(), FileMarker, StringComparison.Ordinal))
                    return;

                string campaignLine = lines[1];
                string[] campaignParts = campaignLine.Split('|');
                if (campaignParts.Length < 2 || campaignParts[0] != "CampaignId") return;
                string storedCampaignId = campaignParts[1];
                if (!string.Equals(storedCampaignId, currentCampaignId, StringComparison.Ordinal))
                    return;

                for (int i = 2; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length < 3) continue;

                    string type = parts[0];
                    string heroId = parts[1];
                    string value = parts[2];
                    if (string.IsNullOrEmpty(heroId)) continue;

                    switch (type)
                    {
                        case "Kills":
                            if (int.TryParse(value, out int kills))
                                settings.AccumulatedKills[heroId] = kills;
                            break;
                        case "Focus":
                            if (int.TryParse(value, out int fk))
                                settings.AccumulatedKillsForFocus[heroId] = fk;
                            break;
                        case "Attr":
                            if (int.TryParse(value, out int ak))
                                settings.AccumulatedKillsForAttribute[heroId] = ak;
                            break;
                    }
                }
            }
            catch (Exception ex) { LogError("LoadData", ex); }
        }

        private void SaveData()
        {
            try
            {
                var settings = JustPointsSettings.Instance;
                if (settings == null) return;
                if (Campaign.Current == null) return;

                string currentCampaignId = Campaign.Current.UniqueGameId;
                if (string.IsNullOrEmpty(currentCampaignId)) return;

                var lines = new List<string>
                {
                    FileMarker,
                    "CampaignId|" + currentCampaignId
                };

                foreach (var kvp in settings.AccumulatedKills)
                    if (!string.IsNullOrEmpty(kvp.Key))
                        lines.Add($"Kills|{kvp.Key}|{kvp.Value}");

                foreach (var kvp in settings.AccumulatedKillsForFocus)
                    if (!string.IsNullOrEmpty(kvp.Key))
                        lines.Add($"Focus|{kvp.Key}|{kvp.Value}");

                foreach (var kvp in settings.AccumulatedKillsForAttribute)
                    if (!string.IsNullOrEmpty(kvp.Key))
                        lines.Add($"Attr|{kvp.Key}|{kvp.Value}");

                AtomicWriteAllLines(DataFilePath, lines);
            }
            catch (Exception ex) { LogError("SaveData", ex); }
        }

        private static void AtomicWriteAllLines(string path, IEnumerable<string> lines)
        {
            try
            {
                string tmp = path + ".tmp";
                File.WriteAllLines(tmp, lines);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch { }
        }

        // Убираем данные о героях, которых больше нет в клане.
        private void CleanupStaleHeroes()
        {
            try
            {
                var settings = JustPointsSettings.Instance;
                if (settings == null) return;

                var currentIds = GetCurrentClanMemberIds();
                if (currentIds.Count == 0) return;

                var staleKills = settings.AccumulatedKills.Keys
                    .Where(id => !currentIds.Contains(id)).ToList();
                foreach (var id in staleKills) settings.AccumulatedKills.Remove(id);

                var staleFocus = settings.AccumulatedKillsForFocus.Keys
                    .Where(id => !currentIds.Contains(id)).ToList();
                foreach (var id in staleFocus) settings.AccumulatedKillsForFocus.Remove(id);

                var staleAttr = settings.AccumulatedKillsForAttribute.Keys
                    .Where(id => !currentIds.Contains(id)).ToList();
                foreach (var id in staleAttr) settings.AccumulatedKillsForAttribute.Remove(id);

                if (staleKills.Count + staleFocus.Count + staleAttr.Count > 0)
                    SaveData();
            }
            catch (Exception ex) { LogError("CleanupStaleHeroes", ex); }
        }

        private void LogError(string context, Exception ex)
        {
            try
            {
                File.AppendAllText(ErrorLogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{context}] {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch { }
        }
    }

    // =========================================================================
    // MISSION BEHAVIOR — только для счётчика убийств
    // =========================================================================

    public class JustPointsMissionBehavior : MissionBehavior
    {
        private readonly JustPointsSubModule _subModule;

        public JustPointsMissionBehavior(JustPointsSubModule subModule)
        {
            _subModule = subModule;
        }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
            AgentState agentState, KillingBlow killingBlow)
        {
            try
            {
                base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
                _subModule?.OnAgentRemovedInternal(affectedAgent, affectorAgent, agentState, killingBlow);
            }
            catch { }
        }
    }
}