using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumaquarium.UI
{
    // Start and guest login in one screen: play right away, or open the (coming soon) account form.
    public sealed class StartGameScreen : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button startButton;
        [SerializeField] private UnityEngine.UI.Button accountButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text feedback;
        private bool transitioning;

        private void Awake()
        {
            startButton.onClick.AddListener(Begin);
            if (accountButton != null) accountButton.onClick.AddListener(OpenAccount);
            bool returning = FrontEndScenes.HasGuestProfile;
            if (startLabel != null) startLabel.text = Loc.T(returning ? "Tiếp tục hồ của bạn" : "Chơi ngay");
            if (subtitle != null) subtitle.text = Loc.T(returning ? "Chào mừng bạn trở lại" : "Hồ cá của riêng bạn");
        }

        private void OnDestroy()
        {
            startButton.onClick.RemoveListener(Begin);
            if (accountButton != null) accountButton.onClick.RemoveListener(OpenAccount);
        }

        public void Begin()
        {
            if (!TryLoad(FrontEndScenes.Loading, "Chưa thể mở hồ cá. Vui lòng thử lại.")) return;
            FrontEndScenes.EnsureGuestProfile();
            SceneManager.LoadSceneAsync(FrontEndScenes.Loading);
        }

        public void OpenAccount()
        {
            if (TryLoad(FrontEndScenes.Login, "Chưa thể mở màn hình tài khoản. Vui lòng thử lại."))
                SceneManager.LoadSceneAsync(FrontEndScenes.Login);
        }

        private bool TryLoad(string scene, string error)
        {
            if (transitioning) return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                feedback.text = Loc.T(error);
                return false;
            }
            transitioning = true;
            startButton.interactable = false;
            if (accountButton != null) accountButton.interactable = false;
            return true;
        }
    }
}
