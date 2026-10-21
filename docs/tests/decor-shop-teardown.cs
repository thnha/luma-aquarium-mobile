// The HUD can be destroyed before the open shop when Unity unloads the scene.
var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>();
if(shop==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Play MainAquarium first.");
var aquarium=shop.aquarium;var hud=aquarium.hud;
float alpha=hud.alpha;bool interactable=hud.interactable,raycasts=hud.blocksRaycasts,orbit=aquarium.orbit.enabled;
UnityEngine.GameObject temporary=null;
try
{
    shop.Open();
    temporary=new UnityEngine.GameObject("TemporaryDestroyedHud",typeof(UnityEngine.CanvasGroup));
    aquarium.hud=temporary.GetComponent<UnityEngine.CanvasGroup>();
    UnityEngine.Object.DestroyImmediate(temporary);temporary=null;
    shop.Close();
    if(shop.IsOpen)throw new System.Exception("FAIL: shop must close when the HUD has already been destroyed");
}
finally
{
    aquarium.hud=hud;shop.Close();hud.alpha=alpha;hud.interactable=interactable;hud.blocksRaycasts=raycasts;aquarium.orbit.enabled=orbit;
    if(temporary!=null)UnityEngine.Object.DestroyImmediate(temporary);
}
return new{passed=1,checks=new[]{"Closing shop tolerates HUD destroyed during scene unload"}};
