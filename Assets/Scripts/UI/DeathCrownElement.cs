using UnityEngine;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    /// <summary>
    /// Draws the broken crown mark as a vector so the death overlay does not need
    /// a baked background image and stays sharp at different resolutions.
    /// </summary>
    public sealed class DeathCrownElement : VisualElement
    {
        private static readonly Color CrownColor = new(0.965f, 0.325f, 0.251f, 1f);
        private static readonly Color CrownShade = new(0.72f, 0.20f, 0.18f, 0.72f);

        public DeathCrownElement()
        {
            pickingMode = PickingMode.Ignore;
            style.flexGrow = 1f;
            generateVisualContent += DrawCrown;
        }

        private static void DrawCrown(MeshGenerationContext context)
        {
            Rect bounds = context.visualElement.contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return;
            }

            float width = bounds.width;
            float height = bounds.height;
            Painter2D painter = context.painter2D;

            painter.fillColor = CrownShade;
            painter.BeginPath();
            painter.MoveTo(new Vector2(width * 0.29f, height * 0.72f));
            painter.LineTo(new Vector2(width * 0.76f, height * 0.69f));
            painter.LineTo(new Vector2(width * 0.71f, height * 0.88f));
            painter.LineTo(new Vector2(width * 0.34f, height * 0.84f));
            painter.ClosePath();
            painter.Fill();

            painter.fillColor = CrownColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(width * 0.22f, height * 0.72f));
            painter.LineTo(new Vector2(width * 0.31f, height * 0.55f));
            painter.LineTo(new Vector2(width * 0.43f, height * 0.62f));
            painter.LineTo(new Vector2(width * 0.49f, height * 0.13f));
            painter.LineTo(new Vector2(width * 0.57f, height * 0.57f));
            painter.LineTo(new Vector2(width * 0.78f, height * 0.35f));
            painter.LineTo(new Vector2(width * 0.73f, height * 0.82f));
            painter.ClosePath();
            painter.Fill();

            DrawRay(painter, width, height, 0.16f, 0.48f, 0.06f, 0.39f, 7f);
            DrawRay(painter, width, height, 0.28f, 0.27f, 0.23f, 0.08f, 8f);
            DrawRay(painter, width, height, 0.83f, 0.48f, 0.94f, 0.43f, 7f);
            DrawRay(painter, width, height, 0.77f, 0.28f, 0.86f, 0.14f, 7f);
        }

        private static void DrawRay(
            Painter2D painter,
            float width,
            float height,
            float startX,
            float startY,
            float endX,
            float endY,
            float lineWidth)
        {
            painter.strokeColor = CrownColor;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            painter.MoveTo(new Vector2(width * startX, height * startY));
            painter.LineTo(new Vector2(width * endX, height * endY));
            painter.Stroke();
        }
    }
}
