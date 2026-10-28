using System.IO;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class DeleteAccountDialogBuilder
{
    public const string PrefabPath = "Assets/_Project/Resources/UI/DeleteAccountDialog.prefab";

    [MenuItem("Lumaquarium/UI/Build Delete Account Dialog")]
    public static void Build()
    {
        Ui3D.LoadFonts();
        var root = Ui3D.Canvas("DeleteAccountDialog", 30000);
        // Keep Awake from running while authoring the prefab.
        root.gameObject.SetActive(false);
        var controller = root.gameObject.AddComponent<DeleteAccountDialog>();
        var backdrop = Ui3D.Solid("Backdrop", root, new Color(.06f, .10f, .06f, .76f));
        Ui3D.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        var safe = Ui3D.Rect("SafeArea", root);
        Ui3D.Stretch(safe);
        safe.gameObject.AddComponent<SafeArea>();
        var panel = Ui3D.Image("Panel", safe, "ui3d_panel");
        Ui3D.At(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(920, 1190));
        var mark = Ui3D.Text("WarningMark", panel.transform, "!", 64, Ui3D.Hex("A73F36"), true);
        Ui3D.TopStrip(mark.rectTransform, 60, 60, 44, 84);
        controller.title = Ui3D.Text("Title", panel.transform, "Xóa tài khoản?", 60, Ui3D.Ink, true);
        Ui3D.TopStrip(controller.title.rectTransform, 64, 64, 148, 100);
        controller.description = Ui3D.Text("Description", panel.transform,
            "Tài khoản và toàn bộ tiến trình của bạn sẽ bị xóa vĩnh viễn:\n\n• Hồ cá, cá và đồ trang trí\n• Xu và vật phẩm đã mua\n• Dữ liệu trên thiết bị này và cloud", 42, Ui3D.Ink, false, TextAlignmentOptions.TopLeft);
        controller.description.enableAutoSizing = false;
        controller.description.textWrappingMode = TextWrappingModes.Normal;
        Ui3D.TopStrip(controller.description.rectTransform, 72, 72, 280, 370);
        var warning = Ui3D.Image("Warning", panel.transform, "ui3d_tile");
        warning.color = Ui3D.Hex("F0D8C8");
        Ui3D.TopStrip(warning.rectTransform, 64, 64, 676, 142);
        controller.warningRoot = warning.gameObject;
        controller.warning = Ui3D.Text("Label", warning.transform, "Không thể khôi phục sau khi xóa.", 40, Ui3D.Hex("94392F"), true);
        Ui3D.Stretch(controller.warning.rectTransform, 18);
        controller.cancelButton = Ui3D.Button("Cancel", panel.transform, "Giữ tài khoản", Ui3D.Style.Primary, 46);
        Ui3D.TopStrip((RectTransform)controller.cancelButton.transform, 64, 64, 866, 124);
        controller.cancelLabel = controller.cancelButton.GetComponentInChildren<TMP_Text>();
        controller.confirmButton = Ui3D.Button("Confirm", panel.transform, "Xóa vĩnh viễn", Ui3D.Style.Secondary, 44);
        Ui3D.TopStrip((RectTransform)controller.confirmButton.transform, 64, 64, 1018, 124);
        StyleDanger(controller.confirmButton, true);
        controller.confirmLabel = controller.confirmButton.GetComponentInChildren<TMP_Text>();
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        // No ExecuteAlways behaviour: safe to save the active prefab, then remove its authoring instance.
        root.gameObject.SetActive(true);
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        Object.DestroyImmediate(root.gameObject);
        AssetDatabase.SaveAssets();
    }

    public static void StyleDanger(UnityEngine.UI.Button button, bool filled)
    {
        var image = (UnityEngine.UI.Image)button.targetGraphic;
        image.color = Ui3D.Hex(filled ? "C26555" : "F6DFD2");
        var label = button.GetComponentInChildren<TMP_Text>();
        label.color = filled ? Ui3D.Cream : Ui3D.Hex("A73F36");
        button.GetComponent<Ui3DButtonLabel>().normal = label.color;
    }
}
