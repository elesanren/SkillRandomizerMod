using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkillTriggerMod
{
    [BepInPlugin("HardItemRandomizer.SkillTriggerMod", "Skill Trigger Mod", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance { get; private set; }

        public static ConfigEntry<int> RandomSeed { get; private set; }
        public static ConfigEntry<bool> ModEnabled { get; private set; }
        public static ConfigEntry<string> CustomPositionsJson { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            RandomSeed = Config.Bind<int>("General", "RandomSeed", 0, "随机种子 (0 表示随机)");
            ModEnabled = Config.Bind<bool>("General", "ModEnabled", true, "启用技能触发器模组");
            CustomPositionsJson = Config.Bind<string>("General", "CustomPositionsJson", "", "自定义触发器坐标 JSON（高级）");

            var config = new SkillTriggerConfig
            {
                Seed = RandomSeed.Value,
                Enabled = ModEnabled.Value,
                AutoDisableShrines = true,
                TriggerPositions = ParseTriggerPositions()
            };

            SkillTriggerAPI.Initialize(config);

            ModEnabled.SettingChanged += (s, e) =>
            {
                SkillTriggerAPI.SetEnabled(ModEnabled.Value);
                Config.Save();  // ← 添加
            };
            RandomSeed.SettingChanged += (s, e) =>
            {
                SkillTriggerAPI.SetSeed(RandomSeed.Value);
                Config.Save();  // ← 添加
            };
            CustomPositionsJson.SettingChanged += (s, e) =>
            {
                var newList = ParseTriggerPositions();
                SkillTriggerAPI.SetTriggerPositions(newList);
                Config.Save();  // ← 添加
            };

            StartCoroutine(WaitForGameReady());
        }

        private IEnumerator WaitForGameReady()
        {
            // 用 FindObjectOfType 轮询，避免访问 GameManager.instance getter
            //（getter 在 GameManager 未就绪时每次调用都会打 "Couldn't find a Game Manager" 错误）
            GameManager gm;
            while ((gm = UnityEngine.Object.FindObjectOfType<GameManager>()) == null)
                yield return new WaitForSeconds(0.25f);
            while (string.IsNullOrEmpty(gm.sceneName))
                yield return new WaitForSeconds(0.25f);
            yield return null;
            SkillTriggerAPI.MarkGameReady();
        }

        private List<TriggerPosition> ParseTriggerPositions()
        {
            var defaultPositions = new List<TriggerPosition>
            {
                new TriggerPosition("Mosstown_02", 86.922f, 52.568f, 0.004f, 0),
                new TriggerPosition("Crawl_05", 23.032f, 16.568f, 0.004f, 1),
                new TriggerPosition("Shellwood_10", 40.643f, 79.57f, 0.004f, 2),
                new TriggerPosition("Greymoor_22", 39.783f, 36.826f, 0.004f, 3),
                new TriggerPosition("Bone_East_05", 100.062f, 13.568f, 0.004f, 4),
                new TriggerPosition("Under_18", 26f, 13f, 0.004f, 5)
            };

            string json = CustomPositionsJson.Value?.Trim();
            if (string.IsNullOrEmpty(json)) return defaultPositions;

            try
            {
                var custom = Newtonsoft.Json.JsonConvert.DeserializeObject<List<TriggerPosition>>(json);
                if (custom != null && custom.Count > 0) return custom;
            }
            catch (Exception ex) { Log.LogError($"解析自定义坐标失败: {ex.Message}"); }
            return defaultPositions;
        }

        public static void ResetAllRecords()
        {
            var saveData = SilksongItemRandomizer.Plugin.SaveData;
            saveData.SkillTriggerRecords.Clear();
            SilksongItemRandomizer.Plugin.SaveGlobalData();
        }
    }
}