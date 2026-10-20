using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;

namespace Lumaquarium.UI
{
    public sealed class MainAquariumScreen : MonoBehaviour
    {
        public BettaFishAI fish;
        public FeedingSystem feeding;
        public OrbitTouchCamera orbit;
        public GameObject header;
        public GameObject toolbar;
        public GameObject restoreButton;
        public GameObject carePanel;
        public GameObject decoratePanel;
        public GameObject settingsPanel;
        public TMP_Text hungerLabel;
        public UnityEngine.UI.Image hungerFill;
        public TMP_Text behaviourLabel;
        public TMP_Text feedback;
        public TMP_Text muteLabel;
        public CanvasGroup hud;
        public AquariumDecoration decoration;

        private Vector3 defaultView;
        private float feedbackUntil;
        private bool hidden;
        private bool capturing;

        private void Start()
        {
            // Orbit fits portrait screens in Start, so derive the same fit before caching.
            if (orbit != null)
            {
                orbit.FitToScreen();
                defaultView = new Vector3(orbit.yaw, orbit.pitch, orbit.distance);
            }
            UpdateMuteLabel();
        }

        private void Update()
        {
            if (fish != null)
            {
                float fullness = 1f - Mathf.Clamp01(fish.hunger);
                hungerFill.fillAmount = fullness;
                hungerLabel.text = $"Độ no     {Mathf.RoundToInt(fullness * 100)}%";
                behaviourLabel.text = fish.state switch
                {
                    BettaFishAI.State.SeekFood => "Đang tìm thức ăn",
                    BettaFishAI.State.Eat => "Đang ăn",
                    BettaFishAI.State.Rest => "Đang nghỉ",
                    BettaFishAI.State.Breathe => "Đang lên mặt nước",
                    BettaFishAI.State.Idle => "Đang thả mình",
                    _ => "Đang bơi thong thả"
                };
            }
            if (feedback.gameObject.activeSelf && Time.unscaledTime >= feedbackUntil)
                feedback.gameObject.SetActive(false);
        }

        public void Feed()
        {
            decoration?.Cancel();
            ClosePanels();
            if (feeding != null) feeding.Feed();
        }

        public void ToggleCare() => TogglePanel(carePanel);
        public void ToggleDecorate() => TogglePanel(decoratePanel);
        public void ToggleSettings() => TogglePanel(settingsPanel);

        private void TogglePanel(GameObject panel)
        {
            decoration?.Cancel();
            bool open = !panel.activeSelf;
            ClosePanels();
            panel.SetActive(open);
            if (orbit != null) orbit.enabled = !open;
        }

        public void ClosePanels()
        {
            carePanel.SetActive(false);
            decoratePanel.SetActive(false);
            settingsPanel.SetActive(false);
            if (orbit != null) orbit.enabled = true;
        }

        public void ToggleHud()
        {
            decoration?.Cancel();
            ClosePanels();
            hidden = !hidden;
            header.SetActive(!hidden);
            toolbar.SetActive(!hidden);
            restoreButton.SetActive(hidden);
            feedback.gameObject.SetActive(false);
        }

        public void ToggleMute()
        {
            AudioListener.volume = AudioListener.volume > 0 ? 0 : 1;
            UpdateMuteLabel();
        }

        private void UpdateMuteLabel() => muteLabel.text = AudioListener.volume > 0 ? "Âm thanh: Bật" : "Âm thanh: Tắt";

        public void ResetView()
        {
            if (orbit == null) return;
            orbit.yaw = defaultView.x;
            orbit.pitch = defaultView.y;
            orbit.distance = defaultView.z;
            ClosePanels();
        }

        public void TakePhoto()
        {
            if (capturing) return;
            decoration?.Cancel();
            ClosePanels();
            StartCoroutine(CapturePhoto());
        }

        private IEnumerator CapturePhoto()
        {
            capturing = true;
            hud.alpha = 0;
            hud.blocksRaycasts = false;
            if (orbit != null) orbit.enabled = false;
            yield return new WaitForEndOfFrame();
            Texture2D photo = null;
            try
            {
                photo = ScreenCapture.CaptureScreenshotAsTexture();
                string folder = Path.Combine(Application.persistentDataPath, "Photos");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"Lumaquarium-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
                File.WriteAllBytes(path, photo.EncodeToPNG());
                Debug.Log("[Lumaquarium] Photo saved: " + path);
                Notify("Đã lưu ảnh vào thư mục Photos của ứng dụng");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Lumaquarium] Photo could not be saved: " + exception.Message);
                Notify("Chưa lưu được ảnh. Bạn có thể thử lại.");
            }
            finally
            {
                if (photo != null) Destroy(photo);
                RestoreAfterCapture();
            }
        }

        private void Notify(string message)
        {
            feedback.text = message;
            feedbackUntil = Time.unscaledTime + 4;
            feedback.gameObject.SetActive(true);
        }

        private void RestoreAfterCapture()
        {
            hud.alpha = 1;
            hud.blocksRaycasts = true;
            if (orbit != null) orbit.enabled = true;
            capturing = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused || !capturing) return;
            StopAllCoroutines();
            RestoreAfterCapture();
        }

        private void OnDisable()
        {
            if (capturing) RestoreAfterCapture();
        }
    }
}
