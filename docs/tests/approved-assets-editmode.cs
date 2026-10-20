// Verifies the installed scene on disk, independently of the preview.
if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Exit Play Mode first.");
string path="Assets/_Project/Scenes/MainAquarium.unity";
var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
bool opened=!scene.IsValid() || !scene.isLoaded;
if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>{if(!ok)throw new System.Exception("FAIL: "+name);checks.Add(name);};
try
{
    var roots=scene.GetRootGameObjects();
    var decorations=roots.SelectMany(x=>x.GetComponentsInChildren<AquariumDecoration>(true)).ToArray();
    check(decorations.Length==1,"One decoration system installed in MainAquarium");
    var d=decorations[0];var lighting=d.GetComponent<AquariumLighting>();
    check(lighting!=null,"Approved day/night lighting installed");
    check(d.storageKey=="Lumaquarium.Decoration.Main.v1","Main layouts isolated from preview");
    check(d.prefabs.Select(x=>x.name).SequenceEqual(new[]{"AquaticPlantNatural","RiverRockNatural","BranchingDriftwoodNatural"}),"Approved natural prefab inventory");
    check(d.itemsRoot.GetComponentsInChildren<AquariumDecorItem>().Length==5,"Five seed decorations retained");
    check(d.screen.decoration==d && d.screen.fish==d.water.fish.GetComponent<BettaFishAI>() && d.screen.feeding.fish==d.screen.fish,"Existing fish feeding and new visuals linked");
    check(d.screen.orbit==d.orbit && d.viewCamera==d.orbit.GetComponent<UnityEngine.Camera>() && lighting.sun.gameObject.scene==scene,"Camera and lighting reference main scene");
    check(UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(d.gameObject)=="Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab","Approved environment installed as reusable asset");
    foreach(var c in roots.SelectMany(x=>x.GetComponentsInChildren<UnityEngine.Component>(true)))
    {
        check(c!=null,"No missing scene scripts");
        var serialized=new UnityEditor.SerializedObject(c);var p=serialized.GetIterator();
        while(p.Next(true))
        {
            if(p.propertyType!=UnityEditor.SerializedPropertyType.ObjectReference)continue;
            var value=p.objectReferenceValue;
            if(value==null || UnityEditor.EditorUtility.IsPersistent(value))continue;
            var go=value as UnityEngine.GameObject ?? (value as UnityEngine.Component)?.gameObject;
            if(go!=null && go.scene!=scene)throw new System.Exception("Cross-scene reference: "+c.name+"."+p.propertyPath);
        }
    }
    checks.RemoveAll(x=>x=="No missing scene scripts");checks.Add("No missing scripts or cross-scene references");
    check(roots.SelectMany(x=>x.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(false)).Count()==1,"Single active EventSystem");
    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab");
    check(prefab!=null && prefab.GetComponentInChildren<AquariumLighting>(true).sun==null,"Shared asset has no scene-specific light binding");
    return new{passed=checks.Count,checks};
}
finally
{
    if(previous.IsValid() && previous.isLoaded)UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
    if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
}
