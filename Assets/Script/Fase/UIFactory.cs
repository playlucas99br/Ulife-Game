using UnityEngine;
using UnityEngine.UI;

namespace FaseLucasGame
{
    /// <summary>Small helpers to build legacy uGUI elements from code (no prefabs required).</summary>
    public static class UIFactory
    {
        public static Font DefaultFont
        {
            get
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return f;
            }
        }

        public static readonly Color Panel = new Color(0.10f, 0.11f, 0.13f, 0.96f);
        public static readonly Color PanelLight = new Color(0.17f, 0.19f, 0.22f, 1f);
        public static readonly Color Accent = new Color(0.30f, 0.65f, 0.95f, 1f);
        public static readonly Color TextCol = new Color(0.88f, 0.90f, 0.93f, 1f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image Paneled(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, TextAnchor anchor)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = TextCol;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Btn(string name, Transform parent, string text, int size, Color bg)
        {
            var img = Paneled(name, parent, bg);
            var btn = img.gameObject.AddComponent<Button>();
            var label = Label("Text", img.transform, text, size, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return btn;
        }

        public static InputField Input(string name, Transform parent, string value)
        {
            var img = Paneled(name, parent, new Color(0.06f, 0.07f, 0.08f, 1f));
            var field = img.gameObject.AddComponent<InputField>();

            var textRT = Rect("Text", img.transform);
            Stretch(textRT, 4, 2);
            var txt = textRT.gameObject.AddComponent<Text>();
            txt.font = DefaultFont;
            txt.fontSize = 13;
            txt.color = TextCol;
            txt.supportRichText = false;
            txt.alignment = TextAnchor.MiddleLeft;

            field.textComponent = txt;
            field.text = value;
            return field;
        }

        public static void Stretch(RectTransform rt, float padX = 0, float padY = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
