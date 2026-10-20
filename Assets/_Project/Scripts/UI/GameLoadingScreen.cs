using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumaquarium.UI
{
    public sealed class GameLoadingScreen : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private GameObject errorActions;
        [SerializeField] private UnityEngine.UI.Button retryButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField, Min(0)] private float minimumVisibleSeconds = 1.2f;
        private AsyncOperation operation;
        private bool loading;

        private void Start()
        {
            retryButton.onClick.AddListener(Retry);
            backButton.onClick.AddListener(Back);
            Retry();
        }

        private void OnDestroy()
        {
            retryButton.onClick.RemoveListener(Retry);
            backButton.onClick.RemoveListener(Back);
            // A pending operation must not block Unity's async scene queue.
            if (operation != null && !operation.isDone) operation.allowSceneActivation = true;
        }

        public void Retry()
        {
            if (!loading) StartCoroutine(LoadAquarium());
        }

        private IEnumerator LoadAquarium()
        {
            errorActions.SetActive(false);
            progressFill.fillAmount = 0;
            progressLabel.text = "0%";
            statusLabel.text = "Đang chuẩn bị hồ cá...";
            if (!Application.CanStreamedLevelBeLoaded(FrontEndScenes.Aquarium))
            {
                ShowError();
                yield break;
            }

            loading = true;
            Exception loadError = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(FrontEndScenes.Aquarium);
                if (operation != null) operation.allowSceneActivation = false;
            }
            catch (Exception exception) { loadError = exception; }

            if (loadError != null || operation == null)
            {
                ShowError();
                yield break;
            }

            float startedAt = Time.realtimeSinceStartup;
            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                progressFill.fillAmount = progress;
                progressLabel.text = $"{Mathf.RoundToInt(progress * 100)}%";
                if (operation.progress >= 0.9f)
                {
                    statusLabel.text = "Đang mở hồ cá...";
                    if (Time.realtimeSinceStartup - startedAt >= minimumVisibleSeconds)
                        operation.allowSceneActivation = true;
                }
                yield return null;
            }
        }

        private void ShowError()
        {
            loading = false;
            operation = null;
            statusLabel.text = "Chưa tải được hồ cá. Bạn có thể thử lại.";
            errorActions.SetActive(true);
        }

        public void Back()
        {
            if (!loading && Application.CanStreamedLevelBeLoaded(FrontEndScenes.Login))
            {
                loading = true;
                retryButton.interactable = false;
                backButton.interactable = false;
                SceneManager.LoadSceneAsync(FrontEndScenes.Login);
            }
        }
    }
}
