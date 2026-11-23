using UnityEngine;

public static class GameArt
{
    private static Sprite circleSprite;
    private static Sprite squareSprite;

    public static Sprite Circle
    {
        get
        {
            if (circleSprite == null)
            {
                const int size = 96;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "Procedural soft circle";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float radius = size * 0.46f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float alpha = Mathf.Clamp01((radius - distance) * 2f + 0.5f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                texture.Apply();
                circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                circleSprite.name = "Procedural Circle";
            }
            return circleSprite;
        }
    }

    public static Sprite Square
    {
        get
        {
            if (squareSprite == null)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "Procedural white square";
                texture.filterMode = FilterMode.Point;
                texture.SetPixel(0, 0, Color.white);
                texture.SetPixel(0, 1, Color.white);
                texture.SetPixel(1, 0, Color.white);
                texture.SetPixel(1, 1, Color.white);
                texture.Apply();
                squareSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                squareSprite.name = "Procedural Square";
            }
            return squareSprite;
        }
    }

    public static GameObject CircleObject(string name, Transform parent, Vector2 localPosition, Vector2 scale, Color color, int sortingOrder)
    {
        return SpriteObject(name, parent, Circle, localPosition, scale, color, sortingOrder);
    }

    public static GameObject RectObject(string name, Transform parent, Vector2 localPosition, Vector2 scale, Color color, int sortingOrder)
    {
        return SpriteObject(name, parent, Square, localPosition, scale, color, sortingOrder);
    }

    public static GameObject SpriteObject(string name, Transform parent, Sprite sprite, Vector2 localPosition, Vector2 scale, Color color, int sortingOrder)
    {
        GameObject obj = new GameObject(name);
        if (parent != null)
            obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return obj;
    }
}
