using RhythmGameStarter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 타격 이펙트 프리팹을 HitEffect_Sheet(4x3, 12프레임) 스프라이트 시트로 만든다.
// 열려 있는 씬에 TapEffectPool이 있으면 새 이펙트로 교체한다. 다시 실행하면 덮어쓴다.
public static class HitEffectBuilder
{
    private const string Dir = "Assets/03.Sprite/VFX/";
    private const string TexturePath = Dir + "HitEffect_Sheet.png";
    private const string MaterialPath = Dir + "HitEffect.mat";
    private const string PrefabPath = Dir + "HitEffect.prefab";

    private const int TilesX = 4;
    private const int TilesY = 3;
    private const float Duration = 0.2f;  // 12프레임 재생 시간 (초)
    private const float Size = 2.5f;      // 이펙트 크기 (월드 단위). 크거나 작으면 이 값만 바꾼다
    private const int SortingOrder = 50;  // 노트보다 위에 그린다

    [UnityEditor.MenuItem("Tools/Build Hit Effect")] // 프로젝트에 MenuItem 클래스가 따로 있어서 전체 이름으로 적는다
    private static void Build()
    {
        var texture = ImportTexture();
        if (texture == null)
        {
            Debug.LogError("텍스처를 찾을 수 없습니다: " + TexturePath);
            return;
        }

        var material = CreateMaterial(texture);
        var prefab = CreatePrefab(material);
        AssignToPools(prefab);

        Selection.activeObject = prefab;
    }

    private static Texture2D ImportTexture()
    {
        AssetDatabase.ImportAsset(TexturePath);
        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    private static Material CreateMaterial(Texture2D texture)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else material.shader = shader;

        material.SetTexture("_BaseMap", texture);

        // 가산(Additive) 블렌딩: 겹칠수록 밝게 빛난다
        material.SetFloat("_Surface", 1f); // Transparent
        material.SetFloat("_Blend", 2f);   // Additive
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(material);
        return material;
    }
    
    private static GameObject CreatePrefab(Material material)
    {
        var go = new GameObject("HitEffect");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = Duration;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = Duration;
        main.startSpeed = 0f;
        main.startSize = Size;
        main.startColor = Color.white;
        main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.Callback; // 끝나면 NoteEffect가 풀에 돌려준다

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) }); // 시작할 때 1장만

        var shape = ps.shape;
        shape.enabled = false;

        // 스프라이트 시트를 순서대로 재생
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = TilesX;
        sheet.numTilesY = TilesY;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.cycleCount = 1;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = SortingOrder;

        go.AddComponent<NoteEffect>(); // 기존 풀(TapEffectPool)이 재생/반납을 처리한다

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // 열려 있는 씬의 TapEffectPool이 새 이펙트를 쓰게 한다 (원본 에셋 프리팹은 건드리지 않는다)
    private static void AssignToPools(GameObject prefab)
    {
        var pools = Object.FindObjectsByType<TapEffectPool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (pools.Length == 0)
        {
            Debug.Log("이펙트 생성: " + PrefabPath + "\n04.InGame 씬을 열고 다시 실행하면 자동으로 교체됩니다.");
            return;
        }

        foreach (var pool in pools)
        {
            var so = new SerializedObject(pool);
            so.FindProperty("effects").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(pool.gameObject.scene);
        }

        Debug.Log("이펙트 생성 및 교체 완료: " + PrefabPath + " (TapEffectPool " + pools.Length + "개). 씬을 저장하세요.");
    }
}
