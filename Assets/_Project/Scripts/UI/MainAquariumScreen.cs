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
        // Chip behind the feedback text; older scenes have none and toggle the text itself.
        public GameObject feedbackRoot;
        public TMP_Text muteLabel;
        // Settings row that switches between Vietnamese and English; older scenes have none.
        public TMP_Text languageLabel;
        public CanvasGroup hud;
        public AquariumDecoration decoration;
        // Redesigned HUD (docs/ui-redesign.md). All optional so older scenes keep working.
        public FishShopScreen fishShop;
        public ShopScreen shop;
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
        // Feed mode shows the jar with its lid off.
        public UnityEngine.UI.Image feedIcon;
        public Sprite feedIconOpen;
        public float activeLift = 18;

        private GameObject FeedbackRoot => feedbackRoot != null ? feedbackRoot : feedback.gameObject;
        private Sprite feedIconClosed;
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
            if (feedIcon != null) feedIconClosed = feedIcon.sprite;
            if (feeding != null) feeding.FeedModeChanged += _ => UpdateTiles();
            UpdateMuteLabel();
            UpdateLanguageLabel();
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
                    hungerLabel.text = Loc.T("Độ no");
                    hungerPercent.text = percent + "%";
                }
                else hungerLabel.text = Loc.T("Độ no") + $"     {percent}%";
                if (hudFullnessFill != null) SetFill(hudFullnessFill, fullness);
                if (hudFullnessLabel != null) hudFullnessLabel.text = percent + "%";
                // Turns between what it is doing (with its mood) and how close it is to you.
                var mood = fish.Mood;
                bool showBond = mood != null && Time.unscaledTime % 8f >= 5f;
                behaviourLabel.enableAutoSizing = true;
                behaviourLabel.fontSizeMin = 24;
                behaviourLabel.fontSizeMax = 36;
                behaviourLabel.text = showBond ? Loc.F("Thân thiết {0}% · {1}", Mathf.RoundToInt(mood.Bond * 100), Loc.T(BondTitle(mood.Bond)))
                    : mood != null ? Loc.T(FeelingLabel(mood.Current)) + " · " + Loc.T(BehaviourText(fish)) : Loc.T(BehaviourText(fish));
            }
            if (FeedbackRoot.activeSelf && Time.unscaledTime >= feedbackUntil)
                FeedbackRoot.SetActive(false);
            if (careFishName != null && fishNameLabel != null && careFishName.text != fishNameLabel.text)
                careFishName.text = fishNameLabel.text;
            if (carePortrait != null && fishPortrait != null && carePortrait.sprite != fishPortrait.sprite)
                carePortrait.sprite = fishPortrait.sprite;
            if (walletLabel != null && Time.unscaledTime >= walletRefreshAt) RefreshWallet();
        }

        private static string BehaviourText(BettaFishAI fish) => fish.state switch
        {
            BettaFishAI.State.SeekFood => "Đang tìm thức ăn",
            BettaFishAI.State.Eat => "Đang ăn",
            BettaFishAI.State.Rest => fish.IsSleeping ? "Đang ngủ" : "Đang nghỉ",
            BettaFishAI.State.Breathe => "Đang lên mặt nước",
            BettaFishAI.State.Idle => "Đang thả mình",
            BettaFishAI.State.SwimToTap => "Đang bơi tới chỗ bạn chạm",
            BettaFishAI.State.Flee => "Giật mình bỏ chạy",
            BettaFishAI.State.Display => "Đang xòe vây",
            BettaFishAI.State.Forage => "Đang kiếm ăn",
            BettaFishAI.State.Patrol => "Đang tuần tra",
            BettaFishAI.State.Greet => "Đang chào bạn",
            BettaFishAI.State.Inspect => "Đang xem đồ mới",
            BettaFishAI.State.Nest => "Đang làm tổ bọt",
            BettaFishAI.State.Hide => "Đang trốn",
            _ => "Đang bơi thong thả"
        };

        private static string FeelingLabel(BettaMood.Feeling feeling) => feeling switch
        {
            BettaMood.Feeling.Happy => "Vui",
            BettaMood.Feeling.Bored => "Chán",
            BettaMood.Feeling.Scared => "Sợ",
            BettaMood.Feeling.Hungry => "Đói",
            BettaMood.Feeling.Sleepy => "Buồn ngủ",
            _ => "Bình thường"
        };

        private static string BondTitle(float bond) => bond < 0.2f ? "Còn lạ" : bond < 0.45f ? "Quen dần" : bond < 0.7f ? "Thân" : "Rất thân";

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
            walletLabel.text = Loc.Money(new FishShopInventory(key).Balance);
        }

        public void OpenShop()
        {
            if (shop != null) shop.Open();
            else if (fishShop != null) fishShop.Open();
        }

        // Đổi cá: the shop on the Cá tab with the active fish selected.
        public void OpenFishShop()
        {
            if (shop == null) { OpenShop(); return; }
            string key = decoration != null ? decoration.inventoryStorageKey : FishShopInventory.DefaultStorageKey;
            shop.OpenTab(ShopTab.Fish, new FishShopInventory(key).ActiveId);
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
        public void ToggleTanks()
        {
            if (shop != null) shop.OpenTab(ShopTab.Tanks);
            else if (tankPanel != null) TogglePanel(tankPanel);
        }

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
            bool feedOn = feeding != null && feeding.FeedMode;
            Highlight(feedGlow, feedLift, feedOn);
            if (feedIcon != null && feedIconOpen != null && feedIconClosed != null)
                feedIcon.sprite = feedOn ? feedIconOpen : feedIconClosed;
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
            FeedbackRoot.SetActive(false);
        }

        public void ToggleMute()
        {
            AudioListener.volume = AudioListener.volume > 0 ? 0 : 1;
            UpdateMuteLabel();
        }

        private void UpdateMuteLabel() => muteLabel.text = Loc.T(AudioListener.volume > 0 ? "Đang bật" : "Đang tắt");

        // The button names the language in use, each in its own language, so either reader can find it.
        public void ToggleLanguage()
        {
            Loc.Toggle();
            UpdateLanguageLabel();
            RefreshWallet();
        }

        private void UpdateLanguageLabel()
        {
            if (languageLabel != null) languageLabel.text = Loc.Current == Language.English ? "English" : "Tiếng Việt";
        }

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
            feedback.text = Loc.T(message);
            feedbackUntil = Time.unscaledTime + 4;
            FeedbackRoot.SetActive(true);
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
