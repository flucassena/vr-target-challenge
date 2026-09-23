using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the complete Neon Pop arena to the existing, working Fase scene.
/// Nothing in the XR rig or gun prefab is replaced.
/// </summary>
public static class HoloBalloonArenaBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureArena(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureArena(scene);
    }

    private static void EnsureArena(Scene scene)
    {
        if (scene.name != "Fase")
            return;

        if (Object.FindAnyObjectByType<HoloBalloonArena>() != null)
            return;

        new GameObject("NEON POP - Arena Controller").AddComponent<HoloBalloonArena>();
    }
}

public sealed class HoloBalloonArena : MonoBehaviour
{
    private enum RoundState { Loading, Ready, Countdown, Playing, Finished }

    private const float RoundDuration = 45f;
    private const float CountdownDuration = 5f;

    private readonly List<HoloBalloonTarget> activeBalloons = new List<HoloBalloonTarget>();
    private GameObject balloonTemplate;
    private Camera playerCamera;
    private Transform arenaRoot;
    private Vector3 forward;
    private Vector3 right;
    private Vector3 arenaCenter;
    private float floorY;
    private float timeRemaining;
    private float nextSpawn;
    private int score;
    private int combo;
    private int bestScore;
    private RoundState state = RoundState.Loading;

    private TMP_Text scoreText;
    private TMP_Text timerText;
    private TMP_Text statusText;
    private TMP_Text centerText;
    private TMP_Text comboText;
    private CanvasGroup centerGroup;
    private AudioSource balloonPopAudio;

    private Material darkMaterial;
    private Material cyanMaterial;
    private Material magentaMaterial;
    private Material goldMaterial;

    public bool IsRoundActive { get { return state == RoundState.Playing; } }
    public float DespawnHeight { get { return floorY + 7.5f; } }

