using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DriveSim.Menu
{
    // Formes, couleurs et icônes du menu. Tout est dessiné ici, sans image à importer.
    public static class UiKit
    {
        public static readonly Color Night = new Color(0.012f, 0.03f, 0.07f, 1f);
        public static readonly Color Cyan = new Color(0.18f, 0.84f, 0.86f, 1f);
        public static readonly Color Ink = new Color(0.04f, 0.1f, 0.14f, 1f);
        public static readonly Color Green = new Color(0.2f, 0.78f, 0.28f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.86f, 0.18f, 1f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.22f, 1f);
        public static readonly Color Red = new Color(0.92f, 0.16f, 0.18f, 1f);
        public static readonly Color Soft = new Color(0.76f, 0.88f, 0.94f, 1f);
        public static readonly Color Panel = new Color(0.03f, 0.08f, 0.13f, 0.96f);
        public static readonly Color Bar = new Color(0.07f, 0.04f, 0.14f, 1f);

        static Font _font;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Font Font
        {
            get
            {
                if (_font != null)
                    return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            return item.AddComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static UnityEngine.UI.Image Image(string name, Transform parent, Color color, Sprite sprite, bool raycast, UnityEngine.UI.Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var image = item.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            image.type = type;
            return image;
        }

        public static Text Label(Transform parent, string value, int size, Color color, FontStyle style, TextAnchor anchor)
        {
            var item = new GameObject("Text");
            item.transform.SetParent(parent, false);
            item.AddComponent<RectTransform>();
            var text = item.AddComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static Button Button(Transform parent, string name, string value, Color fill, Color ink, int size, Sprite sprite, out Text label)
        {
            var image = Image(name, parent, fill, sprite, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Tint(button);
            label = Label(image.transform, value, size, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 18f, 8f, 18f, 8f);
            return button;
        }

        public static LayoutElement Prefer(Component item, float width, float height)
        {
            var element = item.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
                element.preferredWidth = width;
            if (height > 0f)
            {
                element.preferredHeight = height;
                element.minHeight = height;
            }
            return element;
        }

        public static void Lip(GameObject target, Color color, float distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(0f, -distance);
        }

        public static void Tint(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        public static Sprite Pixel => SpriteOf("pixel", PaintPixel);
        public static Sprite Round(int radius) => SpriteOf("round" + radius, () => PaintRound(radius));
        public static Sprite Skew() => SpriteOf("skew", PaintSkew);
        public static Sprite Circle() => SpriteOf("circle", PaintCircle);
        public static Sprite Star() => SpriteOf("star", PaintStar);
        public static Sprite Backdrop() => SpriteOf("backdrop", PaintBackdrop);
        // 0 Avion, 1 Bateau, 2 Voiture, 3 Vélo, 4 Camion.
        public static Sprite Car(int vehicle) => SpriteOf("vehicleArt" + vehicle, () => LoadVehicleArt(vehicle));
        // Illustrations des modes de jeu (Classic, CTF, Slalom, Fee Cash).
        public static Sprite ModeArt(int mode) => SpriteOf("mode" + mode, () => PaintModeArt(mode));
        public static Sprite LevelCar() => SpriteOf("levelcar", () => LoadVehicleArt(2));
        public static Sprite Icon(string name) => SpriteOf("icon" + name, () => PaintIcon(name));

        static readonly string[] VehicleResourceNames = { "plane", "boat", "car", "bike", "truck" };

        static Sprite SpriteOf(string key, System.Func<Sprite> paint)
        {
            Sprite found;
            if (Cache.TryGetValue(key, out found) && found != null)
                return found;
            found = paint();
            Cache[key] = found;
            return found;
        }

        static Sprite LoadVehicleArt(int vehicle)
        {
            vehicle = Mathf.Clamp(vehicle, 0, VehicleResourceNames.Length - 1);
            string path = "Menu/Vehicles/" + VehicleResourceNames[vehicle];
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;
            var texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            return PaintVehicle(vehicle);
        }

        static Sprite PaintPixel()
        {
            var pix = new Pix(4, 4);
            pix.Fill(Color.white);
            return pix.ToSprite(Vector4.zero);
        }

        static Sprite PaintRound(int radius)
        {
            int r = Mathf.Clamp(radius, 4, 36);
            int size = r * 2 + 16;
            var pix = new Pix(size, size);
            pix.RoundRect(1, 1, size - 2, size - 2, r, Color.white);
            return pix.ToSprite(new Vector4(r, r, r, r));
        }

        static Sprite PaintSkew()
        {
            const int width = 180;
            const int height = 72;
            const int skew = 26;
            var pix = new Pix(width, height);
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                int x0 = Mathf.RoundToInt(Mathf.Lerp(skew, 1f, t));
                int x1 = Mathf.RoundToInt(Mathf.Lerp(width - 2, width - 1 - skew, t));
                for (int x = x0; x <= x1; x++)
                    pix.Plot(x, y, Color.white);
            }
            return pix.ToSprite(Vector4.zero);
        }

        static Sprite PaintCircle()
        {
            var pix = new Pix(64, 64);
            pix.FillCircle(32f, 32f, 30f, Color.white);
            return pix.ToSprite(Vector4.zero);
        }

        static Sprite PaintStar()
        {
            var pix = new Pix(64, 64);
            pix.FillStar(32f, 34f, 28f, 12f, Color.white);
            return pix.ToSprite(Vector4.zero);
        }

        static Sprite PaintBackdrop()
        {
            const int width = 768;
            const int height = 432;
            var pix = new Pix(width, height);
            pix.VGradient(0, height - 1, new Color(0.01f, 0.025f, 0.06f), new Color(0.02f, 0.05f, 0.1f));
            for (int y = 10; y < height; y += 16)
            {
                for (int x = 10; x < width; x += 16)
                    pix.Plot(x, y, new Color(0.4f, 0.75f, 0.9f, 0.07f));
            }

            var road = new Color(0.12f, 0.42f, 0.58f, 0.28f);
            pix.Line(30, 360, 180, 250, 2.2f, road);
            pix.Line(180, 250, 340, 300, 2.2f, road);
            pix.Line(340, 300, 560, 140, 2.2f, road);
            pix.Line(560, 140, 740, 200, 2.2f, road);
            pix.Line(80, 40, 240, 120, 1.6f, road);
            pix.Line(240, 120, 460, 70, 1.6f, road);
            pix.Line(460, 70, 700, 160, 1.6f, road);
            pix.Vignette(0.55f);
            return pix.ToSprite(Vector4.zero);
        }

        static Sprite PaintModeArt(int mode)
        {
            mode = Mathf.Clamp(mode, 0, 3);
            Color skyA, skyB, ground, body, trim;
            switch (mode)
            {
                case 1:
                    skyA = new Color(0.18f, 0.05f, 0.02f);
                    skyB = new Color(0.55f, 0.22f, 0.05f);
                    ground = new Color(0.07f, 0.06f, 0.07f);
                    body = new Color(0.12f, 0.38f, 0.95f);
                    trim = new Color(0.85f, 0.9f, 0.95f);
                    break;
                case 2:
                    skyA = new Color(0.42f, 0.46f, 0.5f);
                    skyB = new Color(0.72f, 0.74f, 0.76f);
                    ground = new Color(0.32f, 0.31f, 0.3f);
                    body = new Color(0.94f, 0.96f, 0.98f);
                    trim = new Color(0.15f, 0.4f, 0.95f);
                    break;
                case 3:
                    skyA = new Color(0.05f, 0.07f, 0.09f);
                    skyB = new Color(0.16f, 0.18f, 0.14f);
                    ground = new Color(0.1f, 0.09f, 0.08f);
                    body = new Color(0.98f, 0.78f, 0.08f);
                    trim = new Color(0.12f, 0.12f, 0.12f);
                    break;
                default:
                    skyA = new Color(0.02f, 0.05f, 0.14f);
                    skyB = new Color(0.05f, 0.16f, 0.32f);
                    ground = new Color(0.035f, 0.04f, 0.06f);
                    body = new Color(0.95f, 0.38f, 0.07f);
                    trim = new Color(0.9f, 0.92f, 0.95f);
                    break;
            }

            var pix = new Pix(300, 390);
            pix.VGradient(0, 236, skyA, skyB);
            pix.FillRect(0, 236, 300, 154, ground);
            pix.FillRect(0, 292, 300, 5, new Color(1f, 1f, 1f, 0.22f));
            if (mode == 1)
            {
                pix.FillCircle(78, 210, 78, new Color(1f, 0.35f, 0.05f, 0.95f));
                pix.FillCircle(70, 200, 46, new Color(1f, 0.82f, 0.28f, 0.95f));
            }
            else if (mode == 2)
            {
                pix.Line(16, 330, 140, 286, 4f, new Color(1f, 1f, 1f, 0.35f));
            }
            else if (mode == 3)
            {
                pix.FillCircle(70, 318, 9, Gold);
                pix.FillCircle(196, 330, 7, Gold);
            }
            DrawSideCar(pix, body, trim);
            pix.Vignette(0.4f);
            return pix.ToSprite(Vector4.zero);
        }

        static Sprite PaintVehicle(int kind)
        {
            kind = Mathf.Clamp(kind, 0, 4);
            Color skyA, skyB, ground;
            switch (kind)
            {
                case 1: // Bateau
                    skyA = new Color(0.1f, 0.28f, 0.42f);
                    skyB = new Color(0.45f, 0.72f, 0.78f);
                    ground = new Color(0.05f, 0.28f, 0.4f);
                    break;
                case 2: // Voiture
                    skyA = new Color(0.18f, 0.05f, 0.02f);
                    skyB = new Color(0.55f, 0.22f, 0.05f);
                    ground = new Color(0.07f, 0.06f, 0.07f);
                    break;
                case 3: // Vélo
                    skyA = new Color(0.04f, 0.12f, 0.08f);
                    skyB = new Color(0.18f, 0.42f, 0.28f);
                    ground = new Color(0.08f, 0.14f, 0.1f);
                    break;
                case 4: // Camion
                    skyA = new Color(0.22f, 0.26f, 0.3f);
                    skyB = new Color(0.55f, 0.58f, 0.6f);
                    ground = new Color(0.28f, 0.27f, 0.26f);
                    break;
                default: // Avion
                    skyA = new Color(0.02f, 0.08f, 0.2f);
                    skyB = new Color(0.25f, 0.55f, 0.85f);
                    ground = new Color(0.12f, 0.28f, 0.22f);
                    break;
            }

            var pix = new Pix(300, 390);
            pix.VGradient(0, 236, skyA, skyB);
            pix.FillRect(0, 236, 300, 154, ground);
            if (kind == 0)
            {
                // Piste d'aéroport.
                pix.FillRect(40, 280, 220, 36, new Color(0.18f, 0.2f, 0.22f, 1f));
                pix.FillRect(48, 294, 28, 6, Color.white);
                pix.FillRect(96, 294, 28, 6, Color.white);
                pix.FillRect(144, 294, 28, 6, Color.white);
                pix.FillRect(192, 294, 28, 6, Color.white);
                pix.FillCircle(60, 90, 22, new Color(1f, 1f, 1f, 0.55f));
                pix.FillCircle(120, 70, 14, new Color(1f, 1f, 1f, 0.35f));
            }
            else if (kind == 1)
            {
                pix.FillRect(0, 250, 300, 140, new Color(0.04f, 0.32f, 0.46f, 1f));
                pix.FillRect(0, 268, 300, 8, new Color(0.75f, 0.9f, 0.92f, 0.35f));
            }
            else if (kind == 2)
            {
                pix.FillRect(0, 292, 300, 5, new Color(1f, 1f, 1f, 0.22f));
                pix.FillCircle(78, 210, 78, new Color(1f, 0.35f, 0.05f, 0.95f));
                pix.FillCircle(70, 200, 46, new Color(1f, 0.82f, 0.28f, 0.95f));
            }
            else if (kind == 3)
            {
                pix.FillRect(0, 300, 300, 8, new Color(0.2f, 0.35f, 0.22f, 1f));
                for (int i = 0; i < 5; i++)
                    pix.FillCircle(40 + i * 55, 250, 18 + (i % 2) * 8, new Color(0.12f, 0.32f, 0.16f, 0.9f));
            }
            else
            {
                pix.FillRect(0, 292, 300, 5, new Color(1f, 1f, 1f, 0.18f));
                pix.Line(16, 330, 140, 286, 4f, new Color(1f, 1f, 1f, 0.28f));
            }

            switch (kind)
            {
                case 1:
                    DrawSideBoat(pix);
                    break;
                case 2:
                    DrawSideCar(pix, new Color(0.12f, 0.38f, 0.95f), new Color(0.85f, 0.9f, 0.95f));
                    break;
                case 3:
                    DrawSideBike(pix);
                    break;
                case 4:
                    DrawSideTruck(pix);
                    break;
                default:
                    DrawSidePlane(pix);
                    break;
            }

            pix.Vignette(0.4f);
            return pix.ToSprite(Vector4.zero);
        }

        static void DrawSideCar(Pix pix, Color body, Color trim)
        {
            var dark = Color.Lerp(body, Color.black, 0.62f);
            var glass = new Color(0.04f, 0.07f, 0.12f, 0.96f);
            pix.FillEllipse(160f, 268f, 130f, 16f, new Color(0f, 0f, 0f, 0.38f));
            pix.FillCircle(96, 236, 28, new Color(0.06f, 0.06f, 0.07f, 1f));
            pix.FillCircle(214, 236, 28, new Color(0.06f, 0.06f, 0.07f, 1f));
            pix.FillCircle(96, 236, 13, trim);
            pix.FillCircle(214, 236, 13, trim);
            pix.FillCircle(96, 236, 4, dark);
            pix.FillCircle(214, 236, 4, dark);
            pix.RoundRect(34, 176, 214, 64, 18, body);
            pix.FillPolygon(body, (236f, 186f), (286f, 206f), (286f, 228f), (240f, 234f));
            pix.FillPolygon(dark, (112f, 178f), (148f, 124f), (214f, 124f), (240f, 178f));
            pix.FillPolygon(glass, (124f, 174f), (152f, 132f), (208f, 132f), (228f, 174f));
            pix.FillRect(40, 198, 196, 8, Color.Lerp(Color.white, body, 0.2f));
            pix.RoundRect(24, 156, 48, 12, 3, dark);
            pix.FillRect(36, 166, 8, 16, dark);
            pix.FillRect(272, 208, 14, 8, new Color(1f, 0.96f, 0.75f, 1f));
            pix.FillRect(30, 196, 12, 10, new Color(1f, 0.16f, 0.18f, 1f));
        }

        static void DrawSidePlane(Pix pix)
        {
            var body = new Color(0.92f, 0.94f, 0.97f, 1f);
            var accent = new Color(0.95f, 0.38f, 0.12f, 1f);
            var dark = new Color(0.2f, 0.24f, 0.3f, 1f);
            var glass = new Color(0.15f, 0.35f, 0.55f, 0.95f);
            pix.FillEllipse(150f, 250f, 120f, 14f, new Color(0f, 0f, 0f, 0.35f));
            // Ailes.
            pix.FillPolygon(accent, (70f, 188f), (230f, 188f), (250f, 208f), (50f, 208f));
            pix.FillPolygon(Color.Lerp(accent, Color.black, 0.25f), (80f, 192f), (220f, 192f), (235f, 204f), (65f, 204f));
            // Fuselage.
            pix.FillEllipse(150f, 186f, 110f, 28f, body);
            pix.FillEllipse(150f, 180f, 100f, 18f, Color.Lerp(body, Color.white, 0.35f));
            // Cockpit.
            pix.FillEllipse(230f, 178f, 28f, 16f, glass);
            pix.FillEllipse(236f, 174f, 12f, 8f, new Color(0.55f, 0.8f, 0.95f, 0.7f));
            // Queue.
            pix.FillPolygon(body, (48f, 186f), (78f, 186f), (62f, 130f), (48f, 148f));
            pix.FillPolygon(accent, (52f, 170f), (88f, 178f), (88f, 190f), (52f, 186f));
            // Hélice.
            pix.FillCircle(262, 186, 10, dark);
            pix.FillEllipse(278f, 186f, 18f, 5f, dark);
            pix.FillEllipse(262f, 168f, 5f, 16f, new Color(0.3f, 0.34f, 0.4f, 0.85f));
            // Fenêtres.
            for (int i = 0; i < 4; i++)
                pix.FillCircle(120 + i * 22, 180, 5, glass);
            // Train d'atterrissage.
            pix.FillRect(118, 210, 6, 28, dark);
            pix.FillRect(188, 210, 6, 28, dark);
            pix.FillCircle(121, 240, 10, new Color(0.08f, 0.08f, 0.1f, 1f));
            pix.FillCircle(191, 240, 10, new Color(0.08f, 0.08f, 0.1f, 1f));
            pix.FillCircle(121, 240, 4, Color.gray);
            pix.FillCircle(191, 240, 4, Color.gray);
        }

        static void DrawSideBoat(Pix pix)
        {
            var hull = new Color(0.04f, 0.33f, 0.4f, 1f);
            var deck = new Color(0.93f, 0.9f, 0.82f, 1f);
            var cabin = new Color(0.08f, 0.16f, 0.28f, 1f);
            var glass = new Color(0.55f, 0.84f, 0.88f, 0.95f);
            pix.FillEllipse(150f, 268f, 130f, 14f, new Color(0f, 0f, 0f, 0.28f));
            pix.FillPolygon(hull, (36f, 214f), (236f, 206f), (286f, 248f), (28f, 252f));
            pix.FillPolygon(new Color(0.02f, 0.2f, 0.26f, 1f), (48f, 228f), (250f, 224f), (274f, 248f), (40f, 250f));
            pix.RoundRect(78, 168, 118, 46, 8, deck);
            pix.FillPolygon(cabin, (156f, 168f), (214f, 168f), (198f, 128f), (168f, 128f));
            pix.FillRect(172, 136, 22, 18, glass);
            pix.FillRect(250, 214, 16, 8, new Color(0.95f, 0.35f, 0.2f, 1f));
        }

        static void DrawSideTruck(Pix pix)
        {
            var cab = new Color(0.9f, 0.22f, 0.16f, 1f);
            var box = new Color(0.75f, 0.78f, 0.82f, 1f);
            var dark = new Color(0.12f, 0.12f, 0.14f, 1f);
            var glass = new Color(0.15f, 0.28f, 0.4f, 0.95f);
            pix.FillEllipse(155f, 268f, 135f, 16f, new Color(0f, 0f, 0f, 0.38f));
            // Remorque.
            pix.RoundRect(28, 148, 150, 96, 8, box);
            pix.FillRect(36, 156, 134, 10, Color.Lerp(box, Color.white, 0.25f));
            pix.FillRect(36, 180, 134, 4, new Color(0.45f, 0.48f, 0.52f, 1f));
            pix.FillRect(36, 204, 134, 4, new Color(0.45f, 0.48f, 0.52f, 1f));
            // Cabine.
            pix.RoundRect(178, 160, 96, 84, 10, cab);
            pix.FillPolygon(glass, (190f, 168f), (250f, 168f), (262f, 200f), (190f, 200f));
            pix.FillRect(186, 208, 80, 8, Color.Lerp(cab, Color.white, 0.2f));
            pix.FillRect(262, 220, 14, 10, new Color(1f, 0.95f, 0.7f, 1f));
            pix.FillRect(182, 220, 12, 12, new Color(1f, 0.2f, 0.15f, 1f));
            // Roues.
            pix.FillCircle(70, 248, 26, dark);
            pix.FillCircle(130, 248, 26, dark);
            pix.FillCircle(220, 248, 28, dark);
            pix.FillCircle(70, 248, 11, new Color(0.7f, 0.72f, 0.75f, 1f));
            pix.FillCircle(130, 248, 11, new Color(0.7f, 0.72f, 0.75f, 1f));
            pix.FillCircle(220, 248, 12, new Color(0.7f, 0.72f, 0.75f, 1f));
        }

        static void DrawSideBike(Pix pix)
        {
            var frame = new Color(0.95f, 0.78f, 0.1f, 1f);
            var dark = new Color(0.1f, 0.1f, 0.12f, 1f);
            var seat = new Color(0.25f, 0.18f, 0.12f, 1f);
            var rim = new Color(0.7f, 0.72f, 0.74f, 1f);
            var hubFill = new Color(0.14f, 0.32f, 0.22f, 1f);
            pix.FillEllipse(150f, 262f, 110f, 12f, new Color(0f, 0f, 0f, 0.3f));
            // Roues (pneu + jante + centre, sans trou transparent).
            DrawBikeWheel(pix, 80, 230, dark, rim, hubFill);
            DrawBikeWheel(pix, 220, 230, dark, rim, hubFill);
            // Cadre.
            pix.Line(80, 230, 140, 160, 7f, frame);
            pix.Line(140, 160, 210, 170, 7f, frame);
            pix.Line(140, 160, 150, 230, 6f, frame);
            pix.Line(150, 230, 220, 230, 6f, frame);
            pix.Line(210, 170, 220, 230, 6f, frame);
            pix.Line(80, 230, 150, 230, 5f, frame);
            // Selle et guidon.
            pix.RoundRect(124, 148, 36, 12, 4, seat);
            pix.Line(210, 170, 210, 140, 5f, dark);
            pix.Line(192, 140, 228, 140, 6f, dark);
            // Pédalier.
            pix.FillCircle(150, 230, 12, dark);
            pix.FillRect(146, 218, 8, 28, frame);
        }

        static void DrawBikeWheel(Pix pix, float cx, float cy, Color tire, Color rim, Color hub)
        {
            pix.FillCircle(cx, cy, 42, tire);
            pix.FillCircle(cx, cy, 30, hub);
            pix.FillCircle(cx, cy, 28, rim);
            pix.FillCircle(cx, cy, 18, hub);
            pix.FillCircle(cx, cy, 8, tire);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                pix.Line(cx + Mathf.Cos(a) * 8, cy + Mathf.Sin(a) * 8, cx + Mathf.Cos(a) * 26, cy + Mathf.Sin(a) * 26, 2f, tire);
            }
        }

        static Sprite PaintIcon(string name)
        {
            var pix = new Pix(64, 64);
            var color = Color.white;
            switch (name)
            {
                case "chevron":
                    pix.Line(18, 10, 46, 32, 8f, color);
                    pix.Line(46, 32, 18, 54, 8f, color);
                    break;
                case "shop":
                    pix.Line(16, 16, 26, 16, 4f, color);
                    pix.Line(26, 16, 32, 28, 4f, color);
                    pix.FillPolygon(color, (24f, 30f), (50f, 30f), (46f, 46f), (28f, 46f));
                    pix.FillCircle(32, 54, 4, color);
                    pix.FillCircle(44, 54, 4, color);
                    break;
                case "gear":
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI * 0.25f;
                        pix.Line(32 + Mathf.Cos(angle) * 10f, 32 + Mathf.Sin(angle) * 10f,
                            32 + Mathf.Cos(angle) * 28f, 32 + Mathf.Sin(angle) * 28f, 7f, color);
                    }
                    pix.FillCircle(32, 32, 16, color);
                    pix.EraseCircle(32, 32, 7);
                    break;
                case "trophy":
                    pix.FillPolygon(color, (18f, 14f), (46f, 14f), (40f, 36f), (24f, 36f));
                    pix.Line(18, 20, 10, 20, 4f, color);
                    pix.Line(10, 20, 12, 32, 4f, color);
                    pix.Line(12, 32, 22, 32, 4f, color);
                    pix.Line(46, 20, 54, 20, 4f, color);
                    pix.Line(54, 20, 52, 32, 4f, color);
                    pix.Line(52, 32, 42, 32, 4f, color);
                    pix.FillRect(29, 36, 6, 10, color);
                    pix.RoundRect(20, 46, 24, 8, 2, color);
                    break;
                case "medal":
                    pix.FillRect(24, 34, 7, 18, color);
                    pix.FillRect(34, 34, 7, 18, color);
                    pix.FillCircle(32, 26, 16, color);
                    pix.EraseCircle(32, 26, 8);
                    pix.FillStar(32, 26, 6, 3, color);
                    break;
                default:
                    pix.ArcTop(32f, 30f, 14f, 5f, color);
                    pix.RoundRect(16, 28, 32, 26, 5, color);
                    break;
            }
            return pix.ToSprite(Vector4.zero);
        }

        sealed class Pix
        {
            readonly Color[] _pixels;
            readonly int _w;
            readonly int _h;

            public Pix(int width, int height)
            {
                _w = width;
                _h = height;
                _pixels = new Color[width * height];
            }

            public void Fill(Color color)
            {
                for (int i = 0; i < _pixels.Length; i++)
                    _pixels[i] = color;
            }

            public void Plot(int x, int y, Color color)
            {
                if ((uint)x >= (uint)_w || (uint)y >= (uint)_h || color.a <= 0.004f)
                    return;
                int index = (_h - 1 - y) * _w + x;
                var dst = _pixels[index];
                float a = color.a;
                float inv = (1f - a) * dst.a;
                float outA = a + inv;
                _pixels[index] = new Color(
                    (color.r * a + dst.r * inv) / outA,
                    (color.g * a + dst.g * inv) / outA,
                    (color.b * a + dst.b * inv) / outA,
                    outA);
            }

            public void FillRect(int x, int y, int width, int height, Color color)
            {
                for (int row = y; row < y + height; row++)
                {
                    for (int col = x; col < x + width; col++)
                        Plot(col, row, color);
                }
            }

            public void VGradient(int y0, int y1, Color top, Color bottom)
            {
                int span = Mathf.Max(1, y1 - y0);
                for (int y = y0; y <= y1; y++)
                {
                    var color = Color.Lerp(top, bottom, (y - y0) / (float)span);
                    for (int x = 0; x < _w; x++)
                        Plot(x, y, color);
                }
            }

            public void FillCircle(float cx, float cy, float radius, Color color)
            {
                int x0 = Mathf.FloorToInt(cx - radius - 1f);
                int y0 = Mathf.FloorToInt(cy - radius - 1f);
                int x1 = Mathf.CeilToInt(cx + radius + 1f);
                int y1 = Mathf.CeilToInt(cy + radius + 1f);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float dy = y + 0.5f - cy;
                        float edge = radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.65f;
                        if (edge <= 0f)
                            continue;
                        var dot = color;
                        dot.a *= Mathf.Clamp01(edge);
                        Plot(x, y, dot);
                    }
                }
            }

            public void EraseCircle(float cx, float cy, float radius)
            {
                int x0 = Mathf.FloorToInt(cx - radius);
                int y0 = Mathf.FloorToInt(cy - radius);
                int x1 = Mathf.CeilToInt(cx + radius);
                int y1 = Mathf.CeilToInt(cy + radius);
                float limit = radius * radius;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if ((uint)x >= (uint)_w || (uint)y >= (uint)_h)
                            continue;
                        float dx = x + 0.5f - cx;
                        float dy = y + 0.5f - cy;
                        if (dx * dx + dy * dy <= limit)
                            _pixels[(_h - 1 - y) * _w + x] = default;
                    }
                }
            }

            public void FillEllipse(float cx, float cy, float rx, float ry, Color color)
            {
                int x0 = Mathf.FloorToInt(cx - rx);
                int y0 = Mathf.FloorToInt(cy - ry);
                int x1 = Mathf.CeilToInt(cx + rx);
                int y1 = Mathf.CeilToInt(cy + ry);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float nx = (x + 0.5f - cx) / rx;
                        float ny = (y + 0.5f - cy) / ry;
                        if (nx * nx + ny * ny <= 1f)
                            Plot(x, y, color);
                    }
                }
            }

            public void RoundRect(int x, int y, int width, int height, int radius, Color color)
            {
                radius = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);
                FillRect(x + radius, y, width - radius * 2, height, color);
                FillRect(x, y + radius, radius, height - radius * 2, color);
                FillRect(x + width - radius, y + radius, radius, height - radius * 2, color);
                FillCircle(x + radius, y + radius, radius, color);
                FillCircle(x + width - radius - 1, y + radius, radius, color);
                FillCircle(x + radius, y + height - radius - 1, radius, color);
                FillCircle(x + width - radius - 1, y + height - radius - 1, radius, color);
            }

            public void Line(float x0, float y0, float x1, float y1, float thickness, Color color)
            {
                float dx = x1 - x0;
                float dy = y1 - y0;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(dx * dx + dy * dy)));
                float radius = Mathf.Max(0.6f, thickness * 0.5f);
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    FillCircle(x0 + dx * t, y0 + dy * t, radius, color);
                }
            }

            public void FillPolygon(Color color, params (float x, float y)[] points)
            {
                int count = points.Length;
                if (count < 3)
                    return;
                float minY = points[0].y;
                float maxY = points[0].y;
                for (int i = 1; i < count; i++)
                {
                    minY = Mathf.Min(minY, points[i].y);
                    maxY = Mathf.Max(maxY, points[i].y);
                }

                int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
                int y1 = Mathf.Min(_h - 1, Mathf.CeilToInt(maxY));
                var hits = new List<float>(8);
                for (int y = y0; y <= y1; y++)
                {
                    float scan = y + 0.5f;
                    hits.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        var a = points[i];
                        var b = points[(i + 1) % count];
                        if ((a.y <= scan && b.y > scan) || (b.y <= scan && a.y > scan))
                        {
                            float t = (scan - a.y) / (b.y - a.y);
                            hits.Add(a.x + (b.x - a.x) * t);
                        }
                    }
                    hits.Sort();
                    for (int i = 0; i + 1 < hits.Count; i += 2)
                    {
                        int xStart = Mathf.Max(0, Mathf.FloorToInt(hits[i]));
                        int xEnd = Mathf.Min(_w - 1, Mathf.CeilToInt(hits[i + 1]));
                        for (int x = xStart; x <= xEnd; x++)
                            Plot(x, y, color);
                    }
                }
            }

            public void FillStar(float cx, float cy, float outer, float inner, Color color)
            {
                var points = new (float x, float y)[10];
                for (int i = 0; i < 10; i++)
                {
                    float angle = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                    float radius = i % 2 == 0 ? outer : inner;
                    points[i] = (cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius);
                }
                FillPolygon(color, points);
            }

            public void ArcTop(float cx, float cy, float radius, float thickness, Color color)
            {
                int x0 = Mathf.FloorToInt(cx - radius - thickness);
                int y0 = Mathf.FloorToInt(cy - radius - thickness);
                int x1 = Mathf.CeilToInt(cx + radius + thickness);
                int y1 = Mathf.CeilToInt(cy);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float dy = y + 0.5f - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float edge = thickness * 0.5f - Mathf.Abs(dist - radius) + 0.65f;
                        if (edge <= 0f)
                            continue;
                        var dot = color;
                        dot.a *= Mathf.Clamp01(edge);
                        Plot(x, y, dot);
                    }
                }
            }

            public void Vignette(float amount)
            {
                float cx = (_w - 1) * 0.5f;
                float cy = (_h - 1) * 0.5f;
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        float nx = (x - cx) / cx;
                        float ny = (y - cy) / cy;
                        float fade = nx * nx * 0.55f + ny * ny;
                        if (fade > 0.45f)
                            Plot(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01((fade - 0.45f) * amount)));
                    }
                }
            }

            public Sprite ToSprite(Vector4 border)
            {
                var texture = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.SetPixels(_pixels);
                texture.Apply();
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, _w, _h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }
        }
    }
}
