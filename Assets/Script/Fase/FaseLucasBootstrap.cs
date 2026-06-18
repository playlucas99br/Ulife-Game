using UnityEngine;
using UnityEngine.UI;

namespace FaseLucasGame
{
    /// <summary>
    /// Builds the entire FaseLucas level at runtime: enclosed arena, the two lights
    /// (Luz1/Luz2), player + FPS camera, the rope-hung magnet (Ima), the red Cube / blue
    /// Sphere spawner, the incinerator, the score/challenge logic, the retracting magnet +
    /// extending bridge, the exit portal, the HUD and the TAB visual-programming interface.
    ///
    /// Put this component on an empty GameObject in an otherwise empty scene and press Play,
    /// or use the Tools > Fase Lucas menu to generate the scene automatically.
    /// </summary>
    public class FaseLucasBootstrap : MonoBehaviour
    {
        [Header("Corridor layout")]
        public float corridorLength = 80f;   // Z extent (long)
        public float corridorWidth = 16f;    // X extent (narrow)
        public float corridorHeight = 24f;   // Y extent (tall)
        public float balconyTopY = 17f;      // walking height of the spawn/final balconies (near ceiling)
        public float balconyDepth = 9f;      // how far the balconies reach into the corridor (Z)
        public float doorwayWidth = 6f;      // opening that the bridge passes through

        // derived
        float LenHalf => corridorLength * 0.5f;
        float WidthHalf => corridorWidth * 0.5f;
        float SpawnEdgeZ => -LenHalf + balconyDepth;   // inner edge of spawn balcony
        float FinalEdgeZ => LenHalf - balconyDepth;     // inner edge of final balcony

        Material metalDark;
        Material metalMid;
        Material redMat;
        Material blueMat;
        Material emissiveAccent;
        Material energyField;
        Material spawnFieldFaint;   // much fainter variant used only for the spawn doorway barrier

        Transform spawnPoint;
        Transform finalPoint;
        MagnetController magnet;
        ObjectSpawner spawner;
        BridgeController bridge;
        GameObject spawnBarrier;

        void Awake()
        {
            CleanupExisting();
            CreateMaterials();
            ConfigureEnvironment();
            BuildArena();
            BuildLights();
            BuildSpawnAndFinal();
            BuildBridge();
            BuildCeilingAndMagnet();
            BuildIncinerator();
            BuildSpawner();
            BuildPlayer();
            BuildPortal();
            BuildScoreAndProgrammer();
        }

        // ---------------------------------------------------------------- setup

        void CleanupExisting()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                Destroy(l.gameObject);
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Destroy(c.gameObject);
        }

