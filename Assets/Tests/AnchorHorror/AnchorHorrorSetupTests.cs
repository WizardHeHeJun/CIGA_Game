// ------------------------------------------------------------
// AnchorHorrorSetupTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// 触发一键装配生成（因验证桥无法执行菜单项，借 EditMode 测试跑生成器）。
    /// 幂等：已存在则复用。断言 SO + 场景 + Build Settings 均落盘。
    /// </summary>
    public class AnchorHorrorSetupTests
    {
        [Test]
        public void GeneratePlayableSetup_CreatesAssetsAndScenes()
        {
            AnchorHorrorSetup.BuildAll();

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GlobalConfig>("Assets/Res/AnchorHorror/GlobalConfig.asset"), "GlobalConfig 未生成");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<FeatureDatabase>("Assets/Res/AnchorHorror/FeatureDatabase.asset"), "FeatureDatabase 未生成");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/Res/AnchorHorror/LevelConfig.asset"), "LevelConfig 未生成");

            Assert.IsTrue(System.IO.File.Exists("Assets/Res/AnchorHorror/Bootstrap.unity"), "Bootstrap 场景未生成");
            Assert.IsTrue(System.IO.File.Exists("Assets/Res/AnchorHorror/HorrorLevel.unity"), "HorrorLevel 场景未生成");

            var scenePaths = new System.Collections.Generic.HashSet<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                scenePaths.Add(s.path);
            }

            Assert.IsTrue(scenePaths.Contains("Assets/Res/AnchorHorror/Bootstrap.unity"), "Bootstrap 未加入 Build Settings");
            Assert.IsTrue(scenePaths.Contains("Assets/Res/AnchorHorror/HorrorLevel.unity"), "HorrorLevel 未加入 Build Settings");
        }
    }
}
