// Checks persistent HUD callbacks after installing the shared environment in MainAquarium.
if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play MainAquarium first.");
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainAquarium","Main game scene running");
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"Existing fish reused without duplication");
try
{
    // Trang trí is an inventory sheet: Mua thêm opens the shop on the selected group.
    var inventory=d.screen.decoratePanel.GetComponent<Lumaquarium.UI.DecorInventoryPanel>();
    check(inventory!=null,"Decorate sheet lists the inventory");
    d.screen.ToggleDecorate();
    for(int i=0;i<3;i++)
    {
        inventory.tabButtons[i].onClick.Invoke();
        inventory.shopButton.onClick.Invoke();
        var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>();
        check(shop.IsOpen && !d.Editing && shop.catalog.offers[shop.SelectedIndex].category==(AquariumDecorCategory)i,"Inventory tab opens shop category "+i);
        shop.Close();
        d.screen.ToggleDecorate();
    }
    int pellets=UnityEngine.Object.FindObjectsByType<FoodPellet>(UnityEngine.FindObjectsSortMode.None).Length;
    d.screen.toolbar.transform.Find("FeedButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    check(UnityEngine.Object.FindObjectsByType<FoodPellet>(UnityEngine.FindObjectsSortMode.None).Length==pellets+1,"Approved HUD feeds existing fish system");
}
finally {UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>()?.Close();d.Cancel();d.screen.ClosePanels();}
return new{passed=checks.Count,checks};
