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
        public GameObject lightingPanel;
        public GameObject tankPanel;
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
        // Icon-on-stone dock: the open sheet's icon lifts over a sage glow instead of swapping tiles.
        public GameObject careGlow;
        public GameObject decorateGlow;
        public GameObject feedGlow;
        public RectTransform careLift;
        public RectTransform decorateLift;
        public RectTransform feedLift;
        public float activeLift = 18;

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
            // Scenes built before feed mode existed: find the Feed stone's glow and lift by name.
            if (feedGlow == null && toolbar != null)
            {
                Transform button = toolbar.transform.Find("FeedButton");
                if (button != null)
                {
                    Transform glow = button.Find("Glow");
                    if (glow != null) feedGlow = glow.gameObject;
                    feedLift = button.Find("Lift") as RectTransform;
                }
            }
            if (feeding != null) feeding.FeedModeChanged += _ => UpdateTiles();
            UpdateMuteLabel();
            UpdateTiles();
        }

        private void Update()
        {
            if (fish != null)
            {
                float fullness = 1f - Mathf.Clamp01(fish.hunger);
                int percent = Mathf.RoundToInt(fullness * 100);
                SetFill(hungerFill, fullness);
                if (hungerPercent != null)
                {
                    hungerLabel.text = "Độ no";
                    hungerPercent.text = percent + "%";
                }
                else hungerLabel.text = $"Độ no     {percent}%";
                if (hudFullnessFill != null) SetFill(hudFullnessFill, fullness);
                if (hudFullnessLabel != null) hudFullnessLabel.text = percent + "%";
                behaviourLabel.text = fish.state switch
                {
                    BettaFishAI.State.SeekFood => "Đang tìm thức ăn",
                    BettaFishAI.State.Eat => "Đang ăn",
                    BettaFishAI.State.Rest => "Đang nghỉ",
                    BettaFishAI.State.Breathe => "Đang lên mặt nước",
                    BettaFishAI.State.Idle => "Đang thả mình",
                    BettaFishAI.State.SwimToTap => "Đang bơi tới chỗ bạn chạm",
                    BettaFishAI.State.Flee => "Giật mình bỏ chạy",
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

        // Sliced fills keep their rounded ends by resizing; older scenes still use Filled images.
        private static void SetFill(UnityEngine.UI.Image fill, float amount)
        {
            if (fill.type == UnityEngine.UI.Image.Type.Filled)
            {
                fill.fillAmount = amount;
                return;
            }
            var rect = fill.rectTransform;
            if (!Mathf.Approximately(rect.anchorMax.x, amount)) rect.anchorMax = new Vector2(amount, rect.anchorMax.y);
            fill.enabled = amount > .005f;
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

        // Toggles feed mode: while it is on a tap on the tank drops food, otherwise the fish swims to the tap.
        public void Feed()
        {
            decoration?.Leave();
            ClosePanels();
            if (feeding == null) return;
            feeding.SetFeedMode(!feeding.FeedMode);
            Notify(feeding.FeedMode ? "Chạm vào bể để thả thức ăn" : "Đã tắt chế độ cho ăn");
        }

        public void ToggleCare() => TogglePanel(carePanel);
        public void ToggleDecorate() => TogglePanel(decoratePanel);
        public void ToggleSettings() => TogglePanel(settingsPanel);
        public void ToggleLighting() { if (lightingPanel != null) TogglePanel(lightingPanel); }
        public void ToggleTanks() { if (tankPanel != null) TogglePanel(tankPanel); }

        public void OpenDecorate()
        {
            if (!decoratePanel.activeSelf) ToggleDecorate();
        }

        private void TogglePanel(GameObject panel)
        {
            decoration?.Leave();
            bool open = !panel.activeSelf;
            ClosePanels();
            if (open && feeding != null) feeding.SetFeedMode(false);
            panel.SetActive(open);
            if (orbit != null) orbit.enabled = !open;
            UpdateTiles();
        }

        public void ClosePanels()
        {
            carePanel.SetActive(false);
            decoratePanel.SetActive(false);
            settingsPanel.SetActive(false);
            if (lightingPanel != null) lightingPanel.SetActive(false);
            if (tankPanel != null) tankPanel.SetActive(false);
            if (orbit != null) orbit.enabled = true;
            UpdateTiles();
        }

        // The dock tile of an open sheet turns sage, as in the approved preview.
        private void UpdateTiles()
        {
            Highlight(careGlow, careLift, carePanel.activeSelf);
            Highlight(decorateGlow, decorateLift, decoratePanel.activeSelf);
            Highlight(feedGlow, feedLift, feeding != null && feeding.FeedMode);
            if (tileNormal == null || tileActive == null) return;
            if (careTile != null) careTile.sprite = carePanel.activeSelf ? tileActive : tileNormal;
            if (decorateTile != null) decorateTile.sprite = decoratePanel.activeSelf ? tileActive : tileNormal;
        }

        private void Highlight(GameObject glow, RectTransform lift, bool active)
        {
            if (glow != null) glow.SetActive(active);
            if (lift != null) lift.anchoredPosition = new Vector2(0, active ? activeLift : 0);
        }

        public void ToggleHud()
        {
            decoration?.Leave();
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
            orbit.pan = 0;
            ClosePanels();
        }

        public void TakePhoto()
        {
            if (capturing) return;
            decoration?.Leave();
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
