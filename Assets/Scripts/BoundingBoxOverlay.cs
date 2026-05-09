using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EI.VR
{
    /// <summary>
    /// Draws bounding boxes over the demo viewport. Boxes are positioned by
    /// normalised (0..1) image-space rects produced by FomoOutputParser.
    ///
    /// Expected scene setup:
    ///   - A world-space Canvas attached to the demo viewport quad whose
    ///     RectTransform exactly covers the visible RawImage feed.
    ///   - This component on that Canvas (or a child of it).
    ///   - A `boxPrefab` with an Image (transparent fill) and a TMP_Text label
    ///     child anchored to the rect.
    /// </summary>
    public class BoundingBoxOverlay : MonoBehaviour
    {
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private GameObject boxPrefab;
        [SerializeField] private string[] classNames = { "object" };

        private readonly List<RectTransform> _pool = new();

        public void Render(IReadOnlyList<FomoOutputParser.Detection> detections)
        {
            if (overlayRoot == null || boxPrefab == null) return;

            // Pool grow.
            while (_pool.Count < detections.Count)
            {
                var go = Instantiate(boxPrefab, overlayRoot);
                _pool.Add(go.GetComponent<RectTransform>());
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                if (i >= detections.Count)
                {
                    _pool[i].gameObject.SetActive(false);
                    continue;
                }
                var d = detections[i];
                var rt = _pool[i];
                rt.gameObject.SetActive(true);

                // Image space y-axis goes top-to-bottom; UI rect's y-axis is
                // bottom-to-top. Flip y when mapping.
                var size = overlayRoot.rect.size;
                var anchoredX = d.rect.x * size.x;
                var anchoredY = (1f - d.rect.y - d.rect.height) * size.y;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(anchoredX, anchoredY);
                rt.sizeDelta = new Vector2(d.rect.width * size.x, d.rect.height * size.y);

                // Optional label child with class name and score.
                var label = rt.GetComponentInChildren<TMPro.TMP_Text>();
                if (label != null)
                {
                    string cls = (d.classIndex - 1 >= 0 && d.classIndex - 1 < classNames.Length)
                        ? classNames[d.classIndex - 1]
                        : $"class_{d.classIndex}";
                    label.text = $"{cls} {d.score:P0}";
                }
            }
        }
    }
}
