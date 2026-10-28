using System;
using System.Threading.Tasks;
using Lumaquarium.Account;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Lumaquarium.UI
{
    public sealed class DeleteAccountDialog : MonoBehaviour
    {
        public const string ResourcePath = "UI/DeleteAccountDialog";
        public TMP_Text title, description, warning, confirmLabel, cancelLabel;
        public UnityEngine.UI.Button confirmButton, cancelButton;
        public GameObject warningRoot;
        public Func<Task<DeleteAccountResult>> Delete = AccountSignIn.DeleteAccount;
        public bool Busy { get; private set; }
        public bool Succeeded { get; private set; }
        private static DeleteAccountDialog instance;
        private float previousTimeScale;
        private GameObject previousSelection;
        private bool error;

        public static DeleteAccountDialog Show()
        {
            if (instance != null) return instance;
            var prefab = Resources.Load<DeleteAccountDialog>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[Account] Missing delete-account dialog.");
                return null;
            }
            return Instantiate(prefab);
        }

        private void Awake()
        {
            instance = this;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Cancel);
            Loc.Changed += Render;
            error = AccountSignIn.DeletionPending;
            Render();
            SelectSafeAction();
        }

        private void Update() => AccountSignIn.Update(); // Apple native callbacks must also run during reauthentication.

        private void SelectSafeAction()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(cancelButton.gameObject.activeSelf ? cancelButton.gameObject : confirmButton.gameObject);
        }

        public async void Confirm()
        {
            if (Busy) return;
            if (Succeeded)
            {
                Destroy(gameObject);
                SceneManager.LoadSceneAsync(FrontEndScenes.Start);
                return;
            }
            Busy = true;
            error = false;
            Render();
            DeleteAccountResult result;
            try { result = await Delete(); }
            catch (Exception) { result = DeleteAccountResult.Failed; }
            if (this == null) return;
            Busy = false;
            Succeeded = result == DeleteAccountResult.Success;
            error = result != DeleteAccountResult.Canceled && !Succeeded;
            Render();
            if (error)
            {
                description.text = Loc.T(result switch
                {
                    DeleteAccountResult.Offline => "Cần kết nối mạng để xóa tài khoản. Vui lòng kết nối và thử lại.",
                    DeleteAccountResult.WrongAccount => "Vui lòng xác thực bằng đúng tài khoản Apple hoặc Google đã đăng nhập.",
                    DeleteAccountResult.Unavailable => "Chưa thể xóa tài khoản trên thiết bị này. Vui lòng thử lại trên thiết bị đã đăng nhập.",
                    _ => "Không thể hoàn tất việc xóa. Vui lòng kiểm tra kết nối và thử lại."
                });
            }
            SelectSafeAction();
        }

        public void Cancel()
        {
            if (Busy || AccountSignIn.DeletionPending || Succeeded) return;
            Destroy(gameObject);
        }

        private void Render()
        {
            title.text = Loc.T(Busy ? "Đang xóa tài khoản…" : Succeeded ? "Đã xóa tài khoản" : error ? "Chưa xóa xong" : "Xóa tài khoản?");
            description.text = Loc.T(Busy ? "Vui lòng chờ trong khi xóa dữ liệu trên máy và cloud."
                : Succeeded ? "Tài khoản và toàn bộ tiến trình trên thiết bị này và cloud đã được xóa."
                : error ? "Yêu cầu xóa chưa hoàn tất. Vui lòng thử lại để hoàn tất việc xóa."
                : "Tài khoản và toàn bộ tiến trình của bạn sẽ bị xóa vĩnh viễn:\n\n• Hồ cá, cá và đồ trang trí\n• Xu và vật phẩm đã mua\n• Dữ liệu trên thiết bị này và cloud");
            warningRoot.SetActive(!Busy && !Succeeded);
            warning.text = Loc.T(error && AccountSignIn.DeletionPending
                ? "Việc xóa đã bắt đầu. Cần hoàn tất trước khi chơi tiếp." : "Không thể khôi phục sau khi xóa.");
            cancelButton.gameObject.SetActive(!Busy && !Succeeded && !AccountSignIn.DeletionPending);
            cancelLabel.text = Loc.T(error ? "Để sau" : "Giữ tài khoản");
            confirmButton.gameObject.SetActive(!Busy);
            confirmButton.interactable = !Busy;
            confirmLabel.text = Loc.T(Succeeded ? "Về màn hình bắt đầu" : error ? "Thử lại" : "Xóa vĩnh viễn");
        }

        private void OnDestroy()
        {
            Loc.Changed -= Render;
            Time.timeScale = previousTimeScale;
            if (instance == this) instance = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
        }
    }
}
