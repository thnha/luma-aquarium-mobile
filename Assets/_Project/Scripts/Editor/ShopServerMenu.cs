using Lumaquarium.Shop;
using UnityEditor;

// Play in the Editor against the deployed Cloud Code module instead of the local rules (a build always uses the server).
public static class ShopServerMenu
{
    private const string Item = "Lumaquarium/Shop/Use Server in Editor";

    [MenuItem(Item)]
    private static void Toggle() => EditorPrefs.SetBool(ShopService.EditorServerKey, !EditorPrefs.GetBool(ShopService.EditorServerKey, false));

    [MenuItem(Item, true)]
    private static bool CanToggle()
    {
        Menu.SetChecked(Item, EditorPrefs.GetBool(ShopService.EditorServerKey, false));
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
