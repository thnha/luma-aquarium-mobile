using System.Collections;
using Lumaquarium.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Lumaquarium.UI
{
    // Downloads and opens the aquarium. MainAquarium with its fish and decor is remote content (Addressables,
    // see Editor/RemoteContent): the first launch downloads it, later launches fetch only what changed, and
    // with no network the cached copy opens.
    public sealed class GameLoadingScreen : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private GameObject errorActions;
        [SerializeField] private UnityEngine.UI.Button retryButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField, Min(0)] private float minimumVisibleSeconds = 1.2f;
        [SerializeField, Min(0)] private float shopSyncSeconds = 8f;
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
        }

        public void Retry()
        {
            if (!loading) StartCoroutine(LoadAquarium());
        }

        private IEnumerator LoadAquarium()
        {
            loading = true;
            errorActions.SetActive(false);
            SetProgress(0);
            statusLabel.text = "Đang chuẩn bị hồ cá...";
            float startedAt = Time.realtimeSinceStartup;

            // Also picks up a newer remote catalog; offline it keeps the cached one.
            var init = Addressables.InitializeAsync(false);
            yield return init;
            bool initialized = init.Status == AsyncOperationStatus.Succeeded;
            init.Release();
            if (!initialized)
            {
                ShowError();
                yield break;
            }

            var size = Addressables.GetDownloadSizeAsync(FrontEndScenes.Aquarium);
            yield return size;
            long bytes = size.Status == AsyncOperationStatus.Succeeded ? size.Result : -1;
            size.Release();
            if (bytes < 0)
            {
                ShowError();
                yield break;
            }

            // Downloading takes the first 80% of the bar, opening the scene the rest.
            float opening = 0;
            if (bytes > 0)
            {
                statusLabel.text = $"Đang tải nội dung mới ({bytes / 1048576f:0.#} MB)...";
                var download = Addressables.DownloadDependenciesAsync(FrontEndScenes.Aquarium, false);
                while (!download.IsDone)
                {
                    SetProgress(download.GetDownloadStatus().Percent * 0.8f);
                    yield return null;
                }
                bool downloaded = download.Status == AsyncOperationStatus.Succeeded;
                download.Release();
                if (!downloaded)
                {
                    ShowError();
                    yield break;
                }
                opening = 0.8f;
                statusLabel.text = "Đang chuẩn bị hồ cá...";
            }

            // The shop save from the server (and a save from before it, sent once) before the aquarium reads it.
            // Offline, or when it takes too long, the aquarium opens with the save on the device.
            var shop = ShopService.Prepare();
            float syncStarted = Time.realtimeSinceStartup;
            while (!shop.IsCompleted && Time.realtimeSinceStartup - syncStarted < shopSyncSeconds) yield return null;
            if (shop.IsFaulted) Debug.LogException(shop.Exception);

            var scene = Addressables.LoadSceneAsync(FrontEndScenes.Aquarium, LoadSceneMode.Single, false);
            while (!scene.IsDone)
            {
                SetProgress(opening + (1 - opening) * scene.PercentComplete);
                yield return null;
            }
            if (scene.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(scene);
                ShowError();
                yield break;
            }

            SetProgress(1);
            statusLabel.text = "Đang mở hồ cá...";
            while (Time.realtimeSinceStartup - startedAt < minimumVisibleSeconds) yield return null;
            yield return scene.Result.ActivateAsync();
        }

        private void SetProgress(float progress)
        {
            // The fill is a sliced pill resized by its anchor so the rounded ends keep their shape.
            var rect = progressFill.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(progress), rect.anchorMax.y);
            progressFill.enabled = progress > .005f;
            progressLabel.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }

        private void ShowError()
        {
            loading = false;
            statusLabel.text = "Chưa tải được hồ cá. Kiểm tra kết nối mạng rồi thử lại.";
            errorActions.SetActive(true);
        }

        public void Back()
        {
            // Back returns to the merged start screen, where guest play and the account form live.
            if (!loading && Application.CanStreamedLevelBeLoaded(FrontEndScenes.Start))
            {
                loading = true;
                retryButton.interactable = false;
                backButton.interactable = false;
                SceneManager.LoadSceneAsync(FrontEndScenes.Start);
            }
        }
    }
}
