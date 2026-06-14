using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FaseLucasGame
{
    /// <summary>
    /// Difficulty of the magnet programming puzzle.
    /// <para>Easy (default): the palette offers a few high-level "smart" blocks (Search,
    /// Deliver, NeedBelow) so the whole challenge can be solved with a handful of nodes.</para>
    /// <para>Hard: only the low-level granular blocks are available, so the player has to
    /// build the sweep, the colour logic and the delivery by hand.</para>
    /// </summary>
    public enum Difficulty { Easy, Hard }

    /// <summary>
    /// TAB-toggled visual programming interface for the magnet (Ima). The player wires
    /// sensors, logic gates, comparisons, variables and XYZ/grab actions into a data-flow
    /// graph that is evaluated every physics step to drive the magnet.
    ///
    /// The whole UI is generated from code so no prefabs/scene wiring are required.
    /// </summary>
    public class MagnetProgramUI : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        public MagnetController magnet;

        [Tooltip("Easy mode (default) exposes high-level blocks; Hard mode only the granular ones.")]
        public Difficulty difficulty = Difficulty.Easy;

        // built UI
        GameObject panel;
        RectTransform nodeArea;     // viewport (masked) that clips the graph
        RectTransform nodeCanvas;   // pannable/zoomable root where nodes live
        RectTransform lineLayer;    // connection lines, behind nodes
        Text readout;
        Text statusText;
        Transform paletteContent;   // the (rebuildable) list of palette buttons
        Text difficultyLabel;       // label on the mode-toggle button

        readonly List<ProgramNode> nodes = new List<ProgramNode>();

        class LineLink
        {
            public ProgramNode from;
            public ProgramNode to;
            public int inputIndex;
            public RectTransform line;
        }
        readonly List<LineLink> links = new List<LineLink>();

        ProgramNode pendingOutput;
        readonly Dictionary<string, float> variables = new Dictionary<string, float>();

        bool running = true;
        int spawnCounter;

        // per-column "next Y" cursor used to lay the example program out in tidy columns
        Dictionary<int, float> exampleColY;

        void Awake()
        {
            EnsureEventSystem();
            BuildUI();
            panel.SetActive(false);
            IsOpen = false;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                Toggle();

            if (panel.activeSelf)
                UpdateReadout();
        }

        void FixedUpdate()
        {
            if (magnet == null) return;
            if (!running)
            {
                magnet.Tick(Vector3.zero, false, false);
                return;
            }
            EvaluateGraph();
        }

        void LateUpdate()
        {
            if (panel.activeSelf)
                RedrawLines();
        }

        void Toggle()
        {
            bool open = !panel.activeSelf;
            panel.SetActive(open);
            IsOpen = open;
            PlayerFPS.LockCursor(!open);
        }

        // ---------------------------------------------------------------- evaluation

        void EvaluateGraph()
        {
            foreach (var n in nodes) { n.evaluated = false; n.evaluating = false; }

            float vx = 0f, vy = 0f, vz = 0f;
            bool grab = false, release = false;
            var pendingWrites = new List<KeyValuePair<string, float>>();

            foreach (var n in nodes)
            {
                if (!n.spec.isSink) continue;
                switch (n.kind)
                {
                    case NodeKind.MoveX: vx += InputValue(n, 0); break;
                    case NodeKind.MoveY: vy += InputValue(n, 0); break;
                    case NodeKind.MoveZ: vz += InputValue(n, 0); break;
                    case NodeKind.Grab: if (InputValue(n, 0) > 0.5f) grab = true; break;
                    case NodeKind.Release: if (InputValue(n, 0) > 0.5f) release = true; break;
                    case NodeKind.VarSet:
                        pendingWrites.Add(new KeyValuePair<string, float>(n.VarName, InputValue(n, 0)));
                        break;

                    case NodeKind.Search:
                        if (InputValue(n, 0) > 0.5f)
                        {
                            Vector3 sv = SearchVelocity(n);
                            vx += sv.x; vy += sv.y; vz += sv.z;
                        }
                        break;
                    case NodeKind.Deliver:
                        if (InputValue(n, 0) > 0.5f)
                        {
                            Vector3 dv = DeliverVelocity(out bool drop);
                            vx += dv.x; vy += dv.y; vz += dv.z;
                            if (drop) release = true;
                        }
                        break;
                }
            }

            foreach (var w in pendingWrites)
                variables[w.Key] = w.Value;

            magnet.Tick(new Vector3(vx, vy, vz), grab, release);
        }

        float InputValue(ProgramNode node, int i)
        {
            if (node.inputSources == null || i >= node.inputSources.Length) return 0f;
            var src = node.inputSources[i];
            return src != null ? Eval(src) : 0f;
        }

        float Eval(ProgramNode n)
        {
            if (n.evaluated) return n.cached;
            if (n.evaluating) return 0f; // break accidental cycles (variables are the intended feedback path)
            n.evaluating = true;
            float r = Compute(n);
            n.evaluating = false;
            n.evaluated = true;
            n.cached = r;
            return r;
        }

        float Compute(ProgramNode n)
        {
            switch (n.kind)
            {
                case NodeKind.PosX: return magnet.SensorPosition.x;
                case NodeKind.PosY: return magnet.SensorPosition.y;
                case NodeKind.PosZ: return magnet.SensorPosition.z;
                case NodeKind.BelowColor: return magnet.SensorBelowColor;
                case NodeKind.BelowDistance: return magnet.SensorBelowDistance;
                case NodeKind.IsHolding: return magnet.IsHolding ? 1f : 0f;

                case NodeKind.RedScore: return RedScore;
                case NodeKind.BlueScore: return BlueScore;
                case NodeKind.Goal: return GoalPerColor;
                case NodeKind.NeedBelow: return NeedColor(magnet.SensorBelowColor) ? 1f : 0f;

                case NodeKind.Constant: return n.Value;
                case NodeKind.VarGet: return variables.TryGetValue(n.VarName, out float v) ? v : 0f;
                case NodeKind.VarSet: return InputValue(n, 0);

                case NodeKind.Add: return InputValue(n, 0) + InputValue(n, 1);
                case NodeKind.Sub: return InputValue(n, 0) - InputValue(n, 1);
                case NodeKind.Mul: return InputValue(n, 0) * InputValue(n, 1);

                case NodeKind.Greater: return InputValue(n, 0) > InputValue(n, 1) ? 1f : 0f;
                case NodeKind.Less: return InputValue(n, 0) < InputValue(n, 1) ? 1f : 0f;
                case NodeKind.Equals: return Mathf.Abs(InputValue(n, 0) - InputValue(n, 1)) < 0.001f ? 1f : 0f;

                case NodeKind.And: return (InputValue(n, 0) > 0.5f && InputValue(n, 1) > 0.5f) ? 1f : 0f;
                case NodeKind.Or: return (InputValue(n, 0) > 0.5f || InputValue(n, 1) > 0.5f) ? 1f : 0f;
                case NodeKind.Not: return InputValue(n, 0) > 0.5f ? 0f : 1f;
            }
            return 0f;
        }

        // ---------------------------------------------------------------- score / colour helpers

        int RedScore => ScoreManager.Instance != null ? ScoreManager.Instance.RedScore : 0;
        int BlueScore => ScoreManager.Instance != null ? ScoreManager.Instance.BlueScore : 0;
        int GoalPerColor => ScoreManager.Instance != null ? ScoreManager.Instance.targetPerColor : 3;

        /// <summary>True when a colour code (1=red, 2=blue) is one we still have to burn.</summary>
        bool NeedColor(int colorCode)
        {
            int goal = GoalPerColor;
            if (colorCode == 1) return RedScore < goal;   // red
            if (colorCode == 2) return BlueScore < goal;  // blue
            return false;                                  // nothing / unknown below
        }

        // ---------------------------------------------------------------- smart-block behaviours

        /// <summary>
        /// Self-contained search pattern: hug the floor and sweep fast in X while crawling in Z,
        /// bouncing off the magnet's own play-volume walls. Used by the easy-mode "Procurar" block.
        /// </summary>
        Vector3 SearchVelocity(ProgramNode n)
        {
            Vector3 p = magnet.SensorPosition;
            Vector3 mn = magnet.areaMin, mx = magnet.areaMax;
            const float padX = 0.6f, padZ = 1.0f;

            if (p.x > mx.x - padX) n.dirX = -1f;
            else if (p.x < mn.x + padX) n.dirX = 1f;
            if (p.z > mx.z - padZ) n.dirZ = -1f;
            else if (p.z < mn.z + padZ) n.dirZ = 1f;

            // Fast back-and-forth in X with a slow crawl in Z so the down sensor passes over
            // every object, while seeking the floor so the grab-point hovers just above them.
            const float speedX = 7f, speedZ = 0.8f;
            float vy = (mn.y - p.y) * 4f;
            return new Vector3(n.dirX * speedX, vy, n.dirZ * speedZ);
        }

        /// <summary>
        /// Self-contained delivery: lift clear of the furnace rim, slide to the origin (the
        /// incinerator) and drop the carried object straight in. Used by the easy-mode
        /// "Entregar no forno" block. Sets <paramref name="drop"/> when centred over the mouth.
        /// </summary>
        Vector3 DeliverVelocity(out bool drop)
        {
            Vector3 p = magnet.SensorPosition;
            const float liftY = 4.2f;

            float vy = (liftY - p.y) * 4f;
            bool high = p.y > 3.2f;                  // clear of the rim before sliding across
            float kxz = high ? 2f : 0f;
            float vx = (0f - p.x) * kxz;
            float vz = (0f - p.z) * kxz;

            drop = high && Mathf.Abs(p.x) < 0.7f && Mathf.Abs(p.z) < 0.7f;
            return new Vector3(vx, vy, vz);
        }

        // ---------------------------------------------------------------- UI build

        void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("MagnetProgramCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            panel = new GameObject("ProgramPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, false);
            var pr = panel.GetComponent<RectTransform>();
            UIFactory.Stretch(pr);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.07f, 0.85f);

            // Title
            var title = UIFactory.Label("Title", panel.transform,
                "PROGRAMACAO DO IMA   |   arraste blocos   |   clique SAIDA depois ENTRADA para ligar   |   RODA = zoom, arraste o fundo = navegar   |   MODO troca FACIL/DIFICIL   |   TAB fecha",
                15, TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(0, 1);
            title.rectTransform.anchoredPosition = new Vector2(16, -8);
            title.rectTransform.sizeDelta = new Vector2(-32, 30);
            title.color = UIFactory.Accent;

            BuildPalette();
            BuildCanvasArea();
            BuildReadout();
            BuildBottomBar();
        }

        void BuildPalette()
        {
            var palette = UIFactory.Paneled("Palette", panel.transform, UIFactory.Panel);
            palette.rectTransform.anchorMin = new Vector2(0, 0);
            palette.rectTransform.anchorMax = new Vector2(0, 1);
            palette.rectTransform.pivot = new Vector2(0, 1);
            palette.rectTransform.sizeDelta = new Vector2(210, -48);
            palette.rectTransform.anchoredPosition = new Vector2(8, -42);

            // ScrollRect
            var viewport = UIFactory.Rect("Viewport", palette.transform);
            UIFactory.Stretch(viewport, 4, 4);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.15f);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = UIFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = palette.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            paletteContent = content;
            PopulatePalette();
        }

        /// <summary>Fills the palette with the blocks available for the current difficulty.</summary>
        void PopulatePalette()
        {
            if (paletteContent == null) return;
            if (difficulty == Difficulty.Easy) PopulateEasyPalette();
            else PopulateHardPalette();
        }

        void RebuildPalette()
        {
            if (paletteContent == null) return;
            for (int i = paletteContent.childCount - 1; i >= 0; i--)
                Destroy(paletteContent.GetChild(i).gameObject);
            PopulatePalette();
        }

        void PopulateEasyPalette()
        {
            AddPaletteHeader(paletteContent, "SENSORES");
            AddPaletteButton(paletteContent, NodeKind.IsHolding);
            AddPaletteButton(paletteContent, NodeKind.BelowColor);
            AddPaletteButton(paletteContent, NodeKind.NeedBelow);
            AddPaletteButton(paletteContent, NodeKind.RedScore);
            AddPaletteButton(paletteContent, NodeKind.BlueScore);
            AddPaletteButton(paletteContent, NodeKind.Goal);
            AddPaletteHeader(paletteContent, "LOGICA");
            AddPaletteButton(paletteContent, NodeKind.Not);
            AddPaletteButton(paletteContent, NodeKind.And);
            AddPaletteButton(paletteContent, NodeKind.Or);
            AddPaletteButton(paletteContent, NodeKind.Constant);
            AddPaletteHeader(paletteContent, "ACOES INTELIGENTES");
            AddPaletteButton(paletteContent, NodeKind.Search);
            AddPaletteButton(paletteContent, NodeKind.Deliver);
            AddPaletteButton(paletteContent, NodeKind.Grab);
            AddPaletteButton(paletteContent, NodeKind.Release);
        }

        void PopulateHardPalette()
        {
            AddPaletteHeader(paletteContent, "SENSORES");
            AddPaletteButton(paletteContent, NodeKind.PosX);
            AddPaletteButton(paletteContent, NodeKind.PosY);
            AddPaletteButton(paletteContent, NodeKind.PosZ);
            AddPaletteButton(paletteContent, NodeKind.BelowColor);
            AddPaletteButton(paletteContent, NodeKind.BelowDistance);
            AddPaletteButton(paletteContent, NodeKind.IsHolding);
            AddPaletteButton(paletteContent, NodeKind.RedScore);
            AddPaletteButton(paletteContent, NodeKind.BlueScore);
            AddPaletteButton(paletteContent, NodeKind.Goal);
            AddPaletteHeader(paletteContent, "VALORES / VARIAVEIS");
            AddPaletteButton(paletteContent, NodeKind.Constant);
            AddPaletteButton(paletteContent, NodeKind.VarGet);
            AddPaletteButton(paletteContent, NodeKind.VarSet);
            AddPaletteHeader(paletteContent, "MATEMATICA");
            AddPaletteButton(paletteContent, NodeKind.Add);
            AddPaletteButton(paletteContent, NodeKind.Sub);
            AddPaletteButton(paletteContent, NodeKind.Mul);
            AddPaletteHeader(paletteContent, "COMPARACOES");
            AddPaletteButton(paletteContent, NodeKind.Greater);
            AddPaletteButton(paletteContent, NodeKind.Less);
            AddPaletteButton(paletteContent, NodeKind.Equals);
            AddPaletteHeader(paletteContent, "PORTAS LOGICAS");
            AddPaletteButton(paletteContent, NodeKind.And);
            AddPaletteButton(paletteContent, NodeKind.Or);
            AddPaletteButton(paletteContent, NodeKind.Not);
            AddPaletteHeader(paletteContent, "ACOES (XYZ + GARRA)");
            AddPaletteButton(paletteContent, NodeKind.MoveX);
            AddPaletteButton(paletteContent, NodeKind.MoveY);
            AddPaletteButton(paletteContent, NodeKind.MoveZ);
            AddPaletteButton(paletteContent, NodeKind.Grab);
            AddPaletteButton(paletteContent, NodeKind.Release);
        }

        void AddPaletteHeader(Transform parent, string text)
        {
            var t = UIFactory.Label("Header", parent, text, 11, TextAnchor.MiddleLeft);
            t.color = new Color(0.6f, 0.6f, 0.65f, 1f);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 18;
        }

        void AddPaletteButton(Transform parent, NodeKind kind)
        {
            var spec = NodeSpec.Get(kind);
            var btn = UIFactory.Btn("Btn_" + kind, parent, spec.label, 12, spec.color);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 24;
            NodeKind captured = kind;
            btn.onClick.AddListener(() => CreateNode(captured, NextSpawnPos()));
        }

        Vector2 NextSpawnPos()
        {
            spawnCounter++;
            float x = 320 + (spawnCounter % 5) * 60;
            float y = 220 + (spawnCounter % 7) * 50;
            return new Vector2(x, y);
        }

        void BuildCanvasArea()
        {
            var area = UIFactory.Paneled("NodeArea", panel.transform, new Color(0.08f, 0.09f, 0.10f, 1f));
            area.rectTransform.anchorMin = new Vector2(0, 0);
            area.rectTransform.anchorMax = new Vector2(1, 1);
            area.rectTransform.offsetMin = new Vector2(226, 48);
            area.rectTransform.offsetMax = new Vector2(-236, -42);
            nodeArea = area.rectTransform;

            area.gameObject.AddComponent<RectMask2D>();

            // Pannable / zoomable content root. Nodes and links are children of it, so moving
            // or scaling this single RectTransform pans/zooms the whole graph at once. It is
            // anchored to the viewport's bottom-left so node coordinates are easy to reason about.
            nodeCanvas = UIFactory.Rect("NodeCanvas", area.transform);
            nodeCanvas.anchorMin = nodeCanvas.anchorMax = new Vector2(0, 0);
            nodeCanvas.pivot = new Vector2(0, 0);
            nodeCanvas.sizeDelta = new Vector2(8000, 8000);
            nodeCanvas.anchoredPosition = Vector2.zero;
            nodeCanvas.localScale = Vector3.one;

            var view = area.gameObject.AddComponent<GraphViewController>();
            view.content = nodeCanvas;
            view.viewport = nodeArea;

            lineLayer = UIFactory.Rect("LineLayer", nodeCanvas);
            UIFactory.Stretch(lineLayer);
            lineLayer.SetAsFirstSibling();
        }

        /// <summary>Zooms/pans the view so every node fits inside the visible area.</summary>
        void FrameAll()
        {
            if (nodeCanvas == null || nodeArea == null || nodes.Count == 0) return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var n in nodes)
            {
                if (n.root == null) continue;
                Vector2 p = n.root.anchoredPosition;   // top-left (pivot 0,1) measured from content bottom-left
                Vector2 sz = n.root.sizeDelta;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x + sz.x);
                minY = Mathf.Min(minY, p.y - sz.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            float gw = Mathf.Max(1f, maxX - minX);
            float gh = Mathf.Max(1f, maxY - minY);
            Vector2 areaSize = nodeArea.rect.size;
            const float margin = 60f;

            float s = Mathf.Min((areaSize.x - margin) / gw, (areaSize.y - margin) / gh);
            s = Mathf.Clamp(s, 0.2f, 1.25f);

            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            nodeCanvas.localScale = new Vector3(s, s, 1f);
            nodeCanvas.anchoredPosition = areaSize * 0.5f - s * center;
        }

        void BuildReadout()
        {
            var p = UIFactory.Paneled("Sensors", panel.transform, UIFactory.Panel);
            p.rectTransform.anchorMin = new Vector2(1, 0);
            p.rectTransform.anchorMax = new Vector2(1, 1);
            p.rectTransform.pivot = new Vector2(1, 1);
            p.rectTransform.sizeDelta = new Vector2(228, -48);
            p.rectTransform.anchoredPosition = new Vector2(-8, -42);

            var head = UIFactory.Label("Head", p.transform, "SENSORES DO IMA", 13, TextAnchor.UpperLeft);
            head.color = UIFactory.Accent;
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.pivot = new Vector2(0, 1);
            head.rectTransform.anchoredPosition = new Vector2(10, -8);
            head.rectTransform.sizeDelta = new Vector2(-20, 22);

            readout = UIFactory.Label("Readout", p.transform, "", 13, TextAnchor.UpperLeft);
            UIFactory.Stretch(readout.rectTransform, 10, 34);
            readout.rectTransform.offsetMax = new Vector2(-10, -34);
        }

        void BuildBottomBar()
        {
            var bar = UIFactory.Paneled("BottomBar", panel.transform, UIFactory.Panel);
            bar.rectTransform.anchorMin = new Vector2(0, 0);
            bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0.5f, 0);
            bar.rectTransform.sizeDelta = new Vector2(0, 40);

            var runBtn = UIFactory.Btn("Run", bar.transform, "RODAR / PAUSAR", 13, UIFactory.PanelLight);
            Place(runBtn.GetComponent<RectTransform>(), 12, 150);
            runBtn.onClick.AddListener(() =>
            {
                running = !running;
                statusText.text = running ? "Estado: RODANDO" : "Estado: PAUSADO";
            });

            var clearBtn = UIFactory.Btn("Clear", bar.transform, "LIMPAR", 13, new Color(0.4f, 0.18f, 0.18f, 1f));
            Place(clearBtn.GetComponent<RectTransform>(), 170, 110);
            clearBtn.onClick.AddListener(ClearAll);

            var exBtn = UIFactory.Btn("Example", bar.transform, "EXEMPLO", 13, new Color(0.18f, 0.34f, 0.22f, 1f));
            Place(exBtn.GetComponent<RectTransform>(), 288, 110);
            exBtn.onClick.AddListener(LoadExample);

            var diffBtn = UIFactory.Btn("Difficulty", bar.transform, "MODO", 13, new Color(0.30f, 0.26f, 0.12f, 1f));
            Place(diffBtn.GetComponent<RectTransform>(), 406, 170);
            difficultyLabel = diffBtn.GetComponentInChildren<Text>();
            UpdateDifficultyLabel();
            diffBtn.onClick.AddListener(ToggleDifficulty);

            var frameBtn = UIFactory.Btn("Frame", bar.transform, "VER TUDO", 13, new Color(0.20f, 0.24f, 0.32f, 1f));
            Place(frameBtn.GetComponent<RectTransform>(), 584, 110);
            frameBtn.onClick.AddListener(FrameAll);

            statusText = UIFactory.Label("Status", bar.transform, "Estado: RODANDO", 13, TextAnchor.MiddleLeft);
            statusText.rectTransform.anchorMin = new Vector2(0, 0);
            statusText.rectTransform.anchorMax = new Vector2(1, 1);
            statusText.rectTransform.pivot = new Vector2(0, 0.5f);
            statusText.rectTransform.offsetMin = new Vector2(706, 0);
            statusText.rectTransform.offsetMax = new Vector2(-10, 0);
        }

        void UpdateDifficultyLabel()
        {
            if (difficultyLabel != null)
                difficultyLabel.text = "MODO: " + (difficulty == Difficulty.Easy ? "FACIL" : "DIFICIL");
        }

        void ToggleDifficulty()
        {
            difficulty = difficulty == Difficulty.Easy ? Difficulty.Hard : Difficulty.Easy;
            UpdateDifficultyLabel();
            RebuildPalette();
            LoadExample();   // clears the graph and loads the example matching the new mode
        }

        void Place(RectTransform rt, float x, float width)
        {
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(width, 28);
            rt.anchoredPosition = new Vector2(x, 0);
        }

        // ---------------------------------------------------------------- node creation

        ProgramNode CreateNode(NodeKind kind, Vector2 pos)
        {
            var spec = NodeSpec.Get(kind);
            var node = new ProgramNode
            {
                kind = kind,
                spec = spec,
                inputSources = new ProgramNode[spec.inputCount],
                inputPorts = new RectTransform[spec.inputCount]
            };

            const float W = 190f;
            float headerH = 24f;
            float rowH = 22f;
            float fieldH = (spec.hasValueField || spec.hasVarField) ? 26f : 0f;
            float bodyH = headerH + spec.inputCount * rowH + fieldH + 8f;

            var rootImg = UIFactory.Paneled("Node_" + kind, nodeCanvas, new Color(spec.color.r, spec.color.g, spec.color.b, 0.97f));
            var root = rootImg.rectTransform;
            root.anchorMin = root.anchorMax = new Vector2(0, 0);
            root.pivot = new Vector2(0, 1);
            root.sizeDelta = new Vector2(W, bodyH);
            root.anchoredPosition = pos;
            node.root = root;

            // The whole block is draggable (the header still works too). This also stops a
            // drag that starts on a node from being treated as a background pan.
            var bodyDrag = root.gameObject.AddComponent<UIDragHandle>();
            bodyDrag.target = root;

            var outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.5f);
            outline.effectDistance = new Vector2(2, -2);

            // Header (draggable) + delete
            var header = UIFactory.Paneled("Header", root, new Color(0, 0, 0, 0.35f));
            header.rectTransform.anchorMin = new Vector2(0, 1);
            header.rectTransform.anchorMax = new Vector2(1, 1);
            header.rectTransform.pivot = new Vector2(0.5f, 1);
            header.rectTransform.sizeDelta = new Vector2(0, headerH);
            var drag = header.gameObject.AddComponent<UIDragHandle>();
            drag.target = root;

            var hLabel = UIFactory.Label("HeaderText", header.transform, spec.label, 12, TextAnchor.MiddleLeft);
            hLabel.rectTransform.anchorMin = new Vector2(0, 0);
            hLabel.rectTransform.anchorMax = new Vector2(1, 1);
            hLabel.rectTransform.offsetMin = new Vector2(6, 0);
            hLabel.rectTransform.offsetMax = new Vector2(-24, 0);

            var del = UIFactory.Btn("Del", header.transform, "X", 12, new Color(0.5f, 0.15f, 0.15f, 1f));
            var delRT = del.GetComponent<RectTransform>();
            delRT.anchorMin = new Vector2(1, 0.5f);
            delRT.anchorMax = new Vector2(1, 0.5f);
            delRT.pivot = new Vector2(1, 0.5f);
            delRT.sizeDelta = new Vector2(20, 18);
            delRT.anchoredPosition = new Vector2(-3, 0);
            del.onClick.AddListener(() => RemoveNode(node));

            float y = -headerH - 2f;

            // Input ports
            for (int i = 0; i < spec.inputCount; i++)
            {
                int idx = i;
                var portRT = MakePort(root, node, idx, false);
                portRT.anchorMin = portRT.anchorMax = new Vector2(0, 1);
                portRT.pivot = new Vector2(0.5f, 0.5f);
                portRT.anchoredPosition = new Vector2(8, y - rowH * 0.5f);
                node.inputPorts[i] = portRT;

                string lbl = spec.inputLabels != null && i < spec.inputLabels.Length ? spec.inputLabels[i] : "in";
                var inLbl = UIFactory.Label("InLbl" + i, root, lbl, 11, TextAnchor.MiddleLeft);
                inLbl.rectTransform.anchorMin = new Vector2(0, 1);
                inLbl.rectTransform.anchorMax = new Vector2(1, 1);
                inLbl.rectTransform.pivot = new Vector2(0, 1);
                inLbl.rectTransform.anchoredPosition = new Vector2(20, y);
                inLbl.rectTransform.sizeDelta = new Vector2(-24, rowH);
                y -= rowH;
            }

            // Output port
            if (spec.hasOutput)
            {
                node.outputPort = MakePort(root, node, -1, true);
                node.outputPort.anchorMin = node.outputPort.anchorMax = new Vector2(1, 1);
                node.outputPort.pivot = new Vector2(0.5f, 0.5f);
                node.outputPort.anchoredPosition = new Vector2(-8, -headerH - 12f);
            }

            // Value / variable fields
            if (spec.hasValueField)
            {
                var f = UIFactory.Input("Value", root, "1");
                PlaceField(f.GetComponent<RectTransform>(), y);
                node.valueField = f;
            }
            else if (spec.hasVarField)
            {
                var f = UIFactory.Input("Var", root, "var");
                PlaceField(f.GetComponent<RectTransform>(), y);
                node.varField = f;
            }

            nodes.Add(node);
            return node;
        }

        void PlaceField(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(8, 0);
            rt.offsetMax = new Vector2(-8, 0);
            rt.anchoredPosition = new Vector2(0, y - 2f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 22);
        }

        RectTransform MakePort(RectTransform parent, ProgramNode node, int inputIndex, bool isOutput)
        {
            var img = UIFactory.Paneled(isOutput ? "OutPort" : "InPort", parent,
                isOutput ? UIFactory.Accent : new Color(0.85f, 0.7f, 0.3f, 1f));
            img.rectTransform.sizeDelta = new Vector2(14, 14);
            var port = img.gameObject.AddComponent<UIPort>();
            port.owner = this;
            port.node = node;
            port.inputIndex = inputIndex;
            port.isOutput = isOutput;
            return img.rectTransform;
        }

        // ---------------------------------------------------------------- connections

        public void OnPortClicked(ProgramNode node, int inputIndex, bool isOutput)
        {
            if (isOutput)
            {
                pendingOutput = node;
                statusText.text = "Ligando de: " + node.spec.label + "  → clique uma ENTRADA";
                return;
            }

            if (pendingOutput != null && pendingOutput != node)
            {
                RemoveLink(node, inputIndex);
                node.inputSources[inputIndex] = pendingOutput;
                AddLink(pendingOutput, node, inputIndex);
                statusText.text = running ? "Estado: RODANDO" : "Estado: PAUSADO";
                pendingOutput = null;
            }
            else
            {
                // clicking an input with no pending connection clears it
                RemoveLink(node, inputIndex);
                node.inputSources[inputIndex] = null;
            }
        }

        void AddLink(ProgramNode from, ProgramNode to, int inputIndex)
        {
            var img = UIFactory.Paneled("Link", lineLayer, UIFactory.Accent);
            img.raycastTarget = false;
            img.rectTransform.pivot = new Vector2(0, 0.5f);
            // Anchor to the layer centre so anchoredPosition matches ScreenPointToLocalPointInRectangle output.
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            links.Add(new LineLink { from = from, to = to, inputIndex = inputIndex, line = img.rectTransform });
        }

        void RemoveLink(ProgramNode to, int inputIndex)
        {
            for (int i = links.Count - 1; i >= 0; i--)
            {
                if (links[i].to == to && links[i].inputIndex == inputIndex)
                {
                    if (links[i].line != null) Destroy(links[i].line.gameObject);
                    links.RemoveAt(i);
                }
            }
        }

        void RemoveNode(ProgramNode node)
        {
            for (int i = links.Count - 1; i >= 0; i--)
            {
                if (links[i].from == node || links[i].to == node)
                {
                    if (links[i].to != null && links[i].inputIndex >= 0 &&
                        links[i].inputIndex < links[i].to.inputSources.Length)
                        links[i].to.inputSources[links[i].inputIndex] = null;
                    if (links[i].line != null) Destroy(links[i].line.gameObject);
                    links.RemoveAt(i);
                }
            }
            if (node.root != null) Destroy(node.root.gameObject);
            nodes.Remove(node);
        }

        void ClearAll()
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
                RemoveNode(nodes[i]);
            variables.Clear();
            pendingOutput = null;
        }

        void RedrawLines()
        {
            foreach (var l in links)
            {
                if (l.from == null || l.from.outputPort == null || l.to == null ||
                    l.inputIndex >= l.to.inputPorts.Length || l.to.inputPorts[l.inputIndex] == null)
                    continue;

                Vector2 a, b;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    lineLayer, l.from.outputPort.position, null, out a);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    lineLayer, l.to.inputPorts[l.inputIndex].position, null, out b);

                Vector2 dir = b - a;
                float len = dir.magnitude;
                l.line.anchoredPosition = a;
                l.line.sizeDelta = new Vector2(len, 3f);
                l.line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        // ---------------------------------------------------------------- readout

        void UpdateReadout()
        {
            if (readout == null || magnet == null) return;
            Vector3 p = magnet.SensorPosition;
            string color = magnet.SensorBelowColor == 1 ? "VERMELHO"
                         : magnet.SensorBelowColor == 2 ? "AZUL" : "nada";

            int goal = GoalPerColor;
            string need = NeedColor(magnet.SensorBelowColor) ? "SIM" : "não";

            readout.text =
                $"Posição X: {p.x:0.0}\n" +
                $"Posição Y: {p.y:0.0}\n" +
                $"Posição Z: {p.z:0.0}\n\n" +
                $"Abaixo (cor): {color}\n" +
                $"Abaixo (dist): {magnet.SensorBelowDistance:0.0}\n" +
                $"Precisa pegar: {need}\n\n" +
                $"Segurando: {(magnet.IsHolding ? "SIM" : "não")}\n\n" +
                $"Vermelhos: {RedScore}/{goal}\nAzuis: {BlueScore}/{goal}";
        }

        // ---------------------------------------------------------------- example program

        /// <summary>Loads the example program that matches the current difficulty.</summary>
        void LoadExample()
        {
            if (difficulty == Difficulty.Easy) LoadExampleEasy();
            else LoadExampleHard();
        }

        /// <summary>
        /// Easy example: only three smart blocks plus the holding logic. "Procurar" sweeps and
        /// dives while not holding, "Garra" only closes when the object below is a colour we
        /// still need (NeedBelow), and "Entregar no forno" carries the catch to the incinerator
        /// and drops it. The whole challenge is solved with six nodes.
        /// </summary>
        void LoadExampleEasy()
        {
            ClearAll();
            exampleColY = new Dictionary<int, float>();

            var holding = New(NodeKind.IsHolding, 0);
            var notHolding = New(NodeKind.Not, 0); Connect(holding, notHolding, 0);
            var need = New(NodeKind.NeedBelow, 0);

            var search = New(NodeKind.Search, 1); Connect(notHolding, search, 0);   // search while empty-handed
            var grab = New(NodeKind.Grab, 1); Connect(need, grab, 0);               // grab only needed colours
            var deliver = New(NodeKind.Deliver, 1); Connect(holding, deliver, 0);   // deliver while carrying

            FrameAll();

            statusText.text = "Exemplo FACIL: Procurar + Garra (so a cor que falta) + Entregar. Clique RODAR.";
        }

        /// <summary>
        /// Hard example: a complete program built only from granular blocks. The magnet sweeps
        /// the pit (bouncing back and forth in X while slowly crawling in Z) and the grab is
        /// armed only when the cube/sphere directly below is a colour that is still missing
        /// (so it never wastes a trip on a colour that already reached the goal). As soon as it
        /// is holding something it stops searching, lifts above the furnace rim, slides to the
        /// origin (0,0) and drops the object into the incinerator. Then it searches again,
        /// repeating until 3 red + 3 blue have been burned and the challenge completes.
        /// </summary>
        void LoadExampleHard()
        {
            ClearAll();
            exampleColY = new Dictionary<int, float>();

            // --- sensors & shared logic (column 0) ---
            var posX = New(NodeKind.PosX, 0);
            var posZ = New(NodeKind.PosZ, 0);
            var posY = New(NodeKind.PosY, 0);
            var holding = New(NodeKind.IsHolding, 0);
            var notHolding = New(NodeKind.Not, 0); Connect(holding, notHolding, 0);
            var tGet = NewVarGet("t", 0);

            // --- constants (column 1) ---
            var c1 = NewConst(1f, 1);
            var c0 = NewConst(0f, 1);
            var cXmax = NewConst(5f, 1);
            var cXmin = NewConst(-5f, 1);
            var cZmax = NewConst(28f, 1);
            var cZmin = NewConst(-28f, 1);
            var cSpeedX = NewConst(7f, 1);
            var cSpeedZ = NewConst(0.8f, 1);   // slow Z crawl => the down sensor passes over every object
            var cYtarget = NewConst(4f, 1);
            var cLift = NewConst(5f, 1);
            var cPull = NewConst(-1.5f, 1);
            var cDive = NewConst(-4f, 1);
            var cHigh = NewConst(3.5f, 1);

            // --- one-shot "boot" pulse: boot = (t == 0) on the first step, then t := 1 forever ---
            // It seeds the bounce directions so the sweep never stalls at the centre.
            var boot = New(NodeKind.Equals, 2); Connect(tGet, boot, 0); Connect(c0, boot, 1);
            var setT = NewVarSet("t", 2); Connect(c1, setT, 0);

            // --- search sweep: bounce in X (fast) and Z (slow) between the walls ---
            var scanVelX = BuildBounce("dx", posX, cXmax, cXmin, cSpeedX, c1, boot, notHolding, 3);
            var scanVelZ = BuildBounce("dz", posZ, cZmax, cZmin, cSpeedZ, c1, boot, notHolding, 4);

            // --- delivery: once lifted clear of the rim, steer to the origin (the furnace) ---
            var high = New(NodeKind.Greater, 5); Connect(posY, high, 0); Connect(cHigh, high, 1);
            var gateXZ = New(NodeKind.Mul, 5); Connect(holding, gateXZ, 0); Connect(high, gateXZ, 1);

            var delVelX = New(NodeKind.Mul, 5); Connect(posX, delVelX, 0); Connect(cPull, delVelX, 1);
            var delVelXg = New(NodeKind.Mul, 5); Connect(delVelX, delVelXg, 0); Connect(gateXZ, delVelXg, 1);
            var delVelZ = New(NodeKind.Mul, 5); Connect(posZ, delVelZ, 0); Connect(cPull, delVelZ, 1);
            var delVelZg = New(NodeKind.Mul, 5); Connect(delVelZ, delVelZg, 0); Connect(gateXZ, delVelZg, 1);

            // --- vertical: dive while searching, rise to furnace height while carrying ---
            var scanY = New(NodeKind.Mul, 5); Connect(notHolding, scanY, 0); Connect(cDive, scanY, 1);
            var errY = New(NodeKind.Sub, 5); Connect(cYtarget, errY, 0); Connect(posY, errY, 1);
            var liftY = New(NodeKind.Mul, 5); Connect(errY, liftY, 0); Connect(cLift, liftY, 1);
            var liftYg = New(NodeKind.Mul, 5); Connect(liftY, liftYg, 0); Connect(holding, liftYg, 1);

            // --- combine search + delivery per axis (column 6) ---
            var sumX = New(NodeKind.Add, 6); Connect(scanVelX, sumX, 0); Connect(delVelXg, sumX, 1);
            var sumZ = New(NodeKind.Add, 6); Connect(scanVelZ, sumZ, 0); Connect(delVelZg, sumZ, 1);
            var sumY = New(NodeKind.Add, 6); Connect(scanY, sumY, 0); Connect(liftYg, sumY, 1);

            // --- colour-aware grab: only close the claw on a colour we still need ---
            // belowColor is 1 for red and 2 for blue; the score sensors tell us how many of
            // each are already burned, so once a colour reaches the goal we stop grabbing it.
            var belowColor = New(NodeKind.BelowColor, 0);
            var redScore = New(NodeKind.RedScore, 0);
            var blueScore = New(NodeKind.BlueScore, 0);
            var goal = New(NodeKind.Goal, 1);
            var cRed = NewConst(1f, 1);    // 1 = red directly below
            var cBlue = NewConst(2f, 1);   // 2 = blue directly below

            var isRed = New(NodeKind.Equals, 2); Connect(belowColor, isRed, 0); Connect(cRed, isRed, 1);
            var isBlue = New(NodeKind.Equals, 2); Connect(belowColor, isBlue, 0); Connect(cBlue, isBlue, 1);
            var redNeed = New(NodeKind.Less, 2); Connect(redScore, redNeed, 0); Connect(goal, redNeed, 1);     // redScore < goal
            var blueNeed = New(NodeKind.Less, 2); Connect(blueScore, blueNeed, 0); Connect(goal, blueNeed, 1); // blueScore < goal

            var needRed = New(NodeKind.And, 3); Connect(isRed, needRed, 0); Connect(redNeed, needRed, 1);
            var needBlue = New(NodeKind.And, 3); Connect(isBlue, needBlue, 0); Connect(blueNeed, needBlue, 1);
            var grabCond = New(NodeKind.Or, 4); Connect(needRed, grabCond, 0); Connect(needBlue, grabCond, 1);

            // --- actuators (column 7) ---
            var moveX = New(NodeKind.MoveX, 7); Connect(sumX, moveX, 0);
            var moveZ = New(NodeKind.MoveZ, 7); Connect(sumZ, moveZ, 0);
            var moveY = New(NodeKind.MoveY, 7); Connect(sumY, moveY, 0);
            var grab = New(NodeKind.Grab, 7); Connect(grabCond, grab, 0);   // only close on a colour we still need

            FrameAll();

            statusText.text = "Exemplo DIFICIL: varre, agarra so a cor que falta e incinera. Clique RODAR (RODA = zoom).";
        }

        /// <summary>
        /// Builds a "bouncing" 1-D sweep on one axis stored in <paramref name="varName"/>.
        /// The direction is +1/-1 and flips when the position passes the min/max bound, which
        /// keeps the magnet patrolling between the walls. Returns the (notHolding-gated) velocity.
        /// </summary>
        ProgramNode BuildBounce(string varName, ProgramNode pos, ProgramNode cMax, ProgramNode cMin,
            ProgramNode cSpeed, ProgramNode cOne, ProgramNode boot, ProgramNode notHolding, int col)
        {
            var g = New(NodeKind.Greater, col); Connect(pos, g, 0); Connect(cMax, g, 1);   // pos > max ?
            var l = New(NodeKind.Less, col); Connect(pos, l, 0); Connect(cMin, l, 1);       // pos < min ?
            var dir = NewVarGet(varName, col);                                              // current direction
            var oneMinusG = New(NodeKind.Sub, col); Connect(cOne, oneMinusG, 0); Connect(g, oneMinusG, 1);   // 1 - g
            var keep = New(NodeKind.Sub, col); Connect(oneMinusG, keep, 0); Connect(l, keep, 1);             // 1 - g - l
            var keepDir = New(NodeKind.Mul, col); Connect(keep, keepDir, 0); Connect(dir, keepDir, 1);       // keep * dir
            var flip = New(NodeKind.Sub, col); Connect(l, flip, 0); Connect(g, flip, 1);                     // (l - g) -> +1 at min, -1 at max
            var baseDir = New(NodeKind.Add, col); Connect(flip, baseDir, 0); Connect(keepDir, baseDir, 1);
            var dirNext = New(NodeKind.Add, col); Connect(baseDir, dirNext, 0); Connect(boot, dirNext, 1);   // seed +1 on the first step
            var setDir = NewVarSet(varName, col); Connect(dirNext, setDir, 0);

            var vel = New(NodeKind.Mul, col); Connect(dir, vel, 0); Connect(cSpeed, vel, 1);                 // dir * speed
            var velGated = New(NodeKind.Mul, col); Connect(vel, velGated, 0); Connect(notHolding, velGated, 1);
            return velGated;
        }

        // ---- example-builder convenience helpers (lay nodes out column by column) ----

        Vector2 NextSlot(int col)
        {
            if (exampleColY == null) exampleColY = new Dictionary<int, float>();
            if (!exampleColY.TryGetValue(col, out float y)) y = 1500f;
            exampleColY[col] = y - 118f;
            return new Vector2(40f + col * 235f, y);
        }

        ProgramNode New(NodeKind kind, int col) => CreateNode(kind, NextSlot(col));

        ProgramNode NewConst(float value, int col)
        {
            var n = CreateNode(NodeKind.Constant, NextSlot(col));
            if (n.valueField != null)
                n.valueField.text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return n;
        }

        ProgramNode NewVarGet(string name, int col)
        {
            var n = CreateNode(NodeKind.VarGet, NextSlot(col));
            if (n.varField != null) n.varField.text = name;
            return n;
        }

        ProgramNode NewVarSet(string name, int col)
        {
            var n = CreateNode(NodeKind.VarSet, NextSlot(col));
            if (n.varField != null) n.varField.text = name;
            return n;
        }

        void Connect(ProgramNode from, ProgramNode to, int inputIndex)
        {
            to.inputSources[inputIndex] = from;
            AddLink(from, to, inputIndex);
        }
    }
}
