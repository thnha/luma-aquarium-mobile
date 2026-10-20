// Run in Edit Mode with unity command eval_file --file docs/tests/fish-shop-inventory.cs.
// Isolated key: never touches the player's wallet or owned fish.
var checks = new System.Collections.Generic.List<string>();
System.Action<bool,string> check = (ok,name) => { if (!ok) throw new System.Exception("FAIL: " + name); checks.Add(name); };
var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Lumaquarium.UI.FishShopInventory")).FirstOrDefault(t=>t!=null);
check(type != null, "Fish shop inventory is available");
string key = "Lumaquarium.Test.FishShop." + System.Guid.NewGuid();
System.Func<object> load = () => System.Activator.CreateInstance(type, new object[]{key});
System.Func<object,string,int,bool> buy = (inventory,id,price) => (bool)type.GetMethod("TryPurchase").Invoke(inventory,new object[]{id,price});
System.Func<object,int> balance = inventory => (int)type.GetProperty("Balance").GetValue(inventory);
System.Func<object,string,bool> owns = (inventory,id) => (bool)type.GetMethod("Owns").Invoke(inventory,new object[]{id});
System.Func<object,string,bool> equip = (inventory,id) => (bool)type.GetMethod("SelectOwned").Invoke(inventory,new object[]{id});
System.Func<object,string> active = inventory => (string)type.GetProperty("ActiveId").GetValue(inventory);
try
{
    var inventory=load();
    check(balance(inventory)==1250, "New local wallet starts with 1250 xu");
    check(!buy(inventory,"royal",-10) && !buy(inventory," ",350), "Invalid purchases do not spend xu");
    check(buy(inventory,"pearl",0) && balance(inventory)==1250 && owns(inventory,"pearl") && active(inventory)=="pearl" && !buy(inventory,"pearl",0), "A free fish is granted once without spending xu");
    check(!equip(inventory,"orchid"), "Unowned fish cannot be equipped");
    check(buy(inventory,"royal",350), "Buy first fish");
    check(balance(inventory)==900 && owns(inventory,"royal") && active(inventory)=="royal", "Purchase spends xu, grants and equips fish");
    check(!buy(inventory,"royal",350) && balance(inventory)==900, "Repeated purchase cannot double-charge");
    check(buy(inventory,"koi",650) && balance(inventory)==250, "Buy another fish with the remaining balance");
    check(!buy(inventory,"orchid",900) && balance(inventory)==250 && !owns(inventory,"orchid"), "Insufficient funds cannot grant fish or change balance");
    check(equip(inventory,"royal") && balance(inventory)==250, "Switching an owned fish is free");
    var reloaded=load();
    check(balance(reloaded)==250 && owns(reloaded,"royal") && owns(reloaded,"koi") && owns(reloaded,"pearl") && active(reloaded)=="royal", "Wallet, ownership and equipped fish survive reload");
    UnityEngine.PlayerPrefs.DeleteKey(key);
    var exact=load();
    check(buy(exact,"royal",350) && buy(exact,"orchid",900) && balance(exact)==0,"A purchase can spend exactly the available balance");
    UnityEngine.PlayerPrefs.SetString(key,"not valid json");
    check(balance(load())==1250, "Malformed local data does not crash loading");
    UnityEngine.PlayerPrefs.SetString(key,"{\"version\":1,\"balance\":-20,\"ownedIds\":[],\"activeId\":\"\"}");
    check(balance(load())==1250, "Invalid negative saved balance is rejected");
}
finally { UnityEngine.PlayerPrefs.DeleteKey(key); UnityEngine.PlayerPrefs.Save(); }
return new {passed=checks.Count,checks};
