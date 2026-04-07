using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MainMenuManager : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] float verticalSpacing = 0.9f;
    [SerializeField] Vector3 menuOrigin = new Vector3(-3.8f, 1.8f, 5f);

    [Header("Hover Glow")]
    [SerializeField] float glowIntensity = 0.35f;
    [SerializeField] float glowTransitionSpeed = 8f;
    [SerializeField] Color glowColor = new Color(0.8f, 0.9f, 1f, 1f);

    [Header("Fade")]
    [SerializeField] float fadeDuration = 0.7f;

    Camera menuCamera;
    readonly List<MenuEntry> entries = new();
    MenuEntry? hoveredEntry;

    struct MenuEntry
    {
        public GameObject go;
        public TextMeshPro text;
        public Material mat;
        public BoxCollider col;
        public MenuGlowHandler glow;
        public string id;
    }

    void Awake()
    {
        menuCamera = Camera.main;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CreateMenuItems();
    }

    void CreateMenuItems()
    {
        var items = new (string id, string label, Vector3 offset)[]
        {
            ("start",    "Iniciar Jogo",    new Vector3(-0.15f, 0f, 0f)),
            ("settings", "Configurações",   new Vector3(0.25f,  0f, 0f)),
            ("quit",     "Sair do Jogo",    new Vector3(-0.3f, 0f, 0f)),
        };

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            Vector3 pos = menuOrigin + item.offset;
            pos.y -= i * verticalSpacing;

            var entry = CreateTextObject(item.id, item.label, pos);
            entries.Add(entry);
        }
    }

    MenuEntry CreateTextObject(string id, string label, Vector3 position)
    {
        var go = new GameObject($"Btn_{id}");
        go.transform.SetParent(transform);
        go.transform.position = position;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = label;
        tmp.fontSize = 5;
        tmp.fontStyle = FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        tmp.overflowMode = TextOverflowModes.Overflow;

        tmp.rectTransform.pivot = new Vector2(0f, 0.5f);
        tmp.rectTransform.sizeDelta = new Vector2(4f, 0.8f);

        var sdfShader = Shader.Find("TextMeshPro/Distance Field");
        var mat = new Material(tmp.fontSharedMaterial);
        if (sdfShader != null)
            mat.shader = sdfShader;

        mat.EnableKeyword("GLOW_ON");
        mat.SetColor("_GlowColor", glowColor);
        mat.SetFloat("_GlowOuter", 0f);
        mat.SetFloat("_GlowInner", 0.754f);
        mat.SetFloat("_GlowOffset", -0.05f);
        mat.SetFloat("_GlowPower", 0.237f);

        tmp.fontSharedMaterial = mat;

        tmp.ForceMeshUpdate();

        var col = go.AddComponent<BoxCollider>();
        Bounds textBounds = tmp.textBounds;
        col.center = textBounds.center;
        col.size = new Vector3(textBounds.size.x + 0.2f, textBounds.size.y + 0.1f, 0.2f);

        var handler = go.AddComponent<MenuGlowHandler>();
        handler.Init(mat, glowIntensity, glowTransitionSpeed);

        return new MenuEntry { go = go, text = tmp, mat = mat, col = col, glow = handler, id = id };
    }

    void Update()
    {
        HandleHover();
        HandleClick();
    }

    void HandleHover()
    {
        Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);

        MenuEntry? newHover = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (hit.collider == entries[i].col)
                {
                    newHover = entries[i];
                    break;
                }
            }
        }

        if (hoveredEntry.HasValue && (!newHover.HasValue || newHover.Value.id != hoveredEntry.Value.id))
            hoveredEntry.Value.glow.SetTarget(0f);

        if (newHover.HasValue)
            newHover.Value.glow.SetTarget(glowIntensity);

        hoveredEntry = newHover;
    }

    void HandleClick()
    {
        if (!Input.GetMouseButtonDown(0) || !hoveredEntry.HasValue) return;

        if (hoveredEntry.Value.id == "start")
            StartCoroutine(FadeOutOtherButtons(hoveredEntry.Value.id));
    }

    IEnumerator FadeOutOtherButtons(string keepId)
    {
        float elapsed = 0f;
        var toFade = new List<TextMeshPro>();

        foreach (var e in entries)
        {
            if (e.id != keepId)
                toFade.Add(e.text);
        }

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            float alpha = Mathf.Lerp(1f, 0f, t * t);

            foreach (var tmp in toFade)
                tmp.color = new Color(1f, 1f, 1f, alpha);

            yield return null;
        }

        foreach (var tmp in toFade)
        {
            tmp.color = new Color(1f, 1f, 1f, 0f);
            tmp.GetComponent<BoxCollider>().enabled = false;
        }
    }
}

public class MenuGlowHandler : MonoBehaviour
{
    Material mat;
    float targetGlow;
    float maxGlow;
    float speed;
    float currentGlow;

    public void Init(Material material, float glowMax, float transitionSpeed)
    {
        mat = material;
        maxGlow = glowMax;
        speed = transitionSpeed;
    }

    public void SetTarget(float target) => targetGlow = target;

    void Update()
    {
        if (mat == null) return;

        currentGlow = Mathf.Lerp(currentGlow, targetGlow, Time.deltaTime * speed);
        mat.SetFloat("_GlowOuter", currentGlow);
    }
}
