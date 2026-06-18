// ------------------------------------------------------------
// GameMain.cs
// Author : WizardHeHeJun
// Created: 2026-06-17
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.Game
{
    /// <summary>
    /// 游戏主入口。挂在 GameMain 场景的入口对象上，负责启动登录 / 资源加载流程。
    /// 当前为骨架：生命周期与启动钩子已就位，具体加载管线待接入。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameMain : MonoBehaviour
    {
        [Header("启动配置")]
        [Tooltip("勾选则在 Start 时自动进入加载流程；关闭则需外部手动调用。")]
        [SerializeField] private bool _autoStart = true;

        /// <summary>入口是否已完成启动调用，避免重复进入。</summary>
        public bool HasStarted { get; private set; }

        private void Awake()
        {
            // 入口对象需跨场景常驻，切场景时不被销毁。
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_autoStart)
            {
                StartLoading();
            }
        }

        /// <summary>
        /// 启动登录 / 资源加载流程。供 _autoStart 或外部入口调用。
        /// TODO: 接入实际的加载管线（登录 → 资源 → 进主场景）。
        /// </summary>
        public void StartLoading()
        {
            if (HasStarted)
            {
                return;
            }

            HasStarted = true;

            // TODO: 在此接入登录与资源加载流程。
        }
    }
}
