// ------------------------------------------------------------
// FeatureEnums.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------

namespace Ciga.AnchorHorror
{
    /// <summary>特征的四个维度。新增维度时同步扩展本枚举与 FeatureTag.GetFeatures()。</summary>
    public enum FeatureDimension
    {
        Color,
        Shape,
        Material,
        Texture,
    }

    /// <summary>颜色（视觉）。None 表示该维度不参与匹配，抽锚点时剔除。</summary>
    public enum FeatureColor
    {
        None,
        Red,
        Blue,
        Green,
        Yellow,
        White,
        Black,
        Brown,
    }

    /// <summary>形状（外形轮廓）。</summary>
    public enum FeatureShape
    {
        None,
        Round,
        Square,
        Long,
        Flat,
        Irregular,
    }

    /// <summary>材质：摸出来的感觉（材料）。与 Texture 正交。</summary>
    public enum FeatureMaterial
    {
        None,
        Wood,
        Metal,
        Glass,
        Fabric,
        Paper,
        Ceramic,
    }

    /// <summary>纹理：看出来的表面现象（视觉表面）。与 Material 正交。</summary>
    public enum FeatureTexture
    {
        None,
        Smooth,
        Rough,
        Glossy,
        Matte,
        Patterned,
    }
}