    private IEnumerator Start()
    {
        yield return null;

        playerCamera = Camera.main;
        if (playerCamera == null && Camera.allCamerasCount > 0)
            playerCamera = Camera.allCameras[0];

        if (playerCamera == null)
        {
            Debug.LogError("[NEON POP] No player camera was found; arena setup stopped.");
            yield break;
        }

        forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        right = Vector3.Cross(Vector3.up, forward).normalized;
        floorY = playerCamera.transform.position.y - 1.55f;
        arenaCenter = playerCamera.transform.position + forward * 6.2f;
        arenaCenter.y = floorY;

        GameObject[] existingTargets;
        try { existingTargets = GameObject.FindGameObjectsWithTag("Target"); }
        catch { existingTargets = new GameObject[0]; }

        if (existingTargets.Length > 0)
        {
            balloonTemplate = existingTargets[0];
            foreach (GameObject target in existingTargets)
                target.SetActive(false);
        }
        else
        {
            balloonTemplate = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            balloonTemplate.name = "Runtime Balloon Template";
            balloonTemplate.SetActive(false);
        }

        HoloArenaBakedReferences baked = Object.FindAnyObjectByType<HoloArenaBakedReferences>();
        if (baked != null)
        {
            arenaRoot = baked.transform;
            if (baked.spawnCenter != null)
            {
                arenaCenter = baked.spawnCenter.position;
                floorY = arenaCenter.y;
                forward = Vector3.ProjectOnPlane(baked.spawnCenter.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
                right = Vector3.Cross(Vector3.up, forward).normalized;
            }
            darkMaterial = baked.darkMaterial;
            cyanMaterial = baked.cyanMaterial;
            magentaMaterial = baked.magentaMaterial;
            goldMaterial = baked.goldMaterial;
            scoreText = baked.scoreText;
            timerText = baked.timerText;
            statusText = baked.statusText;
            centerText = baked.centerText;
            comboText = baked.comboText;
            centerGroup = baked.centerGroup;
        }
        else
        {
            Debug.LogError("[NEON POP] Editable arena is missing from Fase. Use Tools > NEON POP > Rebuild Editable Arena.");
            yield break;
        }
        GameObject popAudioPrefab = Resources.Load<GameObject>("NeonPop/BalloonPopAudio");
        if (popAudioPrefab != null)
        {
            balloonPopAudio = Instantiate(popAudioPrefab, transform).GetComponent<AudioSource>();
            if (balloonPopAudio != null && balloonPopAudio.clip != null)
                balloonPopAudio.clip.LoadAudioData();
        }
        else
        {
            Debug.LogWarning("[NEON POP] BalloonPopAudio resource is missing; pop sound is unavailable.");
        }
        ShowReadyState();
    }

    private void Update()
    {
        if (state != RoundState.Playing)
            return;

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        if (Time.time >= nextSpawn)
        {
            float progress = 1f - (timeRemaining / RoundDuration);
            nextSpawn = Time.time + Mathf.Lerp(0.9f, 0.48f, progress);
            SpawnBalloon(false);
            if (progress > 0.65f && Random.value > 0.62f)
                SpawnBalloon(false);
        }

        UpdateHud();
        if (timeRemaining <= 0f)
            FinishRound();
    }

    public void BalloonHit(HoloBalloonTarget balloon)
    {
        if (balloon == null)
            return;

        activeBalloons.Remove(balloon);
        // Keep playback on the controller: popping/clearing a balloon must not cut its sound.
        if (balloonPopAudio != null && balloonPopAudio.clip != null)
            balloonPopAudio.PlayOneShot(balloonPopAudio.clip);
        if (balloon.IsStartTarget)
        {
            if (state == RoundState.Ready || state == RoundState.Finished)
                StartCoroutine(BeginRoundSequence());
            return;
        }

        if (state != RoundState.Playing)
            return;

        combo++;
        int comboBonus = Mathf.Min(25, Mathf.Max(0, combo - 1) * 2);
        int gained = balloon.Points + comboBonus;
        score += gained;
        ShowCombo(gained);
        HoloScorePopup.Create(balloon.transform.position, gained, balloon.FeedbackColor,
            playerCamera, scoreText != null ? scoreText.font : null);
        UpdateHud();
    }

    public void BalloonMissed(HoloBalloonTarget balloon)
    {
        activeBalloons.Remove(balloon);
        if (state == RoundState.Playing)
        {
            combo = 0;
            if (comboText != null) comboText.text = string.Empty;
        }
    }

    private IEnumerator BeginRoundSequence()
    {
        if (state == RoundState.Countdown || state == RoundState.Playing)
            yield break;

        state = RoundState.Countdown;
        ClearSpawnedBalloons();
        score = 0;
        combo = 0;
        timeRemaining = RoundDuration;
        statusText.text = "SISTEMA ARMADO  //  PREPARE-SE";
        centerGroup.alpha = 1f;

        for (int i = Mathf.CeilToInt(CountdownDuration); i > 0; i--)
        {
            centerText.text = i.ToString("00");
            timerText.text = "00:" + i.ToString("00");
            yield return new WaitForSeconds(1f);
        }

        centerText.text = "VAI!";
        statusText.text = "ALVOS ATIVOS  //  MANTENHA O COMBO";
        state = RoundState.Playing;
        nextSpawn = Time.time;
        UpdateHud();
        yield return new WaitForSeconds(0.65f);
        if (state == RoundState.Playing) centerGroup.alpha = 0f;
    }

    private void FinishRound()
    {
        if (state != RoundState.Playing)
            return;

        state = RoundState.Finished;
        bestScore = Mathf.Max(bestScore, score);
        ClearSpawnedBalloons();
        centerGroup.alpha = 1f;
        centerText.text = "RODADA CONCLUIDA\n<size=55%><color=#59F7FF>PONTOS  " + score.ToString("0000") +
                          "</color>   <color=#FF4FD8>RECORDE  " + bestScore.ToString("0000") + "</color></size>";
        statusText.text = "ATIRE NO NUCLEO PARA JOGAR NOVAMENTE";
        scoreText.text = score.ToString("0000");
        timerText.text = "00:00";
        StartCoroutine(SpawnRestartTargetAfterDelay());
    }

    private IEnumerator SpawnRestartTargetAfterDelay()
    {
        yield return new WaitForSeconds(1.1f);
        if (state == RoundState.Finished)
            SpawnBalloon(true);
    }

    private void ShowReadyState()
    {
        state = RoundState.Ready;
        score = 0;
        combo = 0;
        timeRemaining = RoundDuration;
        scoreText.text = "0000";
        timerText.text = "00:45";
        comboText.text = string.Empty;
        centerGroup.alpha = 1f;
        centerText.text = "NEON POP\n<size=48%><color=#59F7FF>PEGUE A ARMA</color>  //  <color=#FF4FD8>ESTOURE O NUCLEO</color></size>";
        statusText.text = "ATIRE NO BALAO CENTRAL PARA INICIAR";
        SpawnBalloon(true);
    }

    private void SpawnBalloon(bool startTarget)
    {
        if (balloonTemplate == null)
            return;

        GameObject balloon = Instantiate(balloonTemplate);
        balloon.name = startTarget ? "Start Balloon - Shoot To Begin" : "Holographic Balloon";
        balloon.SetActive(true);
        balloon.transform.SetParent(arenaRoot, true);

        OscillateByAxis oldMotion = balloon.GetComponent<OscillateByAxis>();
        if (oldMotion != null) oldMotion.enabled = false;

        Rigidbody body = balloon.GetComponent<Rigidbody>();
        if (body == null) body = balloon.AddComponent<Rigidbody>();
        body.useGravity = false;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;

        int points;
        float speed;
        Material material;
        Color color;
        if (startTarget)
        {
            points = 0;
            speed = 0f;
            material = goldMaterial;
            color = new Color(1f, 0.75f, 0.16f);
            balloon.transform.position = arenaCenter + Vector3.up * 1.8f - forward * 0.8f;
            balloon.transform.localScale = Vector3.one * 0.78f;
        }
        else
        {
            float roll = Random.value;
            if (roll < 0.58f)
            {
                points = 10; speed = Random.Range(1.0f, 1.35f); material = cyanMaterial; color = new Color(0.2f, 0.95f, 1f);
            }
            else if (roll < 0.87f)
            {
                points = 20; speed = Random.Range(1.35f, 1.7f); material = magentaMaterial; color = new Color(1f, 0.2f, 0.78f);
            }
            else
            {
                points = 30; speed = Random.Range(1.7f, 2.05f); material = goldMaterial; color = new Color(1f, 0.72f, 0.12f);
            }

            float lane = Random.Range(-4.4f, 4.4f);
            float depth = Random.Range(-0.8f, 1.2f);
            balloon.transform.position = arenaCenter + right * lane + forward * depth + Vector3.up * 0.45f;
            float scale = Random.Range(0.5f, 0.78f);
            balloon.transform.localScale = Vector3.one * scale;
        }

        foreach (Renderer renderer in balloon.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterial = material;

        HoloBalloonTarget target = balloon.GetComponent<HoloBalloonTarget>();
        if (target == null) target = balloon.AddComponent<HoloBalloonTarget>();
        target.Configure(this, points, speed, startTarget, color, forward, right);
        target.BuildHologram(material, darkMaterial, playerCamera);
        activeBalloons.Add(target);
    }

    private void ClearSpawnedBalloons()
    {
        for (int i = activeBalloons.Count - 1; i >= 0; i--)
            if (activeBalloons[i] != null) Destroy(activeBalloons[i].gameObject);
        activeBalloons.Clear();
    }

    private void UpdateHud()
    {
        if (scoreText == null) return;
        scoreText.text = score.ToString("0000");
        int seconds = Mathf.CeilToInt(timeRemaining);
        timerText.text = "00:" + seconds.ToString("00");
    }

    private void ShowCombo(int gained)
    {
        if (comboText == null) return;
        comboText.text = combo > 1
            ? "COMBO x" + combo + "   <color=#59F7FF>+" + gained + "</color>"
            : "+" + gained;
        StopCoroutine("FadeCombo");
        StartCoroutine("FadeCombo");
    }

    private IEnumerator FadeCombo()
    {
        yield return new WaitForSeconds(1.15f);
        if (comboText != null) comboText.text = string.Empty;
    }

    private void CreateMaterials()
    {
        darkMaterial = CreateMaterial("Absolute Black", new Color(0.001f, 0.001f, 0.002f, 1f), Color.black, false);
        cyanMaterial = CreateMaterial("Holo Cyan", new Color(0.05f, 0.5f, 0.58f, 0.72f), new Color(0.1f, 3.8f, 5.2f), true);
        magentaMaterial = CreateMaterial("Holo Magenta", new Color(0.62f, 0.05f, 0.42f, 0.72f), new Color(5.2f, 0.08f, 3.4f), true);
        goldMaterial = CreateMaterial("Holo Gold", new Color(0.72f, 0.42f, 0.03f, 0.78f), new Color(5.0f, 2.0f, 0.08f), true);
    }

    private static Material CreateMaterial(string name, Color baseColor, Color emission, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        material.color = baseColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.82f);
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        return material;
    }

    private void BuildArenaVisuals()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.fog = false;
        BlackoutExistingEnvironment();

        const float roomWidth = 12f;
        const float roomDepth = 15f;
        const float roomHeight = 6.2f;
        const float edge = 0.035f;
        Vector3 roomCenter = playerCamera.transform.position + forward * 3.4f;
        roomCenter.y = floorY;
        float halfWidth = roomWidth * 0.5f;
        float halfDepth = roomDepth * 0.5f;

        // Opaque black shell: floor, ceiling and all four walls.
        CreateBox("BLACK ROOM // FLOOR", roomCenter + Vector3.down * 0.08f,
            new Vector3(roomWidth, 0.16f, roomDepth), darkMaterial);
        CreateBox("BLACK ROOM // CEILING", roomCenter + Vector3.up * roomHeight,
            new Vector3(roomWidth, 0.16f, roomDepth), darkMaterial);
        CreateBox("BLACK ROOM // LEFT WALL", roomCenter - right * halfWidth + Vector3.up * (roomHeight * 0.5f),
            new Vector3(0.16f, roomHeight, roomDepth), darkMaterial);
        CreateBox("BLACK ROOM // RIGHT WALL", roomCenter + right * halfWidth + Vector3.up * (roomHeight * 0.5f),
            new Vector3(0.16f, roomHeight, roomDepth), darkMaterial);
        CreateBox("BLACK ROOM // FRONT WALL", roomCenter + forward * halfDepth + Vector3.up * (roomHeight * 0.5f),
            new Vector3(roomWidth, roomHeight, 0.16f), darkMaterial);
        CreateBox("BLACK ROOM // REAR WALL", roomCenter - forward * halfDepth + Vector3.up * (roomHeight * 0.5f),
            new Vector3(roomWidth, roomHeight, 0.16f), darkMaterial);

        // Floor and ceiling perimeter lines.
        for (int level = 0; level < 2; level++)
        {
            float y = level == 0 ? 0.025f : roomHeight - 0.025f;
            Material horizontal = level == 0 ? cyanMaterial : magentaMaterial;
            CreateBox("NEON // HORIZONTAL LEFT", roomCenter - right * (halfWidth - 0.09f) + Vector3.up * y,
                new Vector3(edge, edge, roomDepth - 0.18f), horizontal);
            CreateBox("NEON // HORIZONTAL RIGHT", roomCenter + right * (halfWidth - 0.09f) + Vector3.up * y,
                new Vector3(edge, edge, roomDepth - 0.18f), horizontal);
            CreateBox("NEON // HORIZONTAL FRONT", roomCenter + forward * (halfDepth - 0.09f) + Vector3.up * y,
                new Vector3(roomWidth - 0.18f, edge, edge), horizontal);
            CreateBox("NEON // HORIZONTAL REAR", roomCenter - forward * (halfDepth - 0.09f) + Vector3.up * y,
                new Vector3(roomWidth - 0.18f, edge, edge), horizontal);
        }

        // Four luminous vertical corners provide an immediate depth reference in VR.
        CreateBox("NEON // CORNER LF", roomCenter - right * (halfWidth - 0.09f) + forward * (halfDepth - 0.09f) + Vector3.up * (roomHeight * 0.5f),
            new Vector3(edge, roomHeight - 0.12f, edge), cyanMaterial);
        CreateBox("NEON // CORNER RF", roomCenter + right * (halfWidth - 0.09f) + forward * (halfDepth - 0.09f) + Vector3.up * (roomHeight * 0.5f),
            new Vector3(edge, roomHeight - 0.12f, edge), magentaMaterial);
        CreateBox("NEON // CORNER LR", roomCenter - right * (halfWidth - 0.09f) - forward * (halfDepth - 0.09f) + Vector3.up * (roomHeight * 0.5f),
            new Vector3(edge, roomHeight - 0.12f, edge), magentaMaterial);
        CreateBox("NEON // CORNER RR", roomCenter + right * (halfWidth - 0.09f) - forward * (halfDepth - 0.09f) + Vector3.up * (roomHeight * 0.5f),
            new Vector3(edge, roomHeight - 0.12f, edge), cyanMaterial);

        // Sparse wall seams keep the room readable without turning it into a bright grid.
        for (int i = -2; i <= 2; i++)
        {
            CreateBox("NEON // FRONT WALL SEAM", roomCenter + right * (i * 2.1f) + forward * (halfDepth - 0.1f) + Vector3.up * (roomHeight * 0.5f),
                new Vector3(0.018f, roomHeight - 0.3f, 0.018f), i == 0 ? magentaMaterial : cyanMaterial);
        }

        BuildWorkbench();

        CreateLight("Cyan Edge Bounce", roomCenter - right * 3.6f + Vector3.up * 2.7f, new Color(0.05f, 0.65f, 1f), 0.75f, 6f);
        CreateLight("Magenta Edge Bounce", roomCenter + right * 3.6f + Vector3.up * 2.7f, new Color(1f, 0.05f, 0.55f), 0.75f, 6f);
    }

