using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace SkillTriggerMod
{
    public static class SkillRandomizer
    {
        // 仅保留字段名 → 显示名的映射（用于降级或日志）
        private static readonly Dictionary<string, string> DisplayNames = new()
        {
            { "hasNeedleThrow",  "丝之矛" },
            { "hasThreadSphere", "灵丝风暴" },
            { "hasHarpoonDash",  "飞针" },
            { "hasSilkCharge",   "丝刃标" },
            { "hasSilkBomb",     "符文之怒" },
            { "hasSilkBossNeedle","苍白之爪" },
            { "hasNeedolin",     "丝忆弦针" },
            { "hasDash",         "疾风步" },
            { "hasBrolly",       "流浪者披风" },
            { "hasDoubleJump",   "雪绒披风" },
            { "hasChargeSlash",  "蓄力斩" },
            { "hasSuperJump",    "灵丝升腾" },
            { "hasWalljump",     "蛛攀术" }
        };

        private static readonly List<string> AllFields = DisplayNames.Keys.ToList();
        private static Random _rng;
        private static int _seed;
        private static Dictionary<string, Sprite> _icons = new();
        private static Sprite _fallback;
        private static bool _cacheBuilt;
        private static FieldInfo[] _cachedFieldInfos;  // 缓存反射 FieldInfo，避免 GiveRandomSkill 重复查找

        // 最佳图标名映射（用于查找图标）
        private static readonly Dictionary<string, string> BestPickNames = new()
        {
            { "hasNeedleThrow",     "Silk Spear" },
            { "hasThreadSphere",    "Thread Sphere" },
            { "hasHarpoonDash",     "prompt_hornet_silk_dash" },
            { "hasSilkCharge",      "Silk Charge" },
            { "hasSilkBomb",        "Silk Bomb" },
            { "hasSilkBossNeedle",  "Silk Boss Needle" },
            { "hasNeedolin",        "Needolin_Prompt" },
            { "hasDash",            "prompt_swiftstep" },
            { "hasBrolly",          "prompt_hornet_umbrella" },
            { "hasDoubleJump",      "Hornet_Double_Jump_Prompt" },
            { "hasChargeSlash",     "charge_dash_slash" },
            { "hasSuperJump",       "prompt_super_jump" },
            { "hasWalljump",        "Wall_Jump_Prompt" }
        };

        // 关键字已预转小写，避免 BuildIconCache 中反复 ToLower
        private static readonly Dictionary<string, string[]> FallbackKeywords = new()
        {
            { "hasNeedleThrow",     new[] { "silk spear", "silkspear", "needlethrow", "needle", "spear" } },
            { "hasThreadSphere",    new[] { "thread storm", "threadstorm", "threadsphere", "thread", "storm", "sphere" } },
            { "hasHarpoonDash",     new[] { "clawline", "harpoon dash", "harpoondash", "harpoon", "dash" } },
            { "hasSilkCharge",      new[] { "sharpdart", "silk charge", "silkcharge", "sharpdart", "silk charge" } },
            { "hasSilkBomb",        new[] { "rune rage", "runerage", "silk bomb", "silkbomb", "rune", "rage", "silk bomb" } },
            { "hasSilkBossNeedle",  new[] { "pale nails", "palenails", "silk boss needle", "silkbossneedle", "pale", "nail", "needle", "boss" } },
            { "hasNeedolin",        new[] { "needolin", "needolin" } },
            { "hasDash",            new[] { "swift step", "swiftstep", "dash", "dash", "swift", "step" } },
            { "hasBrolly",          new[] { "drifter's cloak", "drifterscloak", "drifter", "brolly", "umbrella", "brolly", "drifter", "cloak" } },
            { "hasDoubleJump",      new[] { "faydown cloak", "faydowncloak", "double jump", "doublejump", "faydown", "double", "jump" } },
            { "hasChargeSlash",     new[] { "needle strike", "needlestrike", "charge slash", "chargeslash", "needle strike", "charge", "slash" } },
            { "hasSuperJump",       new[] { "silk soar", "silksoar", "super jump", "superjump", "silk soar", "super", "jump" } },
            { "hasWalljump",        new[] { "cling grip", "clinggrip", "wall jump", "walljump", "cling", "grip", "wall", "jump" } }
        };

        public static void SetSeed(int seed)
        {
            _seed = seed;
            _rng = seed == 0 ? new Random() : new Random(seed);
            _cacheBuilt = false;
        }

        private static void BuildIconCache()
        {
            if (_cacheBuilt) return;
            _cacheBuilt = true;
            _icons.Clear();

            var allItems = Resources.FindObjectsOfTypeAll<SavedItem>();
            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();

            // 预构建按名字索引的字典，O(1) 精确查找替代 O(n) FirstOrDefault 线性扫描
            var itemByName = new Dictionary<string, SavedItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in allItems)
            {
                if (item && !string.IsNullOrEmpty(item.name) && !itemByName.ContainsKey(item.name))
                    itemByName[item.name] = item;
            }

            var spriteByName = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            foreach (var sprite in allSprites)
            {
                if (sprite && !string.IsNullOrEmpty(sprite.name) && !spriteByName.ContainsKey(sprite.name))
                    spriteByName[sprite.name] = sprite;
            }

            foreach (var field in AllFields)
            {
                bool found = false;

                // 精确名查找：O(1) 字典查找替代 O(n) FirstOrDefault
                if (BestPickNames.TryGetValue(field, out string bestName))
                {
                    if (itemByName.TryGetValue(bestName, out var exactItem))
                    {
                        try { var icon = exactItem.GetPopupIcon(); if (icon) { _icons[field] = icon; found = true; } }
                        catch { }
                    }
                    if (!found && spriteByName.TryGetValue(bestName, out var exactSprite))
                    {
                        _icons[field] = exactSprite; found = true;
                    }
                }

                // 模糊关键词查找：遍历字典值（只 ToLower 一次每条名字）
                if (!found && FallbackKeywords.TryGetValue(field, out string[] keywords))
                {
                    foreach (var item in itemByName.Values)
                    {
                        string nameLower = item.name.ToLower();
                        for (int k = 0; k < keywords.Length; k++)
                        {
                            if (nameLower.IndexOf(keywords[k], StringComparison.Ordinal) >= 0)
                            {
                                try { var icon = item.GetPopupIcon(); if (icon) { _icons[field] = icon; found = true; } }
                                catch { }
                                break;
                            }
                        }
                        if (found) break;
                    }
                    if (!found)
                    {
                        foreach (var sprite in spriteByName.Values)
                        {
                            string nameLower = sprite.name.ToLower();
                            for (int k = 0; k < keywords.Length; k++)
                            {
                                if (nameLower.IndexOf(keywords[k], StringComparison.Ordinal) >= 0)
                                {
                                    _icons[field] = sprite; found = true;
                                    break;
                                }
                            }
                            if (found) break;
                        }
                    }
                }

                if (!found) Plugin.Log.LogWarning($"✗ {field} 未找到图标");
            }

            // Fallback 图标查找
            if (_fallback == null)
            {
                if (itemByName.TryGetValue("Rosary", out var rosary))
                {
                    try { _fallback = rosary.GetPopupIcon(); } catch { }
                }
                if (_fallback == null)
                {
                    foreach (var kv in itemByName)
                    {
                        if (kv.Key.IndexOf("rosary", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try { _fallback = kv.Value.GetPopupIcon(); if (_fallback) break; } catch { }
                        }
                    }
                }
                if (_fallback == null)
                {
                    foreach (var kv in spriteByName)
                    {
                        if (kv.Key.IndexOf("rosary", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _fallback = kv.Value; break;
                        }
                    }
                }
            }
        }

        private static Sprite GetIcon(string field)
        {
            BuildIconCache();
            return _icons.TryGetValue(field, out Sprite icon) ? icon : _fallback;
        }

        // 直接调用开局面板的 GiveSkill
        public static void GiveRandomSkill()
        {
            try
            {
                var pd = PlayerData.instance;
                if (pd == null) return;

                // 缓存 FieldInfo 避免每次调用反射查找
                if (_cachedFieldInfos == null)
                {
                    var list = new List<FieldInfo>();
                    foreach (var fn in AllFields)
                    {
                        var fi = typeof(PlayerData).GetField(fn, BindingFlags.Instance | BindingFlags.Public);
                        if (fi != null && fi.FieldType == typeof(bool))
                            list.Add(fi);
                    }
                    _cachedFieldInfos = list.ToArray();
                }

                // 收集未获得的技能
                List<string> missing = new();
                foreach (var fi in _cachedFieldInfos)
                {
                    if (!(bool)fi.GetValue(pd))
                        missing.Add(fi.Name);
                }
                if (missing.Count == 0)
                {
                    GiveWallJump();
                    return;
                }

                string chosen = missing[(_rng ??= new Random()).Next(missing.Count)];
                // 调用开局面板的 GiveSkill
                StartingAbilityPicker.SkillRandomizer.GiveSkill(chosen);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"GiveRandomSkill 异常: {ex}");
            }
        }

        public static void GiveWallJump()
        {
            try
            {
                string fieldName = "hasWalljump";
                var pd = PlayerData.instance;
                var fi = typeof(PlayerData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (fi == null || (bool)fi.GetValue(pd)) return;
                StartingAbilityPicker.SkillRandomizer.GiveSkill(fieldName);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"GiveWallJump 异常: {ex}");
            }
        }

        public static void GiveHarpoonDash()
        {
            try
            {
                string fieldName = "hasHarpoonDash";
                var pd = PlayerData.instance;
                var fi = typeof(PlayerData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (fi == null || (bool)fi.GetValue(pd)) return;
                StartingAbilityPicker.SkillRandomizer.GiveSkill(fieldName);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"GiveHarpoonDash 异常: {ex}");
            }
        }
    }
}