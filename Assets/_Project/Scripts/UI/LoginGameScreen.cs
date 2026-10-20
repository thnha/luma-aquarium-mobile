using System;
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

        private void Awake()
        {
            guestButton.onClick.AddListener(PlayAsGuest);
            accountButton.onClick.AddListener(ShowAccount);
            returnToGuestButton.onClick.AddListener(ShowGuest);
            backButton.onClick.AddListener(Back);
            ShowGuest();
        }

        private void OnDestroy()
        {
            guestButton.onClick.RemoveListener(PlayAsGuest);
            accountButton.onClick.RemoveListener(ShowAccount);
            returnToGuestButton.onClick.RemoveListener(ShowGuest);
            backButton.onClick.RemoveListener(Back);
        }

        public void ShowAccount()
        {
            if (transitioning) return;
            guestPanel.SetActive(false);
            accountPanel.SetActive(true);
        }

        public void ShowGuest()
        {
            if (transitioning) return;
            password.text = string.Empty;
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

            // This ID is a local guest profile, not an authenticated account.
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(FrontEndScenes.GuestIdKey)))
            {
                PlayerPrefs.SetString(FrontEndScenes.GuestIdKey, Guid.NewGuid().ToString("N"));
                PlayerPrefs.Save();
            }

            transitioning = true;
            guestButton.interactable = false;
            accountButton.interactable = false;
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
