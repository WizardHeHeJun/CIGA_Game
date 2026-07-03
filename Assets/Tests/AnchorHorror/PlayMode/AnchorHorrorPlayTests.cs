// ------------------------------------------------------------
// AnchorHorrorPlayTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using System.Reflection;
using Ciga.AnchorHorror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ciga.AnchorHorror.PlayTests
{
    /// <summary>
    /// PlayMode 运行时冒烟：真 play 下构建并接线 GameManager，验证 Awake→Start 生命周期
    /// 无异常、进入 InitRoom、单例/AnchorSystem/San 初始化正确。补足 EditMode 未覆盖的 Mono 生命周期。
    /// </summary>
    public class AnchorHorrorPlayTests
    {
        private const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        public IEnumerator GameManager_Boots_ToInitRoom_NoErrors()
        {
            var cfg = ScriptableObject.CreateInstance<GlobalConfig>();

            // 先禁用再接线，保证 Awake 时依赖已就绪（GameManager.Awake 需要 _config/_sanity）
            var root = new GameObject("AH_PlayTest");
            root.SetActive(false);
            var sanity = root.AddComponent<SanitySystem>();
            var interaction = root.AddComponent<InteractionSystem>();
            var gm = root.AddComponent<GameManager>();

            typeof(GameManager).GetField("_config", F).SetValue(gm, cfg);
            typeof(GameManager).GetField("_sanity", F).SetValue(gm, sanity);
            typeof(GameManager).GetField("_interaction", F).SetValue(gm, interaction);

            root.SetActive(true);   // Awake：设单例、Init sanity、new AnchorSystem
            yield return null;      // Start：EnterInitRoom
            yield return null;

            Assert.IsNotNull(GameManager.Instance, "单例未建立");
            Assert.AreEqual(GamePhase.InitRoom, gm.CurrentPhase, "启动应进入 InitRoom");
            Assert.IsNotNull(gm.Anchor, "AnchorSystem 未创建");
            Assert.AreEqual(100f, sanity.Current, 1e-4, "San 初始应为 100");
            Assert.AreEqual(SanityState.Normal, sanity.State);

            Object.Destroy(root);
            Object.Destroy(cfg);
            yield return null;
        }
    }
}
