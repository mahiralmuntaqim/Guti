using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BrineTime
{
    /// <summary>Tiny helpers for building uGUI in code (no prefabs or scenes needed).</summary>
    public static class Ui
    {
        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        /// <summary>Anchor a rect: anchors (minX, minY, maxX, maxY) in 0..1 with pixel offsets from them.</summary>
        public static RectTransform Place(RectTransform rt, Vector4 anchors, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = new Vector2(anchors.x, anchors.y);
            rt.anchorMax = new Vector2(anchors.z, anchors.w);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Rect(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public static Image Panel(Transform parent, Color color)
        {
            var rt = Rect(parent, "Panel");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Button Button(Transform parent, string text, Action onClick, int fontSize = 20, float height = 40)
        {
            var rt = Rect(parent, "Button");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.16f, 0.22f, 0.3f, 1f);
            var btn = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            var label = Label(rt, text, fontSize, Color.white, TextAnchor.MiddleLeft);
            Place(label.rectTransform, new Vector4(0, 0, 1, 1), new Vector2(12, 0), new Vector2(-12, 0));
            return btn;
        }

        public static InputField Input(Transform parent, string placeholder, int fontSize = 20)
        {
            var rt = Rect(parent, "Input");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.1f, 0.13f, 0.18f, 1f);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 40;
            le.preferredHeight = 40;
            var text = Label(rt, "", fontSize, Color.white, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Destroy(text.GetComponent<Outline>());
            Place(text.rectTransform, new Vector4(0, 0, 1, 1), new Vector2(10, 0), new Vector2(-10, 0));
            var ph = Label(rt, placeholder, fontSize, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft);
            Destroy(ph.GetComponent<Outline>());
            Place(ph.rectTransform, new Vector4(0, 0, 1, 1), new Vector2(10, 0), new Vector2(-10, 0));
            var field = rt.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            return field;
        }

        public static VerticalLayoutGroup VList(RectTransform rt, int spacing, int padding)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        static void Destroy(UnityEngine.Object o)
        {
            if (o != null) UnityEngine.Object.Destroy(o);
        }
    }
}
