using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumaquarium.UI
{
    public sealed class StartGameScreen : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button startButton;
        [SerializeField] private TMP_Text feedback;
        private bool transitioning;

        private void Awake() => startButton.onClick.AddListener(Begin);
        private void OnDestroy() => startButton.onClick.RemoveListener(Begin);

        public void Begin()
        {
            if (transitioning) return;
            if (!Application.CanStreamedLevelBeLoaded(FrontEndScenes.Login))
            {
                feedback.text = "Chưa thể mở màn hình tiếp theo. Vui lòng thử lại.";
                return;
            }

            transitioning = true;
            startButton.interactable = false;
            SceneManager.LoadSceneAsync(FrontEndScenes.Login);
        }
    }
}
