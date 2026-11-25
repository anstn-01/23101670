using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 메뉴: Tools > Convert Standard Materials to URP
// Project 창에서 선택한 폴더(들) 안의 Built-in "Standard" 머티리얼을 URP Lit 으로 바꿉니다.
// 아무 폴더도 선택하지 않았으면 Assets/도시 를 변환합니다.
// 텍스처(Base/Normal/Occlusion/Metallic/Emission), 색, 컷아웃, 투명 설정을 그대로 옮깁니다.
public static class StandardToURPConverter
{
    const string DefaultFolder = "Assets/도시";

    [MenuItem("Tools/Convert Standard Materials to URP")]
    public static void ConvertMenu()
    {
        var folders = new List<string>();
        foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(p)) folders.Add(p);
        }
        if (folders.Count == 0) folders.Add(DefaultFolder);
        Convert(folders.ToArray());
    }

    public static void Convert(string[] folders)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("URP Lit 셰이더를 찾지 못했습니다. 프로젝트가 URP 인지 확인하세요.");
            return;
        }

        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null) continue;
            string sn = m.shader.name;
            bool isStandard = sn == "Standard" || sn == "Standard (Specular setup)" || sn == "Hidden/InternalErrorShader";
            if (!isStandard) continue;

            // 기존 값 읽기
            Texture mainTex = GetTex(m, "_MainTex");
            Vector2 scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture bump = GetTex(m, "_BumpMap");
            float bumpScale = GetF(m, "_BumpScale", 1f);
            Texture occ = GetTex(m, "_OcclusionMap");
            float occStr = GetF(m, "_OcclusionStrength", 1f);
            Texture metalMap = GetTex(m, "_MetallicGlossMap");
            float metal = GetF(m, "_Metallic", 0f);
            float gloss = GetF(m, "_Glossiness", 0.5f);
            float smoothCh = GetF(m, "_SmoothnessTextureChannel", 0f);
            bool emOn = m.IsKeywordEnabled("_EMISSION");
            Texture emTex = GetTex(m, "_EmissionMap");
            Color emColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float mode = GetF(m, "_Mode", 0f); // 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent
            float cutoff = GetF(m, "_Cutoff", 0.5f);

            m.shader = lit;
            m.shaderKeywords = new string[0];

            m.SetTexture("_BaseMap", mainTex);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", gloss);

            if (bump != null)
            {
                m.SetTexture("_BumpMap", bump);
                m.SetFloat("_BumpScale", bumpScale);
                m.EnableKeyword("_NORMALMAP");
            }
            if (occ != null)
            {
                m.SetTexture("_OcclusionMap", occ);
                m.SetFloat("_OcclusionStrength", occStr);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            if (metalMap != null)
            {
                m.SetTexture("_MetallicGlossMap", metalMap);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            m.SetFloat("_SmoothnessTextureChannel", smoothCh);
            if (smoothCh > 0.5f) m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

            if (emOn)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", emTex);
                m.SetColor("_EmissionColor", emColor);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            // 표면 타입
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Cutoff", cutoff);
            if (mode >= 2f)
            {
                bool premul = mode >= 3f;
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", premul ? 1f : 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", premul ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (premul) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.SetShaderPassEnabled("DepthOnly", true);
                m.SetShaderPassEnabled("ShadowCaster", true);
                if (mode >= 1f)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else
                {
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = -1;
                }
            }

            EditorUtility.SetDirty(m);
            count++;
            Debug.Log("URP Lit 로 변환: " + path);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Standard → URP 변환 완료: " + count + "개 (" + string.Join(", ", folders) + ")");
    }

    static Texture GetTex(Material m, string n) => m.HasProperty(n) ? m.GetTexture(n) : null;
    static float GetF(Material m, string n, float d) => m.HasProperty(n) ? m.GetFloat(n) : d;
}
