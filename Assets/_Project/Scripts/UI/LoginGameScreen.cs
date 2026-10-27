using Lumaquarium.Account;
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
        [SerializeField] private UnityEngine.UI.Button appleButton;
        [SerializeField] private UnityEngine.UI.Button googleButton;
        [SerializeField] private UnityEngine.UI.Button returnToGuestButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private TMP_InputField password;
        [SerializeField] private TMP_Text feedback;
        private bool transitioning;
        private bool signingIn;

        // The redesigned LoginGame holds Apple / Google sign-in; guest play starts from StartGame.
        // guestPanel/guestButton/accountButton/password stay optional for scenes built before the redesign.
        private void Awake()
        {
            if (guestButton != null) guestButton.onClick.AddListener(PlayAsGuest);
            if (accountButton != null) accountButton.onClick.AddListener(ShowAccount);
            if (appleButton != null) appleButton.onClick.AddListener(SignInWithApple);
            if (googleButton != null) googleButton.onClick.AddListener(SignInWithGoogle);
            returnToGuestButton.onClick.AddListener(guestPanel != null ? (UnityEngine.Events.UnityAction)ShowGuest : PlayAsGuest);
            backButton.onClick.AddListener(Back);
            ShowProviders();
            if (guestPanel != null) ShowGuest();
            else ShowAccount();
        }

        private void Update() => AccountSignIn.Update();

        private void OnDestroy()
        {
            if (guestButton != null) guestButton.onClick.RemoveAllListeners();
            if (accountButton != null) accountButton.onClick.RemoveAllListeners();
            if (appleButton != null) appleButton.onClick.RemoveAllListeners();
            if (googleButton != null) googleButton.onClick.RemoveAllListeners();
            returnToGuestButton.onClick.RemoveAllListeners();
            backButton.onClick.RemoveListener(Back);
        }

        // Apple only exists on iOS; when it is hidden, every row below it moves up by one slot and the panel
        // shrinks by the same amount, so no gap is left where the button was.
        private void ShowProviders()
        {
            if (appleButton == null || googleButton == null) return;
            if (AccountSignIn.AppleSupported) return;
            var apple = (RectTransform)appleButton.transform;
            float slot = apple.anchoredPosition.y - ((RectTransform)googleButton.transform).anchoredPosition.y;
            appleButton.gameObject.SetActive(false);
            var panel = apple.parent as RectTransform;
            if (panel == null || slot <= 0) return;
            foreach (RectTransform row in panel)
                if (row != apple && row.anchoredPosition.y < apple.anchoredPosition.y) row.anchoredPosition += new Vector2(0, slot);
            panel.sizeDelta -= new Vector2(0, slot);
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
            if (password != null) password.text = string.Empty;
            if (guestPanel == null) return;
            accountPanel.SetActive(false);
            guestPanel.SetActive(true);
        }

        public void SignInWithApple() => SignIn(true);
        public void SignInWithGoogle() => SignIn(false);

        private async void SignIn(bool withApple)
        {
            if (transitioning || signingIn) return;
            signingIn = true;
            SetInteractable(false);
            feedback.text = Loc.T("Đang đăng nhập…");
            var result = withApple ? await AccountSignIn.SignInWithApple() : await AccountSignIn.SignInWithGoogle();
            signingIn = false;
            if (this == null) return;
            switch (result)
            {
                case SignInResult.Success:
                    FrontEndScenes.EnsureGuestProfile();
                    feedback.text = string.Empty;
                    OpenAquarium();
                    return;
                case SignInResult.Canceled: feedback.text = string.Empty; break;
                case SignInResult.Offline: feedback.text = Loc.T("Không có mạng. Vui lòng thử lại."); break;
                case SignInResult.Unavailable: feedback.text = Loc.T("Cách đăng nhập này chưa dùng được trên máy này."); break;
                default: feedback.text = Loc.T("Đăng nhập không thành công. Vui lòng thử lại."); break;
            }
            SetInteractable(true);
        }

        public void PlayAsGuest()
        {
            if (transitioning || signingIn) return;
            FrontEndScenes.EnsureGuestProfile();
            OpenAquarium();
        }

        private void OpenAquarium()
        {
            if (!Application.CanStreamedLevelBeLoaded(FrontEndScenes.Loading))
            {
                feedback.text = Loc.T("Chưa thể mở hồ cá. Vui lòng thử lại.");
                SetInteractable(true);
                return;
            }

            if (password != null) password.text = string.Empty;
            transitioning = true;
            SetInteractable(false);
            SceneManager.LoadSceneAsync(FrontEndScenes.Loading);
        }

        private void SetInteractable(bool value)
        {
            if (guestButton != null) guestButton.interactable = value;
            if (accountButton != null) accountButton.interactable = value;
            if (appleButton != null) appleButton.interactable = value;
            if (googleButton != null) googleButton.interactable = value;
            returnToGuestButton.interactable = value;
            backButton.interactable = value;
        }

        public void Back()
        {
            if (transitioning || signingIn || !Application.CanStreamedLevelBeLoaded(FrontEndScenes.Start)) return;
            transitioning = true;
            if (password != null) password.text = string.Empty;
            SceneManager.LoadSceneAsync(FrontEndScenes.Start);
        }
    }
}