    private void BlackoutExistingEnvironment()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        foreach (Renderer renderer in renderers)
        {
            string objectName = renderer.gameObject.name.ToLowerInvariant();
            Transform parent = renderer.transform.parent;
            if (parent != null) objectName += " " + parent.name.ToLowerInvariant();

            if (objectName.Contains("parede") || objectName.Contains("piso") ||
                objectName.Contains("mesa") || objectName.Contains("bancada") ||
                objectName.Contains("cube"))
            {
                Material[] blackMaterials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < blackMaterials.Length; i++) blackMaterials[i] = darkMaterial;
                renderer.sharedMaterials = blackMaterials;
            }
        }

        foreach (Light sceneLight in Object.FindObjectsByType<Light>())
        {
            if (sceneLight.type == LightType.Directional)
                sceneLight.intensity = Mathf.Min(sceneLight.intensity, 0.08f);
        }
    }

    private void BuildWorkbench()
    {
        Vector3 benchCenter = playerCamera.transform.position + forward * 2.05f;
        benchCenter.y = floorY;

        CreateBox("BLACK WORKBENCH // TOP", benchCenter + Vector3.up * 0.94f,
            new Vector3(3.8f, 0.16f, 1.15f), darkMaterial);
        CreateBox("BLACK WORKBENCH // FRONT", benchCenter - forward * 0.5f + Vector3.up * 0.48f,
            new Vector3(3.8f, 0.82f, 0.12f), darkMaterial);
        CreateBox("BLACK WORKBENCH // LEFT LEG", benchCenter - right * 1.7f + Vector3.up * 0.46f,
            new Vector3(0.16f, 0.92f, 0.82f), darkMaterial);
        CreateBox("BLACK WORKBENCH // RIGHT LEG", benchCenter + right * 1.7f + Vector3.up * 0.46f,
            new Vector3(0.16f, 0.92f, 0.82f), darkMaterial);

        CreateBox("NEON BENCH // FRONT EDGE", benchCenter - forward * 0.58f + Vector3.up * 1.025f,
            new Vector3(3.86f, 0.035f, 0.035f), cyanMaterial);
        CreateBox("NEON BENCH // BACK EDGE", benchCenter + forward * 0.58f + Vector3.up * 1.025f,
            new Vector3(3.86f, 0.035f, 0.035f), magentaMaterial);
        CreateBox("NEON BENCH // LEFT EDGE", benchCenter - right * 1.92f + Vector3.up * 1.025f,
            new Vector3(0.035f, 0.035f, 1.18f), cyanMaterial);
        CreateBox("NEON BENCH // RIGHT EDGE", benchCenter + right * 1.92f + Vector3.up * 1.025f,
            new Vector3(0.035f, 0.035f, 1.18f), magentaMaterial);
        CreateBox("NEON BENCH // LEFT LEG", benchCenter - right * 1.78f - forward * 0.51f + Vector3.up * 0.47f,
            new Vector3(0.035f, 0.86f, 0.035f), cyanMaterial);
        CreateBox("NEON BENCH // RIGHT LEG", benchCenter + right * 1.78f - forward * 0.51f + Vector3.up * 0.47f,
            new Vector3(0.035f, 0.86f, 0.035f), magentaMaterial);
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject("NEON POP - World HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(arenaRoot, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1100f, 620f);
        canvasRect.localScale = Vector3.one * 0.0035f;
        canvasRect.position = arenaCenter + Vector3.up * 3.2f + forward * 3.6f;
        canvasRect.rotation = Quaternion.LookRotation(forward, Vector3.up);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 18f;

        GameObject topPanel = CreatePanel(canvasRect, "Header Panel", new Vector2(0f, 205f), new Vector2(1040f, 150f), new Color(0.015f, 0.025f, 0.07f, 0.92f), new Color(0.1f, 0.9f, 1f, 0.95f));
        CreateText(topPanel.transform, "NEON POP  //  SIMULATION ARENA", 42, TextAlignmentOptions.MidlineLeft,
            new Vector2(-270f, 43f), new Vector2(450f, 58f), new Color(0.45f, 0.96f, 1f));
        CreateText(topPanel.transform, "PONTOS", 22, TextAlignmentOptions.Center,
            new Vector2(165f, 52f), new Vector2(180f, 32f), new Color(0.6f, 0.76f, 0.86f));
        scoreText = CreateText(topPanel.transform, "0000", 58, TextAlignmentOptions.Center,
            new Vector2(165f, 5f), new Vector2(220f, 72f), new Color(0.25f, 0.98f, 1f));
        CreateText(topPanel.transform, "TEMPO", 22, TextAlignmentOptions.Center,
            new Vector2(395f, 52f), new Vector2(180f, 32f), new Color(0.6f, 0.76f, 0.86f));
        timerText = CreateText(topPanel.transform, "00:45", 58, TextAlignmentOptions.Center,
            new Vector2(395f, 5f), new Vector2(220f, 72f), new Color(1f, 0.3f, 0.82f));
        statusText = CreateText(topPanel.transform, "INICIALIZANDO", 22, TextAlignmentOptions.MidlineLeft,
            new Vector2(-260f, -43f), new Vector2(760f, 35f), new Color(0.67f, 0.78f, 0.9f));
        comboText = CreateText(topPanel.transform, string.Empty, 27, TextAlignmentOptions.MidlineRight,
            new Vector2(365f, -43f), new Vector2(290f, 38f), new Color(1f, 0.74f, 0.2f));

        GameObject centerPanel = CreatePanel(canvasRect, "Center Message", Vector2.zero, new Vector2(900f, 220f),
            new Color(0.01f, 0.018f, 0.055f, 0.9f), new Color(1f, 0.22f, 0.72f, 0.9f));
        centerGroup = centerPanel.AddComponent<CanvasGroup>();
        centerText = CreateText(centerPanel.transform, "NEON POP", 63, TextAlignmentOptions.Center,
            Vector2.zero, new Vector2(850f, 180f), Color.white);
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color fill, Color edge)
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

    private TMP_Text CreateText(Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 rectSize, Color color)
    {
        GameObject textObject = new GameObject("Vector Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
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

    private GameObject CreateBox(string name, Vector3 position, Vector3 axisDimensions, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(arenaRoot, true);
        box.transform.position = position;
        box.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        box.transform.localScale = new Vector3(axisDimensions.x, axisDimensions.y, axisDimensions.z);
        Collider collider = box.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private void CreateLight(string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject(name, typeof(Light));
        lightObject.transform.SetParent(arenaRoot, true);
        lightObject.transform.position = position;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }
}

public sealed class HoloBalloonTarget : MonoBehaviour
{
    private HoloBalloonArena arena;
    private float riseSpeed;
    private float phase;
    private float bornAt;
    private Vector3 right;
    private Vector3 startPosition;
    private Color holoColor;
    private bool hit;
    private Transform halo;
    private Camera playerCamera;

    public int Points { get; private set; }
    public bool IsStartTarget { get; private set; }
    public Color FeedbackColor { get { return holoColor; } }

    public void Configure(HoloBalloonArena owner, int points, float speed, bool isStart, Color color, Vector3 arenaForward, Vector3 arenaRight)
    {
        arena = owner;
        Points = points;
        riseSpeed = speed;
        IsStartTarget = isStart;
        holoColor = color;
        right = arenaRight;
        phase = Random.Range(0f, Mathf.PI * 2f);
        bornAt = Time.time;
        startPosition = transform.position;
    }

    public void BuildHologram(Material glowMaterial, Material darkMaterial, Camera camera)
    {
        playerCamera = camera;

        GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        inner.name = "Hologram Inner Core";
        inner.transform.SetParent(transform, false);
        inner.transform.localScale = Vector3.one * 0.72f;
        Destroy(inner.GetComponent<Collider>());
        inner.GetComponent<Renderer>().sharedMaterial = darkMaterial;

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Hologram Scan Ring";
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        ring.transform.localScale = new Vector3(0.78f, 0.018f, 0.78f);
        Destroy(ring.GetComponent<Collider>());
        ring.GetComponent<Renderer>().sharedMaterial = glowMaterial;
        halo = ring.transform;

        GameObject knot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        knot.name = "Balloon Knot";
        knot.transform.SetParent(transform, false);
        knot.transform.localPosition = new Vector3(0f, -0.62f, 0f);
        knot.transform.localScale = new Vector3(0.08f, 0.13f, 0.08f);
        Destroy(knot.GetComponent<Collider>());
        knot.GetComponent<Renderer>().sharedMaterial = glowMaterial;

        GameObject stringLine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stringLine.name = "Energy Tether";
        stringLine.transform.SetParent(transform, false);
        stringLine.transform.localPosition = new Vector3(0f, -1.18f, 0f);
        stringLine.transform.localScale = new Vector3(0.012f, 0.55f, 0.012f);
        Destroy(stringLine.GetComponent<Collider>());
        stringLine.GetComponent<Renderer>().sharedMaterial = glowMaterial;
    }

    private void Update()
    {
        if (hit || arena == null)
            return;

        if (halo != null)
        {
            halo.Rotate(Vector3.up, (IsStartTarget ? 95f : 160f) * Time.deltaTime, Space.Self);
            float pulse = 0.76f + Mathf.Sin(Time.time * 4f + phase) * 0.08f;
            halo.localScale = new Vector3(pulse, 0.018f, pulse);
        }

        if (IsStartTarget)
        {
            transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * 1.8f + phase) * 0.16f);
            transform.Rotate(Vector3.up, 22f * Time.deltaTime, Space.World);
            return;
        }

        if (!arena.IsRoundActive)
            return;

        float sway = Mathf.Sin(Time.time * 2.2f + phase) * 0.38f;
        transform.position += (Vector3.up * riseSpeed + right * sway) * Time.deltaTime;
        transform.Rotate(Vector3.up, 18f * Time.deltaTime, Space.World);

        if (transform.position.y >= arena.DespawnHeight || Time.time - bornAt > 10f)
        {
            arena.BalloonMissed(this);
            Destroy(gameObject);
        }
    }

    public void Hit(Vector3 hitPoint)
    {
        if (hit) return;
        hit = true;
        if (arena != null) arena.BalloonHit(this);
        HoloPopBurst.Create(transform.position, holoColor);
        StartCoroutine(PopAnimation());
    }

    private IEnumerator PopAnimation()
    {
        foreach (Collider item in GetComponentsInChildren<Collider>()) item.enabled = false;
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < 0.13f)
        {
            elapsed += Time.deltaTime;
            transform.localScale = originalScale * Mathf.Lerp(1f, 1.34f, elapsed / 0.13f);
            yield return null;
        }
        foreach (Renderer item in GetComponentsInChildren<Renderer>()) item.enabled = false;
        yield return new WaitForSeconds(0.45f);
        Destroy(gameObject);
    }
}

