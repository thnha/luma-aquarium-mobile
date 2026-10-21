using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumaquarium.UI
{
    public sealed class LoginGameScreen : MonoBehaviour
    {
        [SerializeField] private GameObject guestPanel;
        [SerializeField] private GameObject accountPanel;
        [SerializeField] private UnityEngine.UI.Button guestButton;
        [SerializeField] private UnityEngine.UI.Button accountButton;
        [SerializeField] private UnityEngine.UI.Button returnToGuestButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private TMP_InputField password;
        [SerializeField] private TMP_Text feedback;
        private bool transitioning;

        // The redesigned LoginGame only holds the account form; guest play starts from StartGame.
        // guestPanel/guestButton/accountButton stay optional for scenes built before the redesign.
        private void Awake()
        {
            if (guestButton != null) guestButton.onClick.AddListener(PlayAsGuest);
            if (accountButton != null) accountButton.onClick.AddListener(ShowAccount);
            returnToGuestButton.onClick.AddListener(guestPanel != null ? (UnityEngine.Events.UnityAction)ShowGuest : PlayAsGuest);
            backButton.onClick.AddListener(Back);
            if (guestPanel != null) ShowGuest();
            else ShowAccount();
        }

        private void OnDestroy()
        {
            if (guestButton != null) guestButton.onClick.RemoveAllListeners();
            if (accountButton != null) accountButton.onClick.RemoveAllListeners();
            returnToGuestButton.onClick.RemoveAllListeners();
            backButton.onClick.RemoveListener(Back);
        }

        public void ShowAccount()
        {
            if (transitioning) return;
            if (guestPanel != null) guestPanel.SetActive(false);
            accountPanel.SetActive(true);
        }

        public void ShowGuest()
        {
            if (transitioning) return;
            password.text = string.Empty;
            if (guestPanel == null) return;
            accountPanel.SetActive(false);
            guestPanel.SetActive(true);
        }

        public void PlayAsGuest()
        {
            if (transitioning) return;
            if (!Application.CanStreamedLevelBeLoaded(FrontEndScenes.Loading))
            {
                feedback.text = "Chưa thể mở hồ cá. Vui lòng thử lại.";
                return;
            }

            FrontEndScenes.EnsureGuestProfile();
            password.text = string.Empty;
            transitioning = true;
            if (guestButton != null) guestButton.interactable = false;
            if (accountButton != null) accountButton.interactable = false;
            returnToGuestButton.interactable = false;
            backButton.interactable = false;
            SceneManager.LoadSceneAsync(FrontEndScenes.Loading);
        }

        public void Back()
        {
            if (transitioning || !Application.CanStreamedLevelBeLoaded(FrontEndScenes.Start)) return;
            transitioning = true;
            password.text = string.Empty;
            SceneManager.LoadSceneAsync(FrontEndScenes.Start);
        }
    }
}
