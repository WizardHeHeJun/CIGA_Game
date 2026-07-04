// ------------------------------------------------------------
// SceneNames.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------

namespace Ciga.Startup
{
    /// <summary>
    /// 场景名称常量，杜绝散落的 index 与字符串魔法。
    /// 全部按 Build Settings 中的文件名填写；Build Settings 顺序变动不影响逻辑。
    /// </summary>
    public static class SceneNames
    {
        public const string Login = "Login";
        public const string GameMain = "GameMain";
        public const string Bootstrap = "Bootstrap";
        public const string HorrorLevel = "HorrorLevel";
    }
}