/// <summary>Independent world-space feedback; does not move or modify the scene HUD.</summary>
public sealed class HoloScorePopup : MonoBehaviour
{
    private const float Lifetime = 1.1f;
    private TextMeshPro label;
    private Camera viewer;
    private Vector3 origin;
    private float age;

    public static void Create(Vector3 position, int gained, Color color, Camera camera, TMP_FontAsset font)
    {
        GameObject root = new GameObject("Balloon Points +" + gained);
        HoloScorePopup popup = root.AddComponent<HoloScorePopup>();
        popup.viewer = camera;
        popup.origin = position + Vector3.up * 0.2f;
        if (camera != null)
            popup.origin += (camera.transform.position - position).normalized * 0.25f;
        root.transform.position = popup.origin;
        popup.label = root.AddComponent<TextMeshPro>();
        if (font != null) popup.label.font = font;
        popup.label.text = "+" + gained;
        popup.label.fontSize = 4f;
        popup.label.fontStyle = FontStyles.Bold;
        popup.label.alignment = TextAlignmentOptions.Center;
        popup.label.textWrappingMode = TextWrappingModes.NoWrap;
        popup.label.color = color;
        popup.label.rectTransform.sizeDelta = new Vector2(2f, 0.7f);
        popup.FaceViewer();
        Destroy(root, Lifetime + 0.1f);
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Lifetime);
        transform.position = origin + Vector3.up * (0.85f * progress);
        label.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, progress));
        FaceViewer();
        if (age >= Lifetime) Destroy(gameObject);
    }

    private void FaceViewer()
    {
        if (viewer == null) return;
        Vector3 awayFromViewer = transform.position - viewer.transform.position;
        if (awayFromViewer.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(awayFromViewer, viewer.transform.up);
    }
}

