using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public static class MistyHillsSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SdfMistyHillsValidation.unity";
    private const string ModelRoot = "Assets/Art/Environment/MistyHills/Models/FBX (Unity)";
    private const string TextureRoot = "Assets/Art/Environment/MistyHills/Textures";
    private const string QuaterniusTextureRoot = TextureRoot + "/Quaternius/Textures";
    private const string MaterialRoot = "Assets/Art/Environment/MistyHills/Materials";
    private const string GeneratedRoot = "Assets/Art/Environment/MistyHills/Generated";
    private const string SkyPath = "Assets/Art/Environment/MistyHills/Sky/qwantani_sunset_puresky_2k.hdr";
    private const string EnvironmentRootName = "MistyHillsEnvironment";
    private const float TerrainWidth = 72.0f;
    private const float TerrainZStart = -24.0f;
    private const float TerrainZEnd = 88.0f;
    private const float TerrainBottomY = -8.0f;

    private static readonly string[] RequiredTextures =
    {
        TextureRoot + "/Grass006_4K-JPG_Color.jpg",
        TextureRoot + "/Grass006_4K-JPG_NormalGL.jpg",
        TextureRoot + "/Grass006_4K-JPG_AmbientOcclusion.jpg",
        QuaterniusTextureRoot + "/Bark_DeadTree.png",
        QuaterniusTextureRoot + "/Bark_DeadTree_Normal.png",
        QuaterniusTextureRoot + "/Bark_NormalTree.png",
        QuaterniusTextureRoot + "/Bark_NormalTree_Normal.png",
        QuaterniusTextureRoot + "/Bark_TwistedTree.png",
        QuaterniusTextureRoot + "/Bark_TwistedTree_Normal.png",
        QuaterniusTextureRoot + "/Flowers.png",
        QuaterniusTextureRoot + "/Grass.png",
        QuaterniusTextureRoot + "/Leaf_Pine.png",
        QuaterniusTextureRoot + "/Leaf_Pine_C.png",
        QuaterniusTextureRoot + "/Leaves.png",
        QuaterniusTextureRoot + "/Leaves_GiantPine_C.png",
        QuaterniusTextureRoot + "/Leaves_NormalTree.png",
        QuaterniusTextureRoot + "/Leaves_NormalTree_C.png",
        QuaterniusTextureRoot + "/Leaves_TwistedTree.png",
        QuaterniusTextureRoot + "/Leaves_TwistedTree_C.png",
        QuaterniusTextureRoot + "/Mushrooms.png",
        QuaterniusTextureRoot + "/PathRocks_Diffuse.png",
        QuaterniusTextureRoot + "/Rocks_Diffuse.png",
        QuaterniusTextureRoot + "/Rocks_Desert_Diffuse.png",
        SkyPath
    };

    private sealed class MaterialSet
    {
        public Material Ground;
        public Material BarkNormal;
        public Material BarkDead;
        public Material BarkTwisted;
        public Material LeavesCommon;
        public Material LeavesNormal;
        public Material LeavesTwisted;
        public Material PineLeaves;
        public Material Grass;
        public Material Flowers;
        public Material Rocks;
        public Material PathRocks;
        public Material Mushrooms;
        public Material DistantSilhouette;
        public Material TerrainRidge;
        public Material Skybox;
        public Material SunDisc;
    }

    [MenuItem("SDF/Build Misty Hills Validation Scene")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void Build()
    {
        EnsureRequiredResources();
        EnsureFolders();
        ConfigureTextureImporters();
        AssetDatabase.Refresh();

        MaterialSet materials = CreateMaterials();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject oldRoot = GameObject.Find(EnvironmentRootName);
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        DisableLegacyBackdrop();
        ConfigureLighting(materials);
        ConfigureCamera();
        GameObject root = new GameObject(EnvironmentRootName);
        BuildTerrain(root.transform, materials);
        BuildEnvironmentSetDressing(root.transform, materials);
        SdfPhase1Driver surfaceDriver = ConfigureSdfObjects();
        ConfigureValidationSceneControllers(surfaceDriver);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Misty Hills validation scene built successfully.");
    }

    private static void EnsureRequiredResources()
    {
        List<string> missing = new List<string>();
        foreach (string path in RequiredTextures)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                missing.Add(path);
            }
        }

        foreach (string modelName in new[]
        {
            "CommonTree_1.fbx", "CommonTree_3.fbx", "TwistedTree_2.fbx", "Pine_3.fbx",
            "DeadTree_2.fbx", "Rock_Medium_1.fbx", "Rock_Medium_3.fbx",
            "Grass_Common_Tall.fbx", "Grass_Wispy_Tall.fbx", "Plant_1_Big.fbx"
        })
        {
            string path = $"{ModelRoot}/{modelName}";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                missing.Add(path);
            }
        }

        if (missing.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException("Misty Hills scene build is missing resources:\n" + string.Join("\n", missing));
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Editor");
        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/Environment");
        EnsureFolder("Assets/Art/Environment/MistyHills");
        EnsureFolder(MaterialRoot);
        EnsureFolder(GeneratedRoot);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent ?? "Assets", name);
    }

    private static void ConfigureTextureImporters()
    {
        ConfigureTexture(TextureRoot + "/Grass006_4K-JPG_Color.jpg", TextureImporterType.Default, false, true);
        ConfigureTexture(TextureRoot + "/Grass006_4K-JPG_NormalGL.jpg", TextureImporterType.NormalMap, false, false);
        ConfigureTexture(TextureRoot + "/Grass006_4K-JPG_AmbientOcclusion.jpg", TextureImporterType.Default, false, false);

        string[] alphaTextures =
        {
            "Flowers.png", "Grass.png", "Leaf_Pine.png", "Leaf_Pine_C.png", "Leaves.png",
            "Leaves_GiantPine_C.png", "Leaves_NormalTree.png", "Leaves_NormalTree_C.png",
            "Leaves_TwistedTree.png", "Leaves_TwistedTree_C.png", "Mushrooms.png"
        };

        foreach (string textureName in alphaTextures)
        {
            ConfigureTexture($"{QuaterniusTextureRoot}/{textureName}", TextureImporterType.Default, true, true);
        }

        foreach (string normalName in new[]
        {
            "Bark_DeadTree_Normal.png", "Bark_NormalTree_Normal.png", "Bark_TwistedTree_Normal.png"
        })
        {
            ConfigureTexture($"{QuaterniusTextureRoot}/{normalName}", TextureImporterType.NormalMap, false, false);
        }

        foreach (string textureName in new[]
        {
            "Bark_DeadTree.png", "Bark_NormalTree.png", "Bark_TwistedTree.png",
            "PathRocks_Diffuse.png", "Rocks_Diffuse.png", "Rocks_Desert_Diffuse.png"
        })
        {
            ConfigureTexture($"{QuaterniusTextureRoot}/{textureName}", TextureImporterType.Default, false, true);
        }

        TextureImporter skyImporter = AssetImporter.GetAtPath(SkyPath) as TextureImporter;
        if (skyImporter != null)
        {
            bool changed = skyImporter.textureShape != TextureImporterShape.TextureCube || skyImporter.maxTextureSize < 2048;
            skyImporter.textureShape = TextureImporterShape.TextureCube;
            skyImporter.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            skyImporter.sRGBTexture = true;
            skyImporter.mipmapEnabled = true;
            skyImporter.maxTextureSize = 2048;
            if (changed)
            {
                skyImporter.SaveAndReimport();
            }
        }
    }

    private static void ConfigureTexture(string path, TextureImporterType type, bool alphaTransparency, bool srgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = importer.textureType != type || importer.alphaIsTransparency != alphaTransparency || importer.sRGBTexture != srgb;
        importer.textureType = type;
        importer.alphaIsTransparency = alphaTransparency;
        importer.sRGBTexture = srgb;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 4;
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static MaterialSet CreateMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        Shader skyboxShader = Shader.Find("Skybox/Cubemap");

        MaterialSet set = new MaterialSet
        {
            Ground = CreateLit("MistyGround", lit, LoadTexture(TextureRoot + "/Grass006_4K-JPG_Color.jpg"), new Color(0.28f, 0.43f, 0.36f), LoadTexture(TextureRoot + "/Grass006_4K-JPG_NormalGL.jpg"), false, 0.72f),
            BarkNormal = CreateLit("BarkNormalTree", lit, LoadTexture($"{QuaterniusTextureRoot}/Bark_NormalTree.png"), new Color(0.34f, 0.39f, 0.38f), LoadTexture($"{QuaterniusTextureRoot}/Bark_NormalTree_Normal.png"), false, 0.58f),
            BarkDead = CreateLit("BarkDeadTree", lit, LoadTexture($"{QuaterniusTextureRoot}/Bark_DeadTree.png"), new Color(0.31f, 0.38f, 0.39f), LoadTexture($"{QuaterniusTextureRoot}/Bark_DeadTree_Normal.png"), false, 0.64f),
            BarkTwisted = CreateLit("BarkTwistedTree", lit, LoadTexture($"{QuaterniusTextureRoot}/Bark_TwistedTree.png"), new Color(0.3f, 0.36f, 0.35f), LoadTexture($"{QuaterniusTextureRoot}/Bark_TwistedTree_Normal.png"), false, 0.6f),
            LeavesCommon = CreateLit("LeavesCommon", lit, LoadTexture($"{QuaterniusTextureRoot}/Leaves.png"), new Color(0.18f, 0.35f, 0.31f), null, true, 0.82f),
            LeavesNormal = CreateLit("LeavesNormalTree", lit, LoadTexture($"{QuaterniusTextureRoot}/Leaves_NormalTree.png"), new Color(0.17f, 0.34f, 0.28f), null, true, 0.82f),
            LeavesTwisted = CreateLit("LeavesTwistedTree", lit, LoadTexture($"{QuaterniusTextureRoot}/Leaves_TwistedTree.png"), new Color(0.16f, 0.3f, 0.27f), null, true, 0.84f),
            PineLeaves = CreateLit("PineLeaves", lit, LoadTexture($"{QuaterniusTextureRoot}/Leaf_Pine.png"), new Color(0.15f, 0.31f, 0.29f), null, true, 0.84f),
            Grass = CreateLit("StylizedGrassClumps", lit, LoadTexture($"{QuaterniusTextureRoot}/Grass.png"), new Color(0.32f, 0.52f, 0.42f), null, true, 0.86f),
            Flowers = CreateLit("Flowers", lit, LoadTexture($"{QuaterniusTextureRoot}/Flowers.png"), new Color(0.78f, 0.8f, 0.68f), null, true, 0.82f),
            Rocks = CreateLit("Rocks", lit, LoadTexture($"{QuaterniusTextureRoot}/Rocks_Diffuse.png"), new Color(0.38f, 0.48f, 0.46f), null, false, 0.72f),
            PathRocks = CreateLit("PathRocks", lit, LoadTexture($"{QuaterniusTextureRoot}/PathRocks_Diffuse.png"), new Color(0.34f, 0.45f, 0.42f), null, false, 0.74f),
            Mushrooms = CreateLit("Mushrooms", lit, LoadTexture($"{QuaterniusTextureRoot}/Mushrooms.png"), new Color(0.72f, 0.74f, 0.66f), null, true, 0.72f),
            DistantSilhouette = CreateSolid("DistantTreeSilhouette", lit, new Color(0.035f, 0.135f, 0.145f, 1.0f), 0.82f),
            TerrainRidge = CreateSolid("MistyRidgeDark", lit, new Color(0.07f, 0.21f, 0.21f, 1.0f), 0.82f),
            SunDisc = CreateUnlit("MistySunDisc", unlit, new Color(1.0f, 0.72f, 0.43f, 1.0f))
        };

        set.Skybox = LoadOrCreateMaterial("MistyHillsSkybox", skyboxShader ?? Shader.Find("Skybox/Panoramic"));
        Texture skyTexture = LoadTexture(SkyPath);
        if (set.Skybox.HasProperty("_Tex"))
        {
            set.Skybox.SetTexture("_Tex", skyTexture);
        }
        if (set.Skybox.HasProperty("_Exposure"))
        {
            set.Skybox.SetFloat("_Exposure", 0.36f);
        }
        if (set.Skybox.HasProperty("_Tint"))
        {
            set.Skybox.SetColor("_Tint", new Color(0.56f, 0.72f, 0.82f, 1.0f));
        }
        EditorUtility.SetDirty(set.Skybox);

        AssetDatabase.SaveAssets();
        return set;
    }

    private static Material CreateLit(string name, Shader shader, Texture albedo, Color color, Texture normal, bool alphaClip, float smoothness)
    {
        Material material = LoadOrCreateMaterial(name, shader);
        SetMainTexture(material, albedo);
        SetColor(material, color);
        if (normal != null)
        {
            SetTextureIfPresent(material, "_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            SetFloatIfPresent(material, "_BumpScale", 1.0f);
        }
        else
        {
            material.DisableKeyword("_NORMALMAP");
        }

        SetFloatIfPresent(material, "_Smoothness", smoothness);
        SetFloatIfPresent(material, "_Metallic", 0.0f);
        ConfigureAlphaClip(material, alphaClip);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateSolid(string name, Shader shader, Color color, float smoothness)
    {
        Material material = LoadOrCreateMaterial(name, shader);
        SetMainTexture(material, null);
        SetColor(material, color);
        SetFloatIfPresent(material, "_Smoothness", smoothness);
        SetFloatIfPresent(material, "_Metallic", 0.0f);
        ConfigureAlphaClip(material, false);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateUnlit(string name, Shader shader, Color color)
    {
        Material material = LoadOrCreateMaterial(name, shader);
        SetMainTexture(material, null);
        SetColor(material, color);
        SetFloatIfPresent(material, "_Surface", 0.0f);
        SetFloatIfPresent(material, "_Cull", 0.0f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color);
            material.EnableKeyword("_EMISSION");
        }

        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateMaterial(string name, Shader shader)
    {
        string path = $"{MaterialRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        return material;
    }

    private static Texture LoadTexture(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Texture>(path);
    }

    private static void SetMainTexture(Material material, Texture texture)
    {
        SetTextureIfPresent(material, "_BaseMap", texture);
        SetTextureIfPresent(material, "_MainTex", texture);
    }

    private static void SetTextureIfPresent(Material material, string property, Texture texture)
    {
        if (material.HasProperty(property))
        {
            material.SetTexture(property, texture);
        }
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void ConfigureAlphaClip(Material material, bool alphaClip)
    {
        SetFloatIfPresent(material, "_AlphaClip", alphaClip ? 1.0f : 0.0f);
        SetFloatIfPresent(material, "_Cutoff", alphaClip ? 0.38f : 0.5f);
        SetFloatIfPresent(material, "_Surface", 0.0f);
        SetFloatIfPresent(material, "_Cull", 0.0f);

        if (alphaClip)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = -1;
        }
    }

    private static void DisableLegacyBackdrop()
    {
        GameObject backdrop = GameObject.Find("SdfValidationBackdrop");
        if (backdrop != null)
        {
            backdrop.SetActive(false);
        }
    }

    private static void ConfigureLighting(MaterialSet materials)
    {
        RenderSettings.skybox = materials.Skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.22f, 0.38f, 0.44f);
        RenderSettings.ambientEquatorColor = new Color(0.105f, 0.21f, 0.21f);
        RenderSettings.ambientGroundColor = new Color(0.045f, 0.075f, 0.065f);
        RenderSettings.ambientIntensity = 0.42f;
        RenderSettings.fog = false;

        Light light = Object.FindFirstObjectByType<Light>();
        if (light == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.name = "Directional Light";
        light.type = LightType.Directional;
        light.transform.position = new Vector3(4.0f, 6.2f, 54.0f);
        light.transform.rotation = Quaternion.LookRotation(new Vector3(-0.08f, -0.16f, -1.0f).normalized, Vector3.up);
        light.color = new Color(1.0f, 0.72f, 0.46f);
        light.intensity = 0.68f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.72f;
        light.cookie = null;
        RenderSettings.sun = light;
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        camera.transform.position = new Vector3(0.4f, 1.15f, -13.5f);
        LookAt(camera.transform, new Vector3(0.8f, 2.45f, 48.0f));
        camera.fieldOfView = 50.0f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 125.0f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true;
        camera.allowMSAA = true;

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }
        cameraData.renderPostProcessing = false;
        cameraData.requiresDepthTexture = true;

        OrbitValidationCamera orbitCamera = camera.GetComponent<OrbitValidationCamera>();
        if (orbitCamera != null)
        {
            orbitCamera.enabled = false;
        }
    }

    private static void BuildTerrain(Transform root, MaterialSet materials)
    {
        Mesh terrainMesh = CreateTerrainMesh();
        string terrainPath = $"{GeneratedRoot}/MistyHillsTerrain.asset";
        SaveMeshAsset(terrainMesh, terrainPath);

        GameObject terrain = new GameObject("RollingGrassTerrain");
        terrain.transform.SetParent(root, false);
        terrain.transform.position = Vector3.zero;
        terrain.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(terrainPath);
        MeshRenderer renderer = terrain.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = materials.Ground;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;

        Mesh ridgeMesh = CreateRidgeMesh();
        string ridgePath = $"{GeneratedRoot}/MistyHillsBackRidge.asset";
        SaveMeshAsset(ridgeMesh, ridgePath);

        GameObject ridge = new GameObject("DistantBackRidge");
        ridge.transform.SetParent(root, false);
        ridge.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ridgePath);
        MeshRenderer ridgeRenderer = ridge.AddComponent<MeshRenderer>();
        ridgeRenderer.sharedMaterial = materials.TerrainRidge;
        ridgeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ridgeRenderer.receiveShadows = false;

        Mesh nearRidgeMesh = CreateNearRidgeMesh();
        string nearRidgePath = $"{GeneratedRoot}/MistyHillsMidRidge.asset";
        SaveMeshAsset(nearRidgeMesh, nearRidgePath);

        GameObject nearRidge = new GameObject("MidgroundSlopeSilhouette");
        nearRidge.transform.SetParent(root, false);
        nearRidge.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(nearRidgePath);
        MeshRenderer nearRidgeRenderer = nearRidge.AddComponent<MeshRenderer>();
        nearRidgeRenderer.sharedMaterial = materials.TerrainRidge;
        nearRidgeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        nearRidgeRenderer.receiveShadows = false;

        BuildSunDisc(root, materials);
    }

    private static Mesh CreateTerrainMesh()
    {
        const int xSegments = 44;
        const int zSegments = 64;

        List<Vector3> vertices = new List<Vector3>((xSegments + 1) * (zSegments + 1) + 256);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        List<int> triangles = new List<int>(xSegments * zSegments * 6 + 2048);

        for (int z = 0; z <= zSegments; z++)
        {
            float vz = z / (float)zSegments;
            for (int x = 0; x <= xSegments; x++)
            {
                float vx = x / (float)xSegments;
                float px = Mathf.Lerp(-TerrainWidth * 0.5f, TerrainWidth * 0.5f, vx);
                float pz = Mathf.Lerp(TerrainZStart, TerrainZEnd, vz);
                vertices.Add(new Vector3(px, TerrainHeight(px, pz), pz));
                uvs.Add(new Vector2(vx * 10.0f, vz * 14.0f));
            }
        }

        for (int z = 0; z < zSegments; z++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int a = z * (xSegments + 1) + x;
                int b = a + 1;
                int c = a + xSegments + 1;
                int d = c + 1;
                AddQuad(triangles, a, c, d, b);
            }
        }

        AddTerrainSide(vertices, uvs, triangles, xSegments, zSegments, TerrainBottomY, Edge.Near);
        AddTerrainSide(vertices, uvs, triangles, xSegments, zSegments, TerrainBottomY, Edge.Far);
        AddTerrainSide(vertices, uvs, triangles, xSegments, zSegments, TerrainBottomY, Edge.Left);
        AddTerrainSide(vertices, uvs, triangles, xSegments, zSegments, TerrainBottomY, Edge.Right);

        int bottomA = vertices.Count;
        vertices.Add(new Vector3(-TerrainWidth * 0.5f, TerrainBottomY, TerrainZStart));
        vertices.Add(new Vector3(-TerrainWidth * 0.5f, TerrainBottomY, TerrainZEnd));
        vertices.Add(new Vector3(TerrainWidth * 0.5f, TerrainBottomY, TerrainZEnd));
        vertices.Add(new Vector3(TerrainWidth * 0.5f, TerrainBottomY, TerrainZStart));
        uvs.Add(new Vector2(0.0f, 0.0f));
        uvs.Add(new Vector2(0.0f, 1.0f));
        uvs.Add(new Vector2(1.0f, 1.0f));
        uvs.Add(new Vector2(1.0f, 0.0f));
        AddQuad(triangles, bottomA, bottomA + 1, bottomA + 2, bottomA + 3);

        Mesh mesh = new Mesh { name = "MistyHillsTerrain" };
        mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float TerrainHeight(float x, float z)
    {
        float vz = Mathf.InverseLerp(TerrainZStart, TerrainZEnd, z);
        float halfWidth = TerrainWidth * 0.5f;
        float sideLift = Mathf.Pow(Mathf.Abs(x) / halfWidth, 1.9f) * Mathf.Lerp(0.1f, 3.4f, vz);
        float distantRise = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(8.0f, 70.0f, z)) * 6.2f;
        float ridgeLift = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(42.0f, 82.0f, z)) * 2.0f;
        float pathDip = Mathf.Exp(-(x * x) / 16.0f) * Mathf.Lerp(0.25f, 0.95f, vz);
        float shoulder = Mathf.Exp(-Mathf.Pow(Mathf.Abs(x) - 10.0f, 2.0f) / 70.0f) * Mathf.Lerp(0.1f, 0.9f, vz);
        float wave = Mathf.Sin(x * 0.18f + z * 0.09f) * 0.16f + Mathf.Sin(z * 0.21f) * 0.13f;
        return sideLift + distantRise + ridgeLift + shoulder - pathDip + wave - 0.86f;
    }

    private enum Edge
    {
        Near,
        Far,
        Left,
        Right
    }

    private static void AddTerrainSide(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int xSegments, int zSegments, float bottomY, Edge edge)
    {
        int count = edge == Edge.Left || edge == Edge.Right ? zSegments + 1 : xSegments + 1;
        for (int i = 0; i < count - 1; i++)
        {
            int topA = GetTerrainEdgeIndex(i, xSegments, zSegments, edge);
            int topB = GetTerrainEdgeIndex(i + 1, xSegments, zSegments, edge);
            Vector3 bottomA = vertices[topA];
            Vector3 bottomB = vertices[topB];
            bottomA.y = bottomY;
            bottomB.y = bottomY;

            int baseIndex = vertices.Count;
            vertices.Add(vertices[topA]);
            vertices.Add(vertices[topB]);
            vertices.Add(bottomB);
            vertices.Add(bottomA);
            uvs.Add(new Vector2(0.0f, 1.0f));
            uvs.Add(new Vector2(1.0f, 1.0f));
            uvs.Add(new Vector2(1.0f, 0.0f));
            uvs.Add(new Vector2(0.0f, 0.0f));
            AddQuad(triangles, baseIndex, baseIndex + 1, baseIndex + 2, baseIndex + 3);
        }
    }

    private static int GetTerrainEdgeIndex(int i, int xSegments, int zSegments, Edge edge)
    {
        switch (edge)
        {
            case Edge.Near:
                return i;
            case Edge.Far:
                return zSegments * (xSegments + 1) + i;
            case Edge.Left:
                return i * (xSegments + 1);
            case Edge.Right:
                return i * (xSegments + 1) + xSegments;
            default:
                return 0;
        }
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }

    private static Mesh CreateRidgeMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-52.0f, -2.4f, 62.0f),
            new Vector3(-43.0f, 7.2f, 61.0f),
            new Vector3(-31.0f, 6.5f, 64.0f),
            new Vector3(-18.0f, 4.9f, 63.0f),
            new Vector3(-4.0f, 4.2f, 66.0f),
            new Vector3(10.0f, 5.1f, 64.0f),
            new Vector3(25.0f, 7.4f, 62.0f),
            new Vector3(42.0f, 6.7f, 65.0f),
            new Vector3(52.0f, -2.4f, 69.0f),
            new Vector3(-52.0f, -2.4f, 69.0f)
        };

        int[] triangles =
        {
            0, 1, 9,
            1, 2, 9,
            2, 3, 9,
            3, 4, 9,
            4, 5, 9,
            5, 6, 9,
            6, 7, 9,
            7, 8, 9
        };

        Mesh mesh = new Mesh { name = "MistyHillsBackRidge" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateNearRidgeMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-45.0f, -2.0f, 39.0f),
            new Vector3(-35.0f, 3.7f, 38.0f),
            new Vector3(-22.0f, 3.2f, 40.0f),
            new Vector3(-8.0f, 2.35f, 39.0f),
            new Vector3(8.0f, 2.7f, 40.5f),
            new Vector3(22.0f, 3.9f, 39.0f),
            new Vector3(37.0f, 3.4f, 41.0f),
            new Vector3(45.0f, -2.0f, 45.0f),
            new Vector3(-45.0f, -2.0f, 45.0f)
        };

        int[] triangles =
        {
            0, 1, 8,
            1, 2, 8,
            2, 3, 8,
            3, 4, 8,
            4, 5, 8,
            5, 6, 8,
            6, 7, 8
        };

        Mesh mesh = new Mesh { name = "MistyHillsMidRidge" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BuildSunDisc(Transform root, MaterialSet materials)
    {
        Mesh sunMesh = CreateSunDiscMesh();
        string sunPath = $"{GeneratedRoot}/MistyHillsSunDisc.asset";
        SaveMeshAsset(sunMesh, sunPath);

        GameObject sun = new GameObject("LowSunDisc");
        sun.transform.SetParent(root, false);
        sun.transform.position = new Vector3(2.2f, 5.4f, 57.5f);
        sun.transform.localScale = Vector3.one * 4.4f;
        LookAt(sun.transform, Camera.main != null ? Camera.main.transform.position : new Vector3(0.4f, 1.15f, -13.5f));
        sun.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(sunPath);
        MeshRenderer renderer = sun.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = materials.SunDisc;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh CreateSunDiscMesh()
    {
        const int segments = 48;
        List<Vector3> vertices = new List<Vector3>(segments + 1);
        List<int> triangles = new List<int>(segments * 3);
        vertices.Add(Vector3.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2.0f;
            vertices.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.0f));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = i == segments - 1 ? 1 : i + 2;
            triangles.Add(0);
            triangles.Add(next);
            triangles.Add(i + 1);
        }

        Mesh mesh = new Mesh { name = "MistyHillsSunDisc" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SaveMeshAsset(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
        }
    }

    private static void BuildEnvironmentSetDressing(Transform root, MaterialSet materials)
    {
        Random.InitState(31337);

        Transform trees = CreateChild(root, "Trees");
        Transform rocks = CreateChild(root, "Rocks");
        Transform grass = CreateChild(root, "GrassAndPlants");

        PlaceModelOnTerrain("CommonTree_2.fbx", -12.5f, 24.0f, 10.0f, 1.35f, 0.0f, trees, materials, "CommonTree");
        PlaceModelOnTerrain("CommonTree_3.fbx", 12.0f, 29.0f, -18.0f, 1.55f, 0.0f, trees, materials, "CommonTree");
        PlaceModelOnTerrain("TwistedTree_2.fbx", 17.5f, 36.0f, -8.0f, 1.55f, 0.0f, trees, materials, "TwistedTree");
        PlaceModelOnTerrain("Pine_3.fbx", 22.0f, 43.0f, 22.0f, 1.75f, 0.0f, trees, materials, "Pine");
        PlaceModelOnTerrain("DeadTree_2.fbx", -20.0f, 36.5f, 28.0f, 1.42f, 0.0f, trees, materials, "DeadTree");
        PlaceModelOnTerrain("CommonTree_5.fbx", 4.5f, 52.0f, -8.0f, 1.75f, 0.0f, trees, materials, "Silhouette");
        PlaceModelOnTerrain("TwistedTree_4.fbx", 9.5f, 55.0f, 16.0f, 1.62f, 0.0f, trees, materials, "Silhouette");
        PlaceModelOnTerrain("Pine_5.fbx", -8.0f, 54.0f, -18.0f, 1.78f, 0.0f, trees, materials, "Silhouette");

        for (int i = 0; i < 18; i++)
        {
            float z = Random.Range(46.0f, 72.0f);
            float x = Random.Range(-34.0f, 34.0f);
            if (Mathf.Abs(x) < 3.0f && z < 58.0f)
            {
                x += x < 0.0f ? -5.0f : 5.0f;
            }

            string model = Pick(i, "CommonTree_4.fbx", "CommonTree_5.fbx", "TwistedTree_5.fbx", "Pine_5.fbx", "DeadTree_5.fbx");
            PlaceModelOnTerrain(model, x, z, Random.Range(-28.0f, 28.0f), Random.Range(0.9f, 1.45f), 0.0f, trees, materials, "Silhouette");
        }

        for (int i = 0; i < 15; i++)
        {
            float z = Mathf.Lerp(3.0f, 34.0f, i / 14.0f);
            float side = i % 2 == 0 ? -1.0f : 1.0f;
            float x = side * Random.Range(1.8f, 5.2f) + Mathf.Sin(z * 0.18f) * 0.6f;
            string model = i % 3 == 0 ? "Rock_Medium_1.fbx" : i % 3 == 1 ? "Pebble_Round_4.fbx" : "Rock_Medium_3.fbx";
            PlaceModelOnTerrain(model, x, z, Random.Range(0.0f, 360.0f), Random.Range(0.38f, 0.9f), 0.04f, rocks, materials, "Rock");
        }

        for (int i = 0; i < 64; i++)
        {
            float z = Random.Range(-2.0f, 46.0f);
            float pathGap = Random.Range(1.5f, 7.8f);
            float x = (Random.value > 0.5f ? 1.0f : -1.0f) * pathGap;
            string model = Pick(i, "Grass_Common_Tall.fbx", "Grass_Wispy_Tall.fbx", "Grass_Common_Short.fbx", "Plant_1_Big.fbx", "Fern_1.fbx");
            PlaceModelOnTerrain(model, x, z, Random.Range(0.0f, 360.0f), Random.Range(0.48f, 1.18f), 0.0f, grass, materials, "Grass");
        }

        for (int i = 0; i < 14; i++)
        {
            float z = Random.Range(4.0f, 30.0f);
            float x = Random.Range(-6.5f, 6.5f);
            string model = Pick(i, "Flower_3_Group.fbx", "Flower_4_Group.fbx", "Mushroom_Common.fbx");
            string category = model.Contains("Mushroom", StringComparison.OrdinalIgnoreCase) ? "Mushroom" : "Flower";
            PlaceModelOnTerrain(model, x, z, Random.Range(0.0f, 360.0f), Random.Range(0.45f, 0.9f), 0.01f, grass, materials, category);
        }

        for (int i = 0; i < 13; i++)
        {
            float z = -1.0f + i * 2.9f;
            float x = Mathf.Sin(i * 0.73f) * 0.65f + Random.Range(-0.35f, 0.35f);
            string model = Pick(i, "RockPath_Round_Thin.fbx", "RockPath_Square_Thin.fbx", "RockPath_Round_Wide.fbx", "RockPath_Square_Wide.fbx");
            PlaceModelOnTerrain(model, x, z, Random.Range(-24.0f, 24.0f), Random.Range(0.72f, 1.08f), 0.035f, rocks, materials, "PathRock");
        }
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static string Pick(int index, params string[] values)
    {
        return values[index % values.Length];
    }

    private static GameObject PlaceModelOnTerrain(string fileName, float x, float z, float yaw, float scale, float yOffset, Transform parent, MaterialSet materials, string category)
    {
        return PlaceModel(
            fileName,
            new Vector3(x, TerrainHeight(x, z) + yOffset, z),
            yaw,
            scale,
            parent,
            materials,
            category);
    }

    private static GameObject PlaceModel(string fileName, Vector3 position, float yaw, float scale, Transform parent, MaterialSet materials, string category)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/{fileName}");
        if (asset == null)
        {
            Debug.LogWarning($"Missing model: {fileName}");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (instance == null)
        {
            return null;
        }

        instance.name = System.IO.Path.GetFileNameWithoutExtension(fileName);
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
        instance.transform.localScale = Vector3.one * scale;
        AssignEnvironmentMaterials(instance, materials, category);

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = category == "Silhouette" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = category != "Silhouette";
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        return instance;
    }

    private static void AssignEnvironmentMaterials(GameObject instance, MaterialSet materials, string category)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            Material[] assigned = new Material[Mathf.Max(renderer.sharedMaterials.Length, 1)];
            for (int i = 0; i < assigned.Length; i++)
            {
                string slotName = renderer.sharedMaterials.Length > i && renderer.sharedMaterials[i] != null
                    ? renderer.sharedMaterials[i].name
                    : string.Empty;
                assigned[i] = PickMaterial(materials, category, instance.name, renderer.name, slotName, i, assigned.Length);
            }
            renderer.sharedMaterials = assigned;
        }
    }

    private static Material PickMaterial(MaterialSet materials, string category, string instanceName, string rendererName, string slotName, int slotIndex, int slotCount)
    {
        string key = $"{category} {instanceName} {rendererName} {slotName}".ToLowerInvariant();
        if (category == "Silhouette")
        {
            return materials.DistantSilhouette;
        }
        if (key.Contains("rock") || key.Contains("pebble"))
        {
            return category == "PathRock" ? materials.PathRocks : materials.Rocks;
        }
        if (key.Contains("mushroom"))
        {
            return materials.Mushrooms;
        }
        if (key.Contains("flower") || key.Contains("petal"))
        {
            return materials.Flowers;
        }
        if (key.Contains("grass") || key.Contains("clover") || key.Contains("fern") || key.Contains("plant"))
        {
            return materials.Grass;
        }
        if (key.Contains("pine"))
        {
            return IsLikelyBark(key, slotIndex, slotCount) ? materials.BarkNormal : materials.PineLeaves;
        }
        if (key.Contains("deadtree"))
        {
            return IsLikelyBark(key, slotIndex, slotCount) ? materials.BarkDead : materials.LeavesCommon;
        }
        if (key.Contains("twistedtree"))
        {
            return IsLikelyBark(key, slotIndex, slotCount) ? materials.BarkTwisted : materials.LeavesTwisted;
        }
        if (key.Contains("commontree"))
        {
            return IsLikelyBark(key, slotIndex, slotCount) ? materials.BarkNormal : materials.LeavesNormal;
        }

        return materials.Grass;
    }

    private static bool IsLikelyBark(string key, int slotIndex, int slotCount)
    {
        if (key.Contains("bark") || key.Contains("trunk") || key.Contains("wood"))
        {
            return true;
        }
        if (key.Contains("leaf") || key.Contains("leave") || key.Contains("foliage"))
        {
            return false;
        }

        return slotCount > 1 && slotIndex == 0;
    }

    private static SdfPhase1Driver ConfigureSdfObjects()
    {
        Material sdfMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Sdf/SdfPhase1_Mat.mat");
        GameObject sdfObject = GameObject.Find("SdfProxyCube");
        if (sdfObject == null)
        {
            sdfObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sdfObject.name = "SdfProxyCube";
        }

        float sdfZ = 18.0f;
        sdfObject.transform.position = new Vector3(0.0f, TerrainHeight(0.0f, sdfZ) + 1.2f, sdfZ);
        sdfObject.transform.rotation = Quaternion.identity;
        sdfObject.transform.localScale = new Vector3(1.28f, 1.28f, 1.28f);
        MeshRenderer sdfRenderer = sdfObject.GetComponent<MeshRenderer>();
        if (sdfRenderer != null && sdfMaterial != null)
        {
            sdfRenderer.sharedMaterial = sdfMaterial;
            sdfRenderer.shadowCastingMode = ShadowCastingMode.On;
            sdfRenderer.receiveShadows = true;
        }

        SdfPhase1Driver surfaceDriver = sdfObject.GetComponent<SdfPhase1Driver>();
        if (surfaceDriver == null)
        {
            surfaceDriver = sdfObject.AddComponent<SdfPhase1Driver>();
        }
        ConfigureSurfaceSdfDriver(surfaceDriver);

        GameObject volumeObject = GameObject.Find("SdfSharedVolumeProxy");
        if (volumeObject == null)
        {
            volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volumeObject.name = "SdfSharedVolumeProxy";
        }

        volumeObject.transform.position = new Vector3(0.0f, 1.85f, 32.0f);
        volumeObject.transform.rotation = Quaternion.identity;
        volumeObject.transform.localScale = new Vector3(78.0f, 5.6f, 104.0f);
        MeshRenderer volumeRenderer = volumeObject.GetComponent<MeshRenderer>();
        if (volumeRenderer != null && sdfMaterial != null)
        {
            volumeRenderer.sharedMaterial = sdfMaterial;
        }

        SdfPhase1Driver volumeDriver = volumeObject.GetComponent<SdfPhase1Driver>();
        if (volumeDriver == null)
        {
            volumeDriver = volumeObject.AddComponent<SdfPhase1Driver>();
        }

        SdfSharedVolumeProxy proxy = volumeObject.GetComponent<SdfSharedVolumeProxy>();
        if (proxy == null)
        {
            proxy = volumeObject.AddComponent<SdfSharedVolumeProxy>();
        }

        ConfigureVolumeDriver(volumeDriver, proxy, surfaceDriver);
        return surfaceDriver;
    }

    private static void ConfigureSurfaceSdfDriver(SdfPhase1Driver driver)
    {
        SerializedObject serialized = new SerializedObject(driver);
        SetProperty(serialized, "shapeMode", (int)SdfPhase1Driver.ShapeMode.Sphere);
        SetProperty(serialized, "sphereCenter", Vector3.zero);
        SetProperty(serialized, "sphereRadius", 0.38f);
        SetProperty(serialized, "boxExtents", new Vector3(0.36f, 0.36f, 0.36f));
        SetProperty(serialized, "cutPlaneNormal", new Vector3(0.82f, 0.22f, -0.35f));
        SetProperty(serialized, "cutPlaneOffset", 0.02f);
        SetProperty(serialized, "maxSteps", 96);
        SetProperty(serialized, "maxDistance", 8.0f);
        SetProperty(serialized, "baseColor", new Color(0.82f, 0.67f, 0.53f, 1.0f));
        SetProperty(serialized, "ambientStrength", 0.22f);
        SetProperty(serialized, "diffuseStrength", 1.05f);
        SetProperty(serialized, "cutFaceColor", new Color(0.95f, 0.48f, 0.32f, 1.0f));
        SetProperty(serialized, "volumeContributionMode", (int)SdfPhase1Driver.VolumeContributionMode.SurfaceOnly);
        SetProperty(serialized, "syncEveryFrame", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        driver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.SurfaceOnly);
        driver.SetDebugView(SdfPhase1Driver.DebugViewMode.Lighting);
    }

    private static void ConfigureVolumeDriver(SdfPhase1Driver volumeDriver, SdfSharedVolumeProxy proxy, SdfPhase1Driver surfaceDriver)
    {
        volumeDriver.ApplyVolumePreset(SdfPhase1Driver.VolumePreset.Balanced);
        volumeDriver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.VolumeOnly);
        volumeDriver.SetVolumeMediumSettings(
            1.35f,
            0.62f,
            0.012f,
            0.055f,
            0.62f,
            0.075f,
            1.08f,
            new Color(0.54f, 0.78f, 0.84f, 1.0f));
        volumeDriver.SetVolumeVisibilitySettings(0.0015f, 0.001f);
        volumeDriver.SetCloudShapeSettings(
            SdfPhase1Driver.VolumeFogShapeMode.ProxyBox,
            new Vector3(0.5f, 0.5f, 0.5f),
            0.34f,
            0.0f,
            0.85f,
            0.18f,
            0.55f,
            0.0f,
            3.0f,
            0.0f,
            1,
            Vector3.zero,
            0.7f,
            0.0f);
        volumeDriver.SetVolumePointLight(
            true,
            new Vector3(1.8f, 3.6f, 47.0f),
            new Color(1.0f, 0.66f, 0.34f, 1.0f),
            8.0f,
            18.0f);

        SerializedObject serializedVolumeDriver = new SerializedObject(volumeDriver);
        SetProperty(serializedVolumeDriver, "volumeLightSamples", 14);
        SetProperty(serializedVolumeDriver, "volumeLightMaxDistance", 1.8f);
        SetProperty(serializedVolumeDriver, "volumeLightMaxStepLength", 0.2f);
        SetProperty(serializedVolumeDriver, "volumeLightShadowStrength", 0.34f);
        SetProperty(serializedVolumeDriver, "volumeNoiseContrast", 0.72f);
        SetProperty(serializedVolumeDriver, "volumeLightNoiseStrength", 0.08f);
        SetProperty(serializedVolumeDriver, "volumeShadowSamples", 8);
        SetProperty(serializedVolumeDriver, "volumeShadowMaxDistance", 1.8f);
        SetProperty(serializedVolumeDriver, "volumeSurfaceOcclusionStrength", 0.18f);
        SetProperty(serializedVolumeDriver, "maxDistance", 12.0f);
        serializedVolumeDriver.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedProxy = new SerializedObject(proxy);
        SetProperty(serializedProxy, "autoFindSurfaceDrivers", false);
        SetProperty(serializedProxy, "forceSurfaceDriversToSurfaceOnly", true);
        SetProperty(serializedProxy, "autoFitBounds", false);
        SetProperty(serializedProxy, "manualCenter", new Vector3(0.0f, 1.85f, 32.0f));
        SetProperty(serializedProxy, "manualSize", new Vector3(78.0f, 5.6f, 104.0f));
        SetProperty(serializedProxy, "boundsPadding", 0.35f);
        SetProperty(serializedProxy, "useScreenSpaceVolume", true);
        SetProperty(serializedProxy, "hideProxyRendererInScreenSpace", true);
        SetProperty(serializedProxy, "screenSpaceVisibilityMode", 1);
        SetProperty(serializedProxy, "enableCutTileCulling", true);
        SetProperty(serializedProxy, "maxCutIndicesPerTile", 64);
        serializedProxy.ApplyModifiedPropertiesWithoutUndo();
        proxy.SetSurfaceDrivers(new[] { surfaceDriver });

        SdfCloudVolumeController cloudController = proxy.GetComponent<SdfCloudVolumeController>();
        if (cloudController != null)
        {
            SerializedObject serializedCloud = new SerializedObject(cloudController);
            SetProperty(serializedCloud, "applyOnEnable", false);
            SetProperty(serializedCloud, "applyOnValidate", false);
            SetProperty(serializedCloud, "animateWarmCoreLightInPlayMode", false);
            serializedCloud.ApplyModifiedPropertiesWithoutUndo();
            cloudController.enabled = false;
        }
    }

    private static void ConfigureValidationSceneControllers(SdfPhase1Driver surfaceDriver)
    {
        SdfValidationEnvironmentController environmentController = Object.FindFirstObjectByType<SdfValidationEnvironmentController>();
        if (environmentController != null)
        {
            Light directionalLight = RenderSettings.sun != null ? RenderSettings.sun : Object.FindFirstObjectByType<Light>();
            SdfSharedVolumeProxy sharedVolumeProxy = Object.FindFirstObjectByType<SdfSharedVolumeProxy>();
            Camera camera = Camera.main;

            SerializedObject serialized = new SerializedObject(environmentController);
            SetProperty(serialized, "directionalLight", directionalLight);
            SetProperty(serialized, "sharedVolumeProxy", sharedVolumeProxy);
            SetObjectArrayProperty(serialized, "sdfDrivers", new Object[] { surfaceDriver });
            SetObjectArrayProperty(serialized, "validationCameras", camera != null ? new Object[] { camera } : Array.Empty<Object>());
            SetProperty(serialized, "validationMode", (int)SdfValidationEnvironmentController.ValidationMode.FinalLighting);
            SetProperty(serialized, "applyOnEnable", true);
            SetProperty(serialized, "applyOnValidate", true);
            SetProperty(serialized, "applyInEditMode", true);
            SetProperty(serialized, "normalAmbientIntensity", 0.42f);
            SetProperty(serialized, "validationAmbientIntensity", 0.42f);
            SetProperty(serialized, "useDarkValidationSky", false);
            SetProperty(serialized, "normalLightIntensity", 0.68f);
            SetProperty(serialized, "validationLightIntensity", 0.68f);
            SetProperty(serialized, "normalLightColor", new Color(1.0f, 0.72f, 0.46f, 1.0f));
            SetProperty(serialized, "validationLightColor", new Color(1.0f, 0.72f, 0.46f, 1.0f));
            SetProperty(serialized, "useValidationCookie", false);
            SetProperty(serialized, "showBackdropInValidationModes", false);
            SetProperty(serialized, "animateLightInValidationModes", false);
            SetProperty(serialized, "pulseLightIntensityInVolumeMode", false);
            SetProperty(serialized, "applyVolumePresetInValidationModes", false);
            SetProperty(serialized, "enforceSingleVolumeBackground", true);
            SetProperty(serialized, "enableVirtualPointLight", false);
            SetProperty(serialized, "animateVirtualPointLight", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            environmentController.ApplyCurrentMode();
        }
    }

    private static void SetProperty(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetProperty(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetProperty(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetProperty(SerializedObject serialized, string propertyName, Vector3 value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }

    private static void SetProperty(SerializedObject serialized, string propertyName, Color value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
        }
    }

    private static void SetProperty(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetObjectArrayProperty(SerializedObject serialized, string propertyName, Object[] values)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void LookAt(Transform transform, Vector3 target)
    {
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
