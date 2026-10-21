// Checks persistent HUD callbacks after installing the shared environment in MainAquarium.
if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play MainAquarium first.");
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainAquarium","Main game scene running");
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
check(UnityEngine.Object.FindObjectsByType<BettaFishAI>(UnityEngine.FindObjectsSortMode.None).Length==1,"Existing fish reused without duplication");
try
{
    for(int i=0;i<3;i++)
    {
        d.screen.decoratePanel.transform.Find("Inventory"+i).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        var shop=UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>();
        if(shop!=null)
        {
            var expected=i==0?AquariumDecorCategory.Plants:i==1?AquariumDecorCategory.Rocks:AquariumDecorCategory.Wood;
            check(shop.IsOpen && !d.Editing && shop.catalog.offers[shop.SelectedIndex].category==expected,"Inventory callback opens shop category "+i);
            shop.Close();
        }
        else { check(d.Editing && d.Selected!=null && d.Selected.kind==i,"Inventory callback selects installed kind "+i); d.Cancel(); }
    }
    int pellets=UnityEngine.Object.FindObjectsByType<FoodPellet>(UnityEngine.FindObjectsSortMode.None).Length;
    d.screen.toolbar.transform.Find("FeedButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    check(UnityEngine.Object.FindObjectsByType<FoodPellet>(UnityEngine.FindObjectsSortMode.None).Length==pellets+1,"Approved HUD feeds existing fish system");
}
finally {UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.DecorShopScreen>()?.Close();d.Cancel();d.screen.ClosePanels();}
return new{passed=checks.Count,checks};
