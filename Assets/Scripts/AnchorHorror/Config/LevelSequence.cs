// ------------------------------------------------------------
// LevelSequence.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡类型：关卡1（选择阶段）或关卡2 子场景（拾取阶段）。
    /// entries[0]=Level1Select，entries[1..N]=Level2Sub（ADR-1）。
    /// </summary>
    public enum LevelKind
    {
        Level1Select,
        Level2Sub,
    }

    /// <summary>
    /// 门类型：进入关卡2 或切换子场景（ADR-1）。
    /// EnterLevel2 仅 entries[0] 使用；SwitchSubScene 用于 entries[1..N]。
    /// </summary>
    public enum DoorKind
    {
        EnterLevel2,
        SwitchSubScene,
    }

    /// <summary>
    /// 关卡序列 SO：有序保存每关的 LevelData 与配套门配置（DoorSetting）。
    /// GameManager 持有此资产，按 _levelIndex 取当前关卡数据与门设置。
    /// 门位配置入本 SO（而非 LevelData），以零改 level-editor 文件为代价换取推进层与编辑层解耦（ADR-1/3）。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelSequence", menuName = "Ciga/AnchorHorror/LevelSequence")]
    public class LevelSequence : ScriptableObject
    {
        /// <summary>门的生成配置：出生坐标、精灵、交互提示文案。</summary>
        [Serializable]
        public class DoorSetting
        {
            [SerializeField] private Vector2 _spawn;
            [SerializeField] private Sprite _sprite;
            [SerializeField] private string _prompt = "按 E 进入下一关";

            /// <summary>门的世界坐标出生点。</summary>
            public Vector2 Spawn => _spawn;

            /// <summary>门的精灵图（可为 null，届时 SpriteRenderer 保持空白）。</summary>
            public Sprite Sprite => _sprite;

            /// <summary>靠近门时的提示文案。</summary>
            public string Prompt => _prompt;
        }

        /// <summary>序列中的单条关卡项：一个 LevelData + 对应的门配置 + 关卡类型 + 门类型（ADR-1）。</summary>
        [Serializable]
        public class Entry
        {
            [SerializeField] private LevelData _level;
            [SerializeField] private DoorSetting _door = new DoorSetting();
            [SerializeField] private LevelKind _kind = LevelKind.Level2Sub;
            [SerializeField] private DoorKind _doorKind = DoorKind.SwitchSubScene;

            /// <summary>本关的关卡数据资产。</summary>
            public LevelData Level => _level;

            /// <summary>本关通关后出现的门配置（末关无门，可忽略）。</summary>
            public DoorSetting Door => _door;

            /// <summary>关卡类型：关卡1选择 或 关卡2子场景。</summary>
            public LevelKind Kind => _kind;

            /// <summary>本关门类型：进入关卡2 或 切换子场景。</summary>
            public DoorKind DoorKind => _doorKind;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        /// <summary>关卡总数。</summary>
        public int Count => _entries != null ? _entries.Count : 0;

        /// <summary>取第 index 关的 LevelData；越界返回 null 并写日志。</summary>
        public LevelData GetLevel(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Count)
            {
                Debug.LogWarning($"[LevelSequence] GetLevel({index}) 越界（共 {Count} 关），返回 null。");
                return null;
            }

            return _entries[index].Level;
        }

        /// <summary>取第 index 关的门配置；越界返回 null 并写日志。</summary>
        public DoorSetting GetDoor(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Count)
            {
                Debug.LogWarning($"[LevelSequence] GetDoor({index}) 越界（共 {Count} 关），返回 null。");
                return null;
            }

            return _entries[index].Door;
        }

        /// <summary>取第 index 关的关卡类型；越界返回 Level2Sub 并写日志。</summary>
        public LevelKind GetKind(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Count)
            {
                Debug.LogWarning($"[LevelSequence] GetKind({index}) 越界（共 {Count} 关），返回 Level2Sub。");
                return LevelKind.Level2Sub;
            }

            return _entries[index].Kind;
        }

        /// <summary>取第 index 关的门类型；越界返回 SwitchSubScene 并写日志。</summary>
        public DoorKind GetDoorKind(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Count)
            {
                Debug.LogWarning($"[LevelSequence] GetDoorKind({index}) 越界（共 {Count} 关），返回 SwitchSubScene。");
                return DoorKind.SwitchSubScene;
            }

            return _entries[index].DoorKind;
        }
    }
}