public sealed class HoloPopBurst : MonoBehaviour
{
    private readonly List<Transform> shards = new List<Transform>();
    private readonly List<Vector3> directions = new List<Vector3>();
    private float bornAt;

    public static void Create(Vector3 position, Color color)
    {
        GameObject root = new GameObject("Hologram Pop Burst");
        root.transform.position = position;
        HoloPopBurst burst = root.AddComponent<HoloPopBurst>();
        burst.Build(color);
    }

    private void Build(Color color)
    {
        bornAt = Time.time;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader);
        material.color = color * 2.2f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color * 2.2f);

        for (int i = 0; i < 14; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
            shard.name = "Vector Spark";
            shard.transform.SetParent(transform, false);
            shard.transform.localScale = Vector3.one * Random.Range(0.035f, 0.085f);
            Destroy(shard.GetComponent<Collider>());
            shard.GetComponent<Renderer>().sharedMaterial = material;
            shards.Add(shard.transform);
            directions.Add(Random.onUnitSphere.normalized * Random.Range(1.8f, 3.8f));
        }
    }

    private void Update()
    {
        float age = Time.time - bornAt;
        for (int i = 0; i < shards.Count; i++)
        {
            if (shards[i] == null) continue;
            shards[i].position += directions[i] * Time.deltaTime;
            shards[i].localScale = Vector3.one * Mathf.Max(0f, 0.075f * (1f - age / 0.5f));
        }
        if (age > 0.52f) Destroy(gameObject);
    }
}

/// <summary>Serialized links created by the editor baker so the arena stays editable in the scene.</summary>
public sealed class HoloArenaBakedReferences : MonoBehaviour
{
    public Transform spawnCenter;
    public Material darkMaterial;
    public Material cyanMaterial;
    public Material magentaMaterial;
    public Material goldMaterial;
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public TMP_Text statusText;
    public TMP_Text centerText;
    public TMP_Text comboText;
    public CanvasGroup centerGroup;
}
