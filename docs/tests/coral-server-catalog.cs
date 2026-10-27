var decor=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(AquariumDecorCatalogBuilder.Root+"/AquariumDecorCatalog.asset");
var server=UnityEngine.JsonUtility.FromJson<Lumaquarium.Shop.ShopCatalog>(System.IO.File.ReadAllText(ServerCatalogWriter.FullPath));
int passed=0;
foreach(var species in CoralCollectionBuilder.All)
{
 var local=decor.offers.Single(o=>o.id==species.id);
 if(!server.items.Any(i=>i.id==local.id && i.price==local.price))throw new System.Exception("Server must sell the coral at the displayed price: "+local.id);
 passed++;
}
if(server.version!=decor.serverCatalogVersion)throw new System.Exception("Client/server catalog versions differ");
return new{passed=passed+1,version=server.version};
