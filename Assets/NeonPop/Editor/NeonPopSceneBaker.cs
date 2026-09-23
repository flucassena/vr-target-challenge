#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class NeonPopSceneBaker
{
    private const string ScenePath = "Assets/Scenes/Fase.unity";
    private const string RootName = "NEON POP - EDITABLE ARENA";
    private const string MaterialFolder = "Assets/NeonPop/Materials";
    private static readonly string RequestPath = Path.Combine(Application.dataPath, "NeonPop/RebuildArena.request");

    static NeonPopSceneBaker()
    {
        EditorApplication.delayCall += ProcessRequestedBake;
        EditorApplication.delayCall += FocusArenaOnce;
    }

    private static void FocusArenaOnce()
    {
        const string sessionKey = "NeonPop.ArenaFocused";
        if (SessionState.GetBool(sessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;
        GameObject arena = FindRoot(scene, RootName);
        if (arena == null) return;
        Selection.activeGameObject = arena;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        SessionState.SetBool(sessionKey, true);
    }

    [MenuItem("Tools/NEON POP/Rebuild Editable Arena")]
    public static void RebuildEditableArena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[NEON POP] Exit Play Mode before rebuilding the editable arena.");
            return;
        }

        bool wasLoaded = false;
        Scene scene = default;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);
            if (candidate.path == ScenePath)
            {
                scene = candidate;
                wasLoaded = true;
                break;
            }
        }

        if (!wasLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        BuildScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!wasLoaded)
            EditorSceneManager.CloseScene(scene, true);

        Debug.Log("[NEON POP] Editable arena baked and saved in Assets/Scenes/Fase.unity");
    }

    private static void ProcessRequestedBake()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ProcessRequestedBake;
            return;
        }

        File.Delete(RequestPath);
        RebuildEditableArena();
    }

    private static void BuildScene(Scene scene)
    {
        GameObject existingRoot = FindRoot(scene, RootName);
        if (existingRoot != null) Object.DestroyImmediate(existingRoot);

        EnsureFolder("Assets/NeonPop");
        EnsureFolder(MaterialFolder);
        Material black = GetOrCreateMaterial(MaterialFolder + "/Absolute Black.mat", new Color(0.001f, 0.001f, 0.002f, 1f), Color.black, false);
        Material cyan = GetOrCreateMaterial(MaterialFolder + "/Neon Cyan.mat", new Color(0.02f, 0.5f, 0.62f, 0.92f), new Color(0.1f, 5.2f, 7f), true);
        Material magenta = GetOrCreateMaterial(MaterialFolder + "/Neon Magenta.mat", new Color(0.66f, 0.02f, 0.42f, 0.92f), new Color(7f, 0.08f, 4.5f), true);
        Material gold = GetOrCreateMaterial(MaterialFolder + "/Neon Gold.mat", new Color(0.75f, 0.38f, 0.02f, 0.92f), new Color(6f, 2.2f, 0.06f), true);
        Material energy = GetOrCreateMaterial(MaterialFolder + "/Energy Shot.mat", new Color(0.18f, 0.85f, 1f, 0.95f), new Color(0.2f, 8f, 12f), true);

        Camera camera = FindCamera(scene);
        Vector3 forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized : Vector3.forward;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        float floorY = FindFloorY(scene, camera);
        Vector3 viewPosition = camera != null ? camera.transform.position : Vector3.zero;
        Vector3 arenaCenter = viewPosition + forward * 6.2f;
        arenaCenter.y = floorY;
        Vector3 roomCenter = viewPosition + forward * 3.4f;
        roomCenter.y = floorY;

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        HoloArenaBakedReferences references = root.AddComponent<HoloArenaBakedReferences>();
        references.darkMaterial = black;
        references.cyanMaterial = cyan;
        references.magentaMaterial = magenta;
        references.goldMaterial = gold;

        Transform shell = CreateGroup(root.transform, "01 - BLACK ROOM SHELL");
        Transform edges = CreateGroup(root.transform, "02 - NEON ROOM EDGES");
        Transform props = CreateGroup(root.transform, "03 - BLACK WORKBENCH");
        Transform hudRoot = CreateGroup(root.transform, "04 - WORLD HUD");
        Transform markers = CreateGroup(root.transform, "05 - GAMEPLAY MARKERS");

        GameObject spawn = new GameObject("Balloon Spawn Center");
        spawn.transform.SetParent(markers, false);
        spawn.transform.position = arenaCenter;
        spawn.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        references.spawnCenter = spawn.transform;

        BuildRoom(shell, edges, roomCenter, forward, right, black, cyan, magenta);
        BuildWorkbench(props, viewPosition, floorY, forward, right, black, cyan, magenta);
        BuildHud(hudRoot, arenaCenter, forward, references);
        BlackoutOriginalEnvironment(scene, black);
        TuneSceneLighting(scene);
        BakeEnergyProjectile(energy);

        Selection.activeGameObject = root;
    }

    private static void BuildRoom(Transform shell, Transform edges, Vector3 center, Vector3 forward, Vector3 right,
        Material black, Material cyan, Material magenta)
    {
        const float width = 12f;
        const float depth = 15f;
        const float height = 6.2f;
        const float line = 0.035f;
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;

        CreateBox(shell, "Floor - Absolute Black", center + Vector3.down * 0.08f, new Vector3(width, 0.16f, depth), forward, black);
        CreateBox(shell, "Ceiling - Absolute Black", center + Vector3.up * height, new Vector3(width, 0.16f, depth), forward, black);
        CreateBox(shell, "Wall - Left", center - right * halfWidth + Vector3.up * (height * 0.5f), new Vector3(0.16f, height, depth), forward, black);
        CreateBox(shell, "Wall - Right", center + right * halfWidth + Vector3.up * (height * 0.5f), new Vector3(0.16f, height, depth), forward, black);
        CreateBox(shell, "Wall - Front", center + forward * halfDepth + Vector3.up * (height * 0.5f), new Vector3(width, height, 0.16f), forward, black);
        CreateBox(shell, "Wall - Rear", center - forward * halfDepth + Vector3.up * (height * 0.5f), new Vector3(width, height, 0.16f), forward, black);

        for (int level = 0; level < 2; level++)
        {
            float y = level == 0 ? 0.025f : height - 0.025f;
            Material material = level == 0 ? cyan : magenta;
            CreateBox(edges, "Edge Left " + level, center - right * (halfWidth - 0.09f) + Vector3.up * y, new Vector3(line, line, depth - 0.18f), forward, material);
            CreateBox(edges, "Edge Right " + level, center + right * (halfWidth - 0.09f) + Vector3.up * y, new Vector3(line, line, depth - 0.18f), forward, material);
            CreateBox(edges, "Edge Front " + level, center + forward * (halfDepth - 0.09f) + Vector3.up * y, new Vector3(width - 0.18f, line, line), forward, material);
            CreateBox(edges, "Edge Rear " + level, center - forward * (halfDepth - 0.09f) + Vector3.up * y, new Vector3(width - 0.18f, line, line), forward, material);
        }

        CreateBox(edges, "Corner - Left Front", center - right * (halfWidth - 0.09f) + forward * (halfDepth - 0.09f) + Vector3.up * (height * 0.5f), new Vector3(line, height - 0.12f, line), forward, cyan);
        CreateBox(edges, "Corner - Right Front", center + right * (halfWidth - 0.09f) + forward * (halfDepth - 0.09f) + Vector3.up * (height * 0.5f), new Vector3(line, height - 0.12f, line), forward, magenta);
        CreateBox(edges, "Corner - Left Rear", center - right * (halfWidth - 0.09f) - forward * (halfDepth - 0.09f) + Vector3.up * (height * 0.5f), new Vector3(line, height - 0.12f, line), forward, magenta);
        CreateBox(edges, "Corner - Right Rear", center + right * (halfWidth - 0.09f) - forward * (halfDepth - 0.09f) + Vector3.up * (height * 0.5f), new Vector3(line, height - 0.12f, line), forward, cyan);

        for (int i = -2; i <= 2; i++)
            CreateBox(edges, "Front Wall Seam " + i, center + right * (i * 2.1f) + forward * (halfDepth - 0.1f) + Vector3.up * (height * 0.5f), new Vector3(0.018f, height - 0.3f, 0.018f), forward, i == 0 ? magenta : cyan);
    }

    private static void BuildWorkbench(Transform parent, Vector3 viewPosition, float floorY, Vector3 forward, Vector3 right,
        Material black, Material cyan, Material magenta)
    {
        Vector3 center = viewPosition + forward * 2.05f;
        center.y = floorY;
        CreateBox(parent, "Bench Top - Black", center + Vector3.up * 0.94f, new Vector3(3.8f, 0.16f, 1.15f), forward, black);
        CreateBox(parent, "Bench Front - Black", center - forward * 0.5f + Vector3.up * 0.48f, new Vector3(3.8f, 0.82f, 0.12f), forward, black);
        CreateBox(parent, "Bench Left Leg - Black", center - right * 1.7f + Vector3.up * 0.46f, new Vector3(0.16f, 0.92f, 0.82f), forward, black);
        CreateBox(parent, "Bench Right Leg - Black", center + right * 1.7f + Vector3.up * 0.46f, new Vector3(0.16f, 0.92f, 0.82f), forward, black);
        CreateBox(parent, "Neon Front Edge", center - forward * 0.58f + Vector3.up * 1.025f, new Vector3(3.86f, 0.035f, 0.035f), forward, cyan);
        CreateBox(parent, "Neon Back Edge", center + forward * 0.58f + Vector3.up * 1.025f, new Vector3(3.86f, 0.035f, 0.035f), forward, magenta);
        CreateBox(parent, "Neon Left Edge", center - right * 1.92f + Vector3.up * 1.025f, new Vector3(0.035f, 0.035f, 1.18f), forward, cyan);
        CreateBox(parent, "Neon Right Edge", center + right * 1.92f + Vector3.up * 1.025f, new Vector3(0.035f, 0.035f, 1.18f), forward, magenta);
    }

    private static void BuildHud(Transform parent, Vector3 arenaCenter, Vector3 forward, HoloArenaBakedReferences references)
    {
        GameObject canvasObject = new GameObject("NEON POP - Editable World HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1100f, 620f);
        canvasRect.localScale = Vector3.one * 0.0035f;
        canvasRect.position = arenaCenter + Vector3.up * 3.2f + forward * 3.6f;
        canvasRect.rotation = Quaternion.LookRotation(forward, Vector3.up);
        canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 18f;

        GameObject header = CreatePanel(canvasRect, "Header Panel", new Vector2(0f, 205f), new Vector2(1040f, 150f), new Color(0.002f, 0.004f, 0.012f, 0.96f), new Color(0.1f, 0.9f, 1f, 0.95f));
        CreateText(header.transform, "Title", "NEON POP  //  SIMULATION ARENA", 42, TextAlignmentOptions.MidlineLeft, new Vector2(-270f, 43f), new Vector2(450f, 58f), new Color(0.45f, 0.96f, 1f));
        CreateText(header.transform, "Score Label", "PONTOS", 22, TextAlignmentOptions.Center, new Vector2(165f, 52f), new Vector2(180f, 32f), new Color(0.6f, 0.76f, 0.86f));
        references.scoreText = CreateText(header.transform, "Score Value", "0000", 58, TextAlignmentOptions.Center, new Vector2(165f, 5f), new Vector2(220f, 72f), new Color(0.25f, 0.98f, 1f));
        CreateText(header.transform, "Timer Label", "TEMPO", 22, TextAlignmentOptions.Center, new Vector2(395f, 52f), new Vector2(180f, 32f), new Color(0.6f, 0.76f, 0.86f));
        references.timerText = CreateText(header.transform, "Timer Value", "00:45", 58, TextAlignmentOptions.Center, new Vector2(395f, 5f), new Vector2(220f, 72f), new Color(1f, 0.3f, 0.82f));
        references.statusText = CreateText(header.transform, "Status Text", "ATIRE NO BALAO CENTRAL PARA INICIAR", 22, TextAlignmentOptions.MidlineLeft, new Vector2(-260f, -43f), new Vector2(760f, 35f), new Color(0.67f, 0.78f, 0.9f));
        references.comboText = CreateText(header.transform, "Combo Text", string.Empty, 27, TextAlignmentOptions.MidlineRight, new Vector2(365f, -43f), new Vector2(290f, 38f), new Color(1f, 0.74f, 0.2f));

        GameObject center = CreatePanel(canvasRect, "Center Message", Vector2.zero, new Vector2(900f, 220f), new Color(0.001f, 0.003f, 0.01f, 0.94f), new Color(1f, 0.22f, 0.72f, 0.9f));
        references.centerGroup = center.AddComponent<CanvasGroup>();
        references.centerText = CreateText(center.transform, "Center Text", "NEON POP", 63, TextAlignmentOptions.Center, Vector2.zero, new Vector2(850f, 180f), Color.white);
    }

    private static void BakeEnergyProjectile(Material energyMaterial)
    {
        const string bulletPath = "Assets/Resoucers/Prefabs/Bullet.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(bulletPath);
        root.transform.localScale = new Vector3(0.06f, 0.06f, 0.14f);
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = energyMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule != null) { capsule.radius = 0.75f; capsule.height = 1.6f; capsule.direction = 2; }
        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body != null) { body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; }

        TrailRenderer trail = root.GetComponent<TrailRenderer>();
        if (trail == null) trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.42f;
        trail.startWidth = 0.14f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.01f;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.sharedMaterial = energyMaterial;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.1f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        Transform oldLight = root.transform.Find("Energy Core Light");
        GameObject lightObject = oldLight != null ? oldLight.gameObject : new GameObject("Energy Core Light", typeof(Light));
        lightObject.transform.SetParent(root.transform, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.1f, 0.8f, 1f);
        light.intensity = 3.5f;
        light.range = 3f;
        light.shadows = LightShadows.None;

        PrefabUtility.SaveAsPrefabAsset(root, bulletPath);
        PrefabUtility.UnloadPrefabContents(root);

        const string gunPath = "Assets/Resoucers/Prefabs/Bit Gun Variant.prefab";
        GameObject gun = PrefabUtility.LoadPrefabContents(gunPath);
        WeaponVR weapon = gun.GetComponent<WeaponVR>();
        if (weapon != null)
        {
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            serializedWeapon.FindProperty("bulletForce").floatValue = 12f;
            serializedWeapon.FindProperty("fireRate").floatValue = 0.45f;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(gun, gunPath);
        PrefabUtility.UnloadPrefabContents(gun);
    }

    private static void BlackoutOriginalEnvironment(Scene scene, Material black)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string value = (renderer.gameObject.name + " " + (renderer.transform.parent != null ? renderer.transform.parent.name : string.Empty)).ToLowerInvariant();
            if (!value.Contains("parede") && !value.Contains("piso") && !value.Contains("mesa") && !value.Contains("bancada")) continue;
            Material[] materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = black;
            renderer.sharedMaterials = materials;
        }
    }

    private static void TuneSceneLighting(Scene scene)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.fog = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            if (light.type == LightType.Directional) light.intensity = Mathf.Min(light.intensity, 0.08f);
    }

    private static Camera FindCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            if (camera.CompareTag("MainCamera")) return camera;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera != null) return camera;
        }
        return null;
    }

    private static float FindFloorY(Scene scene, Camera camera)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            if (renderer.gameObject.name.ToLowerInvariant().Contains("piso")) return renderer.bounds.max.y;
        return camera != null ? camera.transform.position.y - 1.55f : 0f;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    private static Transform CreateGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Vector3 forward, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        box.transform.localScale = scale;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color fill, Color edge)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = fill;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = edge;
        outline.effectDistance = new Vector2(3f, -3f);
        return panel;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 rectSize, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static Material GetOrCreateMaterial(string path, Color baseColor, Color emission, bool transparent)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.color = baseColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_EmissionColor")) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission); }
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", transparent ? 0.82f : 0.18f);
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
