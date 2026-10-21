// Edit Mode: protects repeat purchases, shared wallet, stock and fish save compatibility.
var type = typeof(Lumaquarium.UI.FishShopInventory);
var purchase = type.GetMethod("TryPurchaseDecor");
if (purchase == null) throw new System.Exception("FAIL: decoration purchases must add one item and allow repeat purchases");
string key = "Lumaquarium.Test.Decor." + System.Guid.NewGuid();
var results = new System.Collections.Generic.List<string>();
System.Action<bool,string> check = (ok, label) => { if (!ok) throw new System.Exception("FAIL: " + label); results.Add(label); };
try
{
    var wallet = new Lumaquarium.UI.FishShopInventory(key);
    System.Func<string,int,bool> buy = (id, price) => (bool)purchase.Invoke(wallet, new object[] { id, price });
    var count = type.GetMethod("DecorCount");
    System.Func<string,int> stock = id => (int)count.Invoke(wallet, new object[] { id });
    check(wallet.TryPurchase("royal",350), "Buy fish with original wallet");
    check(buy("java-moss",80) && buy("java-moss",80), "Repeat purchase of same decoration");
    check(stock("java-moss")==2 && wallet.Balance==740, "Each purchase adds exactly one and charges once");
    check(wallet.ActiveId=="royal" && wallet.Owns("royal"), "Decor purchase preserves active fish");
    check(!buy("stone",741) && !buy("stone",-1) && !buy("",10), "Reject invalid or unaffordable purchase");
    check(stock("stone")==0 && wallet.Balance==740, "Rejected purchase changes nothing");
    var another = new Lumaquarium.UI.FishShopInventory(key);
    check(another.TryPurchase("mustard",450), "Another wallet instance buys fish");
    check(buy("java-moss",80) && wallet.Balance==210, "Reload latest shared balance before writing");
    check(wallet.Owns("mustard") && stock("java-moss")==3, "Concurrent screens preserve fish and stock");
    wallet.ImportStarterDecor(new System.Collections.Generic.Dictionary<string,int> { { "starter-wood", 2 } });
    check(wallet.DecorCount("starter-wood")==2 && wallet.Balance==210,"Existing starter layout becomes owned stock without charge");
    wallet.ImportStarterDecor(new System.Collections.Generic.Dictionary<string,int> { { "starter-wood", 99 } });
    check(wallet.DecorCount("starter-wood")==2,"Starter migration cannot grant stock twice");
    var loaded = new Lumaquarium.UI.FishShopInventory(key);
    loaded.ImportStarterDecor(new System.Collections.Generic.Dictionary<string,int> { { "starter-wood", 99 } });
    check(loaded.DecorCount("starter-wood")==2,"Starter migration marker survives reload");
    check(loaded.Balance==210 && (int)count.Invoke(loaded,new object[]{"java-moss"})==3, "Reload purchased stock");
}
finally { UnityEngine.PlayerPrefs.DeleteKey(key); }
return new {passed=results.Count,checks=results};
