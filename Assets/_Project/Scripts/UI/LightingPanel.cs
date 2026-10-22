using TMPro;
using UnityEngine;

namespace Lumaquarium.UI
{
    // The lighting sheet: light from above (daylight or a lamp), the lamp's colour and the backlight.
    // Each row is a tray of tabs like the decorate sheet; the selected tab turns green.
    public sealed class LightingPanel : MonoBehaviour
    {
        public AquariumLighting lighting;
        public UnityEngine.UI.Button[] topButtons;   // Daylight, Lamp
        public UnityEngine.UI.Button[] colorButtons; // White, Blue, Red (AquariumLighting.LampColor order)
        public UnityEngine.UI.Button[] backButtons;  // Off, White, Blue, Red
        public Color labelOn = new Color32(255, 248, 230, 255);
        public Color labelOff = new Color32(74, 80, 63, 255);

        private void Start()
        {
            for (int i = 0; i < topButtons.Length; i++)
            {
                int index = i;
                topButtons[i].onClick.AddListener(() => SelectTop(index));
            }
            for (int i = 0; i < colorButtons.Length; i++)
            {
                int index = i;
                colorButtons[i].onClick.AddListener(() => SelectLampColor(index));
            }
            for (int i = 0; i < backButtons.Length; i++)
            {
                int index = i;
                backButtons[i].onClick.AddListener(() => SelectBacklight(index));
            }
            Refresh();
        }

        private void OnEnable() => Refresh();

        public void SelectTop(int index)
        {
            if (lighting == null) return;
            lighting.SetTop((AquariumLighting.TopSource)index);
            Refresh();
        }

        public void SelectLampColor(int index)
        {
            if (lighting == null) return;
            lighting.SetLampColor((AquariumLighting.LampColor)index);
            Refresh();
        }

        // 0 turns the backlight off; 1-3 turn it on in white, blue or red.
        public void SelectBacklight(int index)
        {
            if (lighting == null) return;
            if (index == 0) lighting.SetBacklight(false);
            else lighting.SetBacklight((AquariumLighting.LampColor)(index - 1));
            Refresh();
        }

        public void Refresh()
        {
            if (lighting == null) return;
            bool lamp = lighting.Top == AquariumLighting.TopSource.Lamp;
            Mark(topButtons, lamp ? 1 : 0);
            // Daylight has no colour to pick, so no colour tab is lit until the lamp is on.
            Mark(colorButtons, lamp ? (int)lighting.TopColor : -1);
            Mark(backButtons, lighting.Backlight ? (int)lighting.BackColor + 1 : 0);
        }

        private void Mark(UnityEngine.UI.Button[] buttons, int selected)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool on = i == selected;
                buttons[i].targetGraphic.color = on ? Color.white : Color.clear;
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = on ? labelOn : labelOff;
            }
        }
    }
}
