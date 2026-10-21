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
        // Redesigned HUD (docs/ui-redesign.md). All optional so older scenes keep working.
        public FishShopScreen fishShop;
        public TMP_Text walletLabel;
        public UnityEngine.UI.Image hudFullnessFill;
        public TMP_Text hudFullnessLabel;
        public TMP_Text hungerPercent;
        public TMP_Text fishNameLabel;
        public UnityEngine.UI.Image fishPortrait;
        public TMP_Text careFishName;
        public UnityEngine.UI.Image carePortrait;
        public UnityEngine.UI.Image careTile;
        public UnityEngine.UI.Image decorateTile;
        public Sprite tileNormal;
        public Sprite tileActive;

        private Vector3 defaultView;
        private float feedbackUntil;
        private bool hidden;
        private bool capturing;
        private float walletRefreshAt;

        private void Start()
        {
            // Orbit fits portrait screens in Start, so derive the same fit before caching.
            if (orbit != null)
            {
                orbit.FitToScreen();
                defaultView = new Vector3(orbit.yaw, orbit.pitch, orbit.distance);
            }
            UpdateMuteLabel();
            UpdateTiles();
        }

        private void Update()
        {
            if (fish != null)
            {
                float fullness = 1f - Mathf.Clamp01(fish.hunger);
                int percent = Mathf.RoundToInt(fullness * 100);
                hungerFill.fillAmount = fullness;
                if (hungerPercent != null)
                {
                    hungerLabel.text = "Độ no";
                    hungerPercent.text = percent + "%";
                }
                else hungerLabel.text = $"Độ no     {percent}%";
                if (hudFullnessFill != null) hudFullnessFill.fillAmount = fullness;
                if (hudFullnessLabel != null) hudFullnessLabel.text = percent + "%";
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
            if (careFishName != null && fishNameLabel != null && careFishName.text != fishNameLabel.text)
                careFishName.text = fishNameLabel.text;
            if (carePortrait != null && fishPortrait != null && carePortrait.sprite != fishPortrait.sprite)
                carePortrait.sprite = fishPortrait.sprite;
            if (walletLabel != null && Time.unscaledTime >= walletRefreshAt) RefreshWallet();
        }

        // The wallet is shared with both shops and saved in PlayerPrefs; poll it cheaply.
        public void RefreshWallet()
        {
            walletRefreshAt = Time.unscaledTime + .5f;
            if (walletLabel == null) return;
            string key = decoration != null ? decoration.inventoryStorageKey : FishShopInventory.DefaultStorageKey;
            walletLabel.text = new FishShopInventory(key).Balance.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
        }

        public void OpenShop()
        {
            if (fishShop != null) fishShop.Open();
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
            UpdateTiles();
        }

        public void ClosePanels()
        {
            carePanel.SetActive(false);
            decoratePanel.SetActive(false);
            settingsPanel.SetActive(false);
            if (orbit != null) orbit.enabled = true;
            UpdateTiles();
        }

        // The dock tile of an open sheet turns sage, as in the approved preview.
        private void UpdateTiles()
        {
            if (tileNormal == null || tileActive == null) return;
            if (careTile != null) careTile.sprite = carePanel.activeSelf ? tileActive : tileNormal;
            if (decorateTile != null) decorateTile.sprite = decoratePanel.activeSelf ? tileActive : tileNormal;
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

        private void UpdateMuteLabel() => muteLabel.text = AudioListener.volume > 0 ? "Đang bật" : "Đang tắt";

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
