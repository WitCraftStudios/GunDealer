using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class RuntimeUiFactory
{
    static TMP_FontAsset cachedFontAsset;

    public static TMP_FontAsset ResolveFontAsset()
    {
        if (cachedFontAsset != null) return cachedFontAsset;

        TextMeshProUGUI[] texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
            {
                cachedFontAsset = texts[i].font;
                break;
            }
        }

        if (cachedFontAsset == null)
        {
            cachedFontAsset = TMP_Settings.defaultFontAsset;
        }

        return cachedFontAsset;
    }

    public static RectTransform CreateRectTransform(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    public static Image CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot,
        Color color)
    {
        RectTransform rect = CreateRectTransform(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string content,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot)
    {
        RectTransform rect = CreateRectTransform(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = ResolveFontAsset();
        text.text = content;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string label,
        UnityAction onClick,
        Color backgroundColor,
        Color textColor)
    {
        Image image = CreatePanel(
            name,
            parent,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 52f),
            new Vector2(0.5f, 0.5f),
            backgroundColor);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.15f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(backgroundColor.r * 0.55f, backgroundColor.g * 0.55f, backgroundColor.b * 0.55f, 0.75f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(
            "Label",
            image.transform,
            label,
            24f,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f));
        text.color = textColor;
        text.margin = new Vector4(18f, 8f, 18f, 8f);
        text.textWrappingMode = TextWrappingModes.Normal;

        return button;
    }
}