        void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.085f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.04f, 1f);
            RenderSettings.fogDensity = 0.012f;
        }

        void CreateMaterials()
        {
            metalDark = MakeMetal(new Color(0.10f, 0.11f, 0.13f), 0.92f, 0.45f);
            metalMid = MakeMetal(new Color(0.18f, 0.19f, 0.22f), 0.85f, 0.6f);
            redMat = MakeMetal(new Color(0.7f, 0.08f, 0.08f), 0.6f, 0.7f);
            blueMat = MakeMetal(new Color(0.08f, 0.2f, 0.75f), 0.6f, 0.7f);
            emissiveAccent = MakeMetal(new Color(0.12f, 0.13f, 0.15f), 0.8f, 0.6f);
            SetEmission(emissiveAccent, new Color(0.2f, 0.6f, 0.9f) * 1.5f);
            energyField = MakeTransparent(new Color(0.2f, 0.6f, 0.95f, 0.22f));
            SetEmission(energyField, new Color(0.2f, 0.6f, 0.95f) * 0.8f);

            // Dedicated, much more see-through field just for the spawn doorway barrier, so the
            // crane's movement in the pit is clearly visible. Kept separate from energyField so
            // the exit PortalSurface (and the BridgeController reference) are unaffected.
            spawnFieldFaint = MakeTransparent(new Color(0.2f, 0.6f, 0.95f, 0.07f));
            SetEmission(spawnFieldFaint, new Color(0.2f, 0.6f, 0.95f) * 0.12f);
        }

        Material MakeTransparent(Color color)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            var m = new Material(s);
            // URP Lit transparent setup.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }

        Material MakeMetal(Color baseColor, float metallic, float smoothness)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        void SetEmission(Material m, Color emission)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        // ---------------------------------------------------------------- geometry

        GameObject MakeBox(string name, Vector3 pos, Vector3 size, Material mat, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        void BuildArena()
        {
            // Fully enclosed tall corridor: long along Z, narrow along X, tall along Y.
            float t = 1f;
            float midY = corridorHeight * 0.5f;

            MakeBox("Floor", new Vector3(0, -0.5f, 0), new Vector3(corridorWidth, 1f, corridorLength), metalDark);

            // Long side walls (run along Z).
            MakeBox("Wall_West", new Vector3(-WidthHalf, midY, 0), new Vector3(t, corridorHeight, corridorLength), metalMid);
            MakeBox("Wall_East", new Vector3(WidthHalf, midY, 0), new Vector3(t, corridorHeight, corridorLength), metalMid);

            // End walls (cap the two ends).
            MakeBox("Wall_SpawnEnd", new Vector3(0, midY, -LenHalf), new Vector3(corridorWidth, corridorHeight, t), metalMid);
            MakeBox("Wall_FinalEnd", new Vector3(0, midY, LenHalf), new Vector3(corridorWidth, corridorHeight, t), metalMid);
        }

        void BuildLights()
        {
            // Only two sources, spaced along the corridor near the ceiling.
            float y = corridorHeight - 5f;
            CreateLight("Luz1", new Vector3(0, y, -corridorLength * 0.25f), new Color(0.7f, 0.8f, 1f), 220f, corridorLength * 0.95f);
            CreateLight("Luz2", new Vector3(0, y, corridorLength * 0.25f), new Color(1f, 0.85f, 0.7f), 220f, corridorLength * 0.95f);
        }

        void CreateLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.Soft;

            // small emissive fixture so the source is visible
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Fixture";
            bulb.transform.SetParent(go.transform, false);
            bulb.transform.localScale = Vector3.one * 0.6f;
            Destroy(bulb.GetComponent<Collider>());
            var bm = MakeMetal(new Color(0.1f, 0.1f, 0.1f), 0.5f, 0.5f);
            SetEmission(bm, color * 2f);
            bulb.GetComponent<MeshRenderer>().sharedMaterial = bm;
        }

        void BuildSpawnAndFinal()
        {
            // --- Spawn balcony (elevated, near the ceiling, at the -Z end) ---
            float spawnCenterZ = -LenHalf + balconyDepth * 0.5f;
            MakeBox("SpawnBalcony", new Vector3(0, balconyTopY - 0.5f, spawnCenterZ),
                new Vector3(corridorWidth, 1f, balconyDepth), metalMid);
            MakeBox("SpawnPad", new Vector3(0, balconyTopY + 0.02f, spawnCenterZ),
                new Vector3(doorwayWidth, 0.1f, 4f), emissiveAccent);

            var spawn = new GameObject("Spawn");
            spawn.transform.position = new Vector3(0, balconyTopY + 0.05f, spawnCenterZ);
            spawnPoint = spawn.transform;

            BuildFrontWall("SpawnFront", SpawnEdgeZ);

            // Removable energy field across the spawn doorway (drops when the bridge extends).
            // Uses the faint variant so the player can clearly watch the crane through it.
            spawnBarrier = MakeBox("SpawnBarrier",
                new Vector3(0, (balconyTopY + corridorHeight) * 0.5f, SpawnEdgeZ),
                new Vector3(doorwayWidth, corridorHeight - balconyTopY, 0.2f), spawnFieldFaint);
            Destroy(spawnBarrier.GetComponent<BoxCollider>());
            var barrierCol = spawnBarrier.AddComponent<BoxCollider>(); // solid so the player can't walk through yet
            barrierCol.size = Vector3.one;

            // --- Final balcony (elevated, at the +Z end) ---
            float finalCenterZ = LenHalf - balconyDepth * 0.5f;
            MakeBox("FinalBalcony", new Vector3(0, balconyTopY - 0.5f, finalCenterZ),
                new Vector3(corridorWidth, 1f, balconyDepth), metalMid);
            MakeBox("FinalPad", new Vector3(0, balconyTopY + 0.02f, finalCenterZ),
                new Vector3(doorwayWidth, 0.1f, 4f), emissiveAccent);

            var fp = new GameObject("FinalPoint");
            fp.transform.position = new Vector3(0, balconyTopY + 0.05f, finalCenterZ);
            finalPoint = fp.transform;

            BuildFrontWall("FinalFront", FinalEdgeZ);
        }

        // Builds the inner wall of a balcony as two segments leaving a central doorway.
        void BuildFrontWall(string name, float z)
        {
            float segWidth = (corridorWidth - doorwayWidth) * 0.5f;
            float segOffset = doorwayWidth * 0.5f + segWidth * 0.5f;
            float wallH = corridorHeight - balconyTopY;
            float wallY = balconyTopY + wallH * 0.5f;
            MakeBox(name + "_L", new Vector3(-segOffset, wallY, z), new Vector3(segWidth, wallH, 0.4f), metalMid);
            MakeBox(name + "_R", new Vector3(segOffset, wallY, z), new Vector3(segWidth, wallH, 0.4f), metalMid);
        }

        void BuildBridge()
        {
            // Spans the gap between the two balcony doorways, near the ceiling, along Z.
            float gap = FinalEdgeZ - SpawnEdgeZ;   // distance between the two doorways
            float deckTop = balconyTopY;
            float deckCenterY = deckTop - 0.2f;
            float railY = deckTop + 0.5f;
            float halfDoor = doorwayWidth * 0.5f;

            var root = new GameObject("Bridge");
            root.transform.position = new Vector3(0, deckCenterY, 0);

            var deck = MakeBox("BridgeDeck", new Vector3(0, deckCenterY, 0), new Vector3(doorwayWidth, 0.4f, 1f), metalMid);
            deck.transform.SetParent(root.transform, true);

            var railL = MakeBox("BridgeRailL", new Vector3(halfDoor - 0.1f, railY, 0), new Vector3(0.2f, 1f, 1f), emissiveAccent);
            var railR = MakeBox("BridgeRailR", new Vector3(-halfDoor + 0.1f, railY, 0), new Vector3(0.2f, 1f, 1f), emissiveAccent);
            railL.transform.SetParent(root.transform, true);
            railR.transform.SetParent(root.transform, true);

            bridge = root.AddComponent<BridgeController>();
            bridge.deck = deck.transform;
            bridge.extras = new Transform[] { railL.transform, railR.transform };
            bridge.barrier = spawnBarrier;
            bridge.retractedLength = 0.1f;
            bridge.extendedLength = gap;
            bridge.extendDuration = 4f;

            // Awake already ran (fields were unassigned), so apply the retracted state explicitly.
            bridge.ApplyRetracted();
        }

        void BuildCeilingAndMagnet()
        {
            // Teto: the corridor ceiling slab (encloses the top); the magnet hangs from it.
            float ceilingY = corridorHeight;
            MakeBox("Teto", new Vector3(0, ceilingY, 0), new Vector3(corridorWidth, 0.6f, corridorLength), metalDark);

            var anchorGO = new GameObject("TetoAnchor");
            anchorGO.transform.position = new Vector3(0, ceilingY - 0.4f, 0);

            // Ima magnet body, starting over the centre of the pit.
            var ima = MakeBox("Ima", new Vector3(0, 9f, 0), new Vector3(1.6f, 1.1f, 1.6f), emissiveAccent);
            // No solid collider on the magnet itself: it grabs via overlap + joint, so a
            // collider would only fight the grabbed object's physics.
            Destroy(ima.GetComponent<BoxCollider>());

            var rb = ima.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var grabPoint = new GameObject("GrabPoint");
            grabPoint.transform.SetParent(ima.transform, false);
            grabPoint.transform.localPosition = new Vector3(0, -0.65f, 0);

            // Rope visual (hangs straight down, gantry-style).
            var ropeGO = new GameObject("Rope");
            ropeGO.transform.SetParent(ima.transform, false);
            var lr = ropeGO.AddComponent<LineRenderer>();
            lr.widthMultiplier = 0.12f;
            lr.numCapVertices = 4;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            var ropeMat = new Material(unlit);
            if (ropeMat.HasProperty("_BaseColor")) ropeMat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.17f));
            lr.material = ropeMat;
            lr.textureMode = LineTextureMode.Tile;

            magnet = ima.AddComponent<MagnetController>();
            magnet.ceilingAnchor = anchorGO.transform;
            magnet.rope = lr;
            magnet.grabPoint = grabPoint.transform;
            // Operates in the open pit between the two balconies, below the bridge height.
            // The floor (Y) is kept high enough that the magnet's grab-point hovers just above
            // the cubes/spheres, so the downward colour sensor can always read what is below it.
            magnet.areaMin = new Vector3(-WidthHalf + 2f, 2.6f, SpawnEdgeZ + 1f);
            magnet.areaMax = new Vector3(WidthHalf - 2f, balconyTopY - 2.5f, FinalEdgeZ - 1f);
            magnet.maxSpeed = 17f;    // headroom so the faster search sweep isn't clamped away
            magnet.grabRadius = 2.4f; // forgiving pickup so the sweeping crane doesn't miss objects
        }

        void BuildIncinerator()
        {
            // Middle of the corridor, on the floor (bottom).
            Vector3 pos = new Vector3(0f, 0f, 0f);
            // Furnace housing (visual, solid).
            MakeBox("Incinerator_Base", pos + new Vector3(0, 0.5f, 0), new Vector3(6f, 1f, 6f), metalDark);
            MakeBox("Incinerator_Rim", pos + new Vector3(0, 1.1f, 0), new Vector3(5f, 0.4f, 5f), emissiveAccent);

            // Trigger volume in the mouth of the furnace.
            var trig = new GameObject("Incinerator");
            trig.transform.position = pos + new Vector3(0, 1.7f, 0);
            var bc = trig.AddComponent<BoxCollider>();
            bc.size = new Vector3(4.4f, 2.2f, 4.4f);
            bc.isTrigger = true;

            var flashGO = new GameObject("IncineratorFlash");
            flashGO.transform.SetParent(trig.transform, false);
            flashGO.transform.localPosition = new Vector3(0, 1.5f, 0);
            var flash = flashGO.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.95f, 0.8f);
            flash.range = 25f;
            flash.intensity = 0f;
            flash.enabled = false;

            var inc = trig.AddComponent<Incinerator>();
            inc.flashLight = flash;
            pendingIncinerator = inc;
        }

        Incinerator pendingIncinerator;

        void BuildSpawner()
        {
            var go = new GameObject("ObjectSpawner");
            spawner = go.AddComponent<ObjectSpawner>();
            spawner.redMaterial = redMat;
            spawner.blueMaterial = blueMat;
            // Spread along the corridor floor, avoiding the central furnace footprint.
            spawner.areaMin = new Vector2(-WidthHalf + 2.5f, SpawnEdgeZ + 2f);
            spawner.areaMax = new Vector2(WidthHalf - 2.5f, FinalEdgeZ - 2f);
            spawner.avoidCenterRadius = 4.5f;
            spawner.spawnY = 1f;
            spawner.objectScale = 1.3f;

            if (pendingIncinerator != null)
                pendingIncinerator.spawner = spawner;
        }

        void BuildPlayer()
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            // Stand on the spawn balcony, looking down the corridor toward Final (+Z).
            player.transform.position = spawnPoint.position + Vector3.up * 0.2f;
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 0.9f, 0);

            var camGO = new GameObject("PlayerCamera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(player.transform, false);
            camGO.transform.localPosition = new Vector3(0, 1.6f, 0);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.03f);
            cam.nearClipPlane = 0.05f;
            camGO.AddComponent<AudioListener>();

            var fps = player.AddComponent<PlayerFPS>();
            fps.cameraPivot = camGO.transform;
        }

        void BuildPortal()
        {
            // Portal saida sits at the far end wall, on the Final balcony.
            float py = balconyTopY + 2f;
            float pz = LenHalf - 0.6f;
            MakeBox("PortalFrame", new Vector3(0, py, pz), new Vector3(doorwayWidth, 4f, 0.4f), emissiveAccent);
            var surface = MakeBox("PortalSurface", new Vector3(0, py, pz - 0.25f), new Vector3(doorwayWidth - 0.6f, 3.6f, 0.1f), energyField);
            Destroy(surface.GetComponent<BoxCollider>());

            var portal = new GameObject("Portal saida");
            portal.transform.position = new Vector3(0, py, pz - 0.6f);
            var bc = portal.AddComponent<BoxCollider>();
            bc.size = new Vector3(doorwayWidth - 0.6f, 3.8f, 1.2f);
            bc.isTrigger = true;
            var ps = portal.AddComponent<PortalSaida>();
            ps.targetScene = "Industrial_Zone";
        }

        void BuildScoreAndProgrammer()
        {
            // HUD
            var canvasGO = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var scoreText = UIFactory.Label("Score", canvasGO.transform, "Vermelhos: 0/3    Azuis: 0/3", 30, TextAnchor.UpperCenter);
            scoreText.rectTransform.anchorMin = new Vector2(0.5f, 1);
            scoreText.rectTransform.anchorMax = new Vector2(0.5f, 1);
            scoreText.rectTransform.pivot = new Vector2(0.5f, 1);
            scoreText.rectTransform.anchoredPosition = new Vector2(0, -24);
            scoreText.rectTransform.sizeDelta = new Vector2(900, 50);

            var hint = UIFactory.Label("Hint", canvasGO.transform,
                "TAB: abrir programacao do Ima   |   WASD mover, Mouse olhar", 20, TextAnchor.LowerCenter);
            hint.rectTransform.anchorMin = new Vector2(0.5f, 0);
            hint.rectTransform.anchorMax = new Vector2(0.5f, 0);
            hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 20);
            hint.rectTransform.sizeDelta = new Vector2(1000, 40);
            hint.color = new Color(0.7f, 0.75f, 0.8f, 1f);

            // crosshair
            var cross = UIFactory.Label("Crosshair", canvasGO.transform, "+", 28, TextAnchor.MiddleCenter);
            cross.rectTransform.anchorMin = cross.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cross.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cross.rectTransform.sizeDelta = new Vector2(40, 40);

            // Score manager
            var smGO = new GameObject("ScoreManager");
            var sm = smGO.AddComponent<ScoreManager>();
            sm.targetPerColor = 3;
            sm.magnet = magnet;
            sm.bridge = bridge;
            sm.scoreText = scoreText;

            // Programmer (builds its own canvas)
            var progGO = new GameObject("MagnetProgrammer");
            var prog = progGO.AddComponent<MagnetProgramUI>();
            prog.magnet = magnet;
        }
    }
}
