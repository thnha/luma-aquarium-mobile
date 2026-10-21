using TMPro;
using UnityEngine;

namespace Lumaquarium.UI
{
    // Keeps a button label readable on the grey disabled surface of the 2.5D buttons.
    public sealed class Ui3DButtonLabel : MonoBehaviour
    {
        public UnityEngine.UI.Button button;
        public TMP_Text label;
        public Color normal = Color.white;
        public Color disabled = new Color32(110, 106, 92, 255);

        private void LateUpdate()
        {
            if (button == null || label == null) return;
            var color = button.IsInteractable() ? normal : disabled;
            if (label.color != color) label.color = color;
        }
    }
}
