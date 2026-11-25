using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 메뉴: Tools > Fix Low Poly Cars Materials (URP)
// 'Drivable-Free Low Poly Cars' 에셋의 Built-in Standard 머티리얼을 URP Lit 으로 바꾸고
// 텍스처/색/발광/투명 설정을 그대로 옮깁니다. (분홍색으로 보이는 문제 해결)
public static class LowPolyCarsURPFixer
{
    const string Root = "Assets/Drivable-Free Low Poly Cars";

    [MenuItem("Tools/Fix Low Poly Cars Materials (URP)")]
    public static void Fix()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("URP Lit 셰이더를 찾지 못했습니다. 프로젝트가 URP 인지 확인하세요.");
            return;
        }

        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == lit) continue;

            // 기존(Standard) 값 먼저 읽기
            Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            Vector2 mainScale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 mainOffset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            bool emissionOn = m.IsKeywordEnabled("_EMISSION");
            Texture emTex = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            Color emColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f; // 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent

            m.shader = lit;
            m.shaderKeywords = new string[0];

            m.SetTexture("_BaseMap", mainTex);
            m.SetTextureScale("_BaseMap", mainScale);
            m.SetTextureOffset("_BaseMap", mainOffset);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", gloss);
            m.SetFloat("_Metallic", metal);

            if (emissionOn)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", emTex);
                m.SetColor("_EmissionColor", emColor);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (mode >= 2f)
            {
                // 투명 (유리)
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetOverrideTag("RenderType", "Opaque");
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = -1;
            }

            EditorUtility.SetDirty(m);
            count++;
            Debug.Log("URP Lit 로 변환: " + path);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Low Poly Cars 머티리얼 변환 완료: " + count + "개");
    }
}
