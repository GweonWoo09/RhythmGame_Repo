using RhythmGameStarter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 롱노트를 누르고 있는 동안 반복되는 이펙트를 HoldEffect_Sheet(4x3, 12프레임)로 만든다.
// 열려 있는 씬의 모든 레인(NoteArea)에 붙이고 sustainEffect를 새 이펙트로 바꾼다.
// 기존 이펙트 오브젝트는 지우지 않는다 (연결만 바뀜). 다시 실행하면 덮어쓴다.
public static class HoldEffectBuilder
{
    private const string Dir = "Assets/03.Sprite/VFX/";
    private const string TexturePath = Dir + "HoldEffect_Sheet.png";
    private const string MaterialPath = Dir + "HoldEffect.mat";
    private const string PrefabPath = Dir + "HoldEffect.prefab";
    private const string InstanceName = "HoldEffect";

    private const int TilesX = 4;
    private const int TilesY = 3;
    private const float Cycle = 0.5f;     // 12프레임 한 바퀴 시간 (초). 빠르면 늘린다
    private const float Size = 2.5f;      // 이펙트 크기 (월드 단위)
    private const int SortingOrder = 50;  // 노트보다 위에 그린다

    [UnityEditor.MenuItem("Tools/Build Hold Effect")] // 프로젝트에 MenuItem 클래스가 따로 있어서 전체 이름으로 적는다
    private static void Build()
    {
        var texture = ImportTexture();
        if (texture == null)
        {
            Debug.LogError("텍스처를 찾을 수 없습니다: " + TexturePath);
            return;
        }

        var prefab = CreatePrefab(CreateMaterial(texture));
        AssignToNoteAreas(prefab);

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
        var go = new GameObject(InstanceName);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 한 바퀴(Cycle)마다 1장을 만들고, 그 1장이 12프레임을 재생한다 → 누르는 동안 계속 반복
        var main = ps.main;
        main.duration = Cycle;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = Cycle;
        main.startSpeed = 0f;
        main.startSize = Size;
        main.startColor = Color.white;
        main.maxParticles = 2;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local; // 레인 크기(Scale)에 찌그러지지 않게

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

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

        go.AddComponent<NoteEffect>(); // NoteArea가 누를 때 StartEffect, 뗄 때 StopEffect를 부른다

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // 각 레인의 판정선 위치에 이펙트를 놓고 NoteArea.sustainEffect를 바꾼다
    private static void AssignToNoteAreas(GameObject prefab)
    {
        var areas = Object.FindObjectsByType<NoteArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (areas.Length == 0)
        {
            Debug.Log("이펙트 생성: " + PrefabPath + "\n04.InGame 씬을 열고 다시 실행하면 자동으로 교체됩니다.");
            return;
        }

        foreach (var area in areas)
        {
            var track = area.GetComponentInParent<Track>();
            if (track == null || track.lineArea == null) continue;

            var so = new SerializedObject(area);
            var prop = so.FindProperty("sustainEffect");

            // 다시 실행한 경우: 전에 만든 이펙트를 지운다
            var previous = prop.objectReferenceValue as NoteEffect;
            if (previous != null && previous.name == InstanceName)
                Undo.DestroyObjectImmediate(previous.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, track.transform);
            instance.name = InstanceName;
            instance.transform.position = track.lineArea.position;
            Undo.RegisterCreatedObjectUndo(instance, "Build Hold Effect");

            prop.objectReferenceValue = instance.GetComponent<NoteEffect>();
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(area.gameObject.scene);
        }

        Debug.Log("롱노트 이펙트 생성 및 교체 완료: " + PrefabPath + " (레인 " + areas.Length + "개). 씬을 저장하세요.");
    }
}
