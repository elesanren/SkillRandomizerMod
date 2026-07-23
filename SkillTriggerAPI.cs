using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SkillTriggerMod
{
    /// <summary>
    /// 触发器坐标配置
    /// </summary>
    [Serializable]
    public class TriggerPosition
    {
        public string sceneName;
        public Vector3 position;
        public int index;   // 用于记录 key，可自动分配

        public TriggerPosition() { }
        public TriggerPosition(string scene, float x, float y, float z, int idx = -1)
        {
            sceneName = scene;
            position = new Vector3(x, y, z);
            index = idx;
        }
    }

    /// <summary>
    /// 技能触发器模块的完整配置
    /// </summary>
    public class SkillTriggerConfig
    {
        public int Seed = 0;
        public bool Enabled = true;
        public List<TriggerPosition> TriggerPositions = new List<TriggerPosition>();
        public bool AutoDisableShrines = true;
    }

    /// <summary>
    /// 技能触发器模块的统一公开 API
    /// </summary>
    public static class SkillTriggerAPI
    {
        private static SkillTriggerConfig _cachedConfig;
        private static bool _pendingChanges = false;
        private static bool _isGameReady = false;
        private static bool _initialized = false;

        // 场景加载时创建的触发器对象（用于清理/重建）
        private static List<GameObject> _activeTriggers = new List<GameObject>();

        // 内部事件订阅标志
        private static bool _eventsSubscribed = false;

        // ========== 初始化与生命周期 ==========
        public static void Initialize(SkillTriggerConfig config)
        {
            if (_initialized) return;
            _cachedConfig = config ?? new SkillTriggerConfig();
            _initialized = true;
            _pendingChanges = true;
        }

        public static void MarkGameReady()
        {
            _isGameReady = true;
            if (_pendingChanges) ApplyPending();
        }

        // ========== 配置修改（缓存，延迟生效） ==========
        public static void SetEnabled(bool enabled) { _cachedConfig.Enabled = enabled; MarkPendingAndApply(); }
        public static void SetSeed(int seed) { _cachedConfig.Seed = seed; MarkPendingAndApply(); }
        public static void SetTriggerPositions(List<TriggerPosition> positions) { _cachedConfig.TriggerPositions = positions; MarkPendingAndApply(); }
        public static void SetAutoDisableShrines(bool auto) { _cachedConfig.AutoDisableShrines = auto; MarkPendingAndApply(); }

        public static void ApplyNow()
        {
            if (!_isGameReady && !_initialized) return;
            ApplyPending();
        }

        // ========== 查询方法 ==========
        public static bool IsEnabled() => _cachedConfig.Enabled;
        public static int GetSeed() => _cachedConfig.Seed;
        public static List<TriggerPosition> GetTriggerPositions() => new List<TriggerPosition>(_cachedConfig.TriggerPositions);
        public static bool IsAutoDisableShrines() => _cachedConfig.AutoDisableShrines;

        // ========== 运行时操作 ==========
        public static void ResetAllRecords()
        {
            var saveData = SilksongItemRandomizer.Plugin.SaveData;
            saveData.SkillTriggerRecords.Clear();
            SilksongItemRandomizer.Plugin.SaveGlobalData();
        }

        // ========== 内部实现 ==========
        private static void MarkPendingAndApply()
        {
            _pendingChanges = true;
            if (_isGameReady) ApplyPending();
        }

        private static void ApplyPending()
        {
            if (!_isGameReady && !_pendingChanges) return;

            // 同步种子到内部随机器
            SkillRandomizer.SetSeed(_cachedConfig.Seed);

            // 清理旧的触发器
            ClearAllTriggers();

            // 重新订阅场景加载事件（如果需要）
            if (_cachedConfig.Enabled && !_eventsSubscribed)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                _eventsSubscribed = true;
            }
            else if (!_cachedConfig.Enabled && _eventsSubscribed)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
                _eventsSubscribed = false;
            }

            // 如果当前场景已加载，立即应用当前场景的触发器
            if (_cachedConfig.Enabled && _isGameReady)
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (currentScene.isLoaded)
                {
                    if (_cachedConfig.AutoDisableShrines && IsItemRandomEnabled())
                        DisableShrinesInScene(currentScene);
                    CreateTriggersForScene(currentScene);
                }
            }

            _pendingChanges = false;
            Plugin.Instance.Config.Save();  // ← 添加
        }

        private static void ClearAllTriggers()
        {
            foreach (var trigger in _activeTriggers)
                if (trigger != null) UnityEngine.Object.Destroy(trigger);
            _activeTriggers.Clear();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!_cachedConfig.Enabled) return;

            if (_cachedConfig.AutoDisableShrines && IsItemRandomEnabled())
                DisableShrinesInScene(scene);

            CreateTriggersForScene(scene);
        }

        // 预计算小写关键词，避免每次调用 DisableShrinesInScene 重复遍历字符串
        private static readonly string[] _shrineKeywordsLower = { "bind orb", "shrine weaver ability", "weaver_shrine", "bellshrine", "dash shrine" };

        private static void DisableShrinesInScene(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string low = t.gameObject.name.ToLower();
                    for (int i = 0; i < _shrineKeywordsLower.Length; i++)
                    {
                        if (low.IndexOf(_shrineKeywordsLower[i], StringComparison.Ordinal) >= 0)
                        {
                            // GetComponent 替代 GetComponents，避免分配数组
                            var col2d = t.GetComponent<Collider2D>();
                            if (col2d) col2d.enabled = false;
                            var col = t.GetComponent<Collider>();
                            if (col) col.enabled = false;
                            var fsm = t.GetComponent<PlayMakerFSM>();
                            if (fsm) fsm.enabled = false;
                            break;
                        }
                    }
                }
            }
        }

        private static void CreateTriggersForScene(UnityEngine.SceneManagement.Scene scene)
        {
            var saveData = SilksongItemRandomizer.Plugin.SaveData;

            for (int idx = 0; idx < _cachedConfig.TriggerPositions.Count; idx++)
            {
                var posCfg = _cachedConfig.TriggerPositions[idx];
                if (posCfg.sceneName != scene.name) continue;

                int actualIdx = posCfg.index >= 0 ? posCfg.index : idx;
                string key = $"SkillTriggered_{scene.name}_{actualIdx}";
                if (saveData.SkillTriggerRecords.Contains(key)) continue;

                GameObject obj = new GameObject($"SkillTrigger_{scene.name}_{actualIdx}");
                obj.transform.position = posCfg.position;
                var box = obj.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(8f, 8f);
                var trigger = obj.AddComponent<SkillTrigger>();
                trigger.SetInfo(scene.name, actualIdx, key);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, scene);
                _activeTriggers.Add(obj);
                Plugin.Log.LogDebug($"触发器创建: {scene.name} 索引 {actualIdx}");
            }
        }

        // 缓存反射结果，避免每次场景加载重复 Type.GetType + GetProperty
        private static Type _cachedItemRandomType;
        private static PropertyInfo _cachedItemRandomProp;
        private static bool _itemRandomReflectionCached;

        private static bool IsItemRandomEnabled()
        {
            try
            {
                if (!_itemRandomReflectionCached)
                {
                    _itemRandomReflectionCached = true;
                    _cachedItemRandomType = Type.GetType("SilksongItemRandomizer.Plugin, SilksongItemRandomizer");
                    _cachedItemRandomProp = _cachedItemRandomType?.GetProperty("PublicItemRandomEnabled", BindingFlags.Public | BindingFlags.Static);
                }
                if (_cachedItemRandomProp != null) return (bool)_cachedItemRandomProp.GetValue(null);
            }
            catch { }
            return false;
        }

        // 内部方法供 SkillTrigger 调用记录触发
        internal static void RecordTriggered(string key)
        {
            var saveData = SilksongItemRandomizer.Plugin.SaveData;
            if (saveData.SkillTriggerRecords.Add(key))
                SilksongItemRandomizer.Plugin.SaveGlobalData();
        }
    }
}