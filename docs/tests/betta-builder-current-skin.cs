// Re-running the material builder must keep the approved soft skin settings.
// Restore persistent assets even on failure, so a failing test cannot regress the preview.
if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Run outside Play Mode");
var skin=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Betta/Betta.mat");
var membrane=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Resources/BettaMembrane.mat");
var savedSkin=UnityEngine.Object.Instantiate(skin);savedSkin.name=skin.name;
var savedMembrane=UnityEngine.Object.Instantiate(membrane);savedMembrane.name=membrane.name;
try{
 var mask=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(BettaMaskBaker.MaskPath);
 var build=typeof(BettaPrefabBuilder).GetMethod("FishMaterial",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
 int checks=0;
 for(int pass=0;pass<2;pass++){
  var result=(UnityEngine.Material)build.Invoke(null,new object[]{mask});
  if(result!=skin)throw new Exception("Material asset reference changed");checks++;
  if(Mathf.Abs(result.GetFloat("_BumpScale")-.065f)>.0001f)throw new Exception("Builder restores obsolete normal strength: "+result.GetFloat("_BumpScale"));checks++;
  if(Mathf.Abs(result.GetFloat("_OcclusionStrength")-.18f)>.0001f)throw new Exception("Builder restores obsolete occlusion: "+result.GetFloat("_OcclusionStrength"));checks++;
  if(!result.GetTexture("_ScaleMap")||UnityEditor.AssetDatabase.GetAssetPath(result.GetTexture("_ScaleMap"))!=BettaScaleBaker.MapPath)throw new Exception("Builder drops the ordered scale atlas");checks++;
  if(membrane.GetFloat("_ZWrite")!=0||!membrane.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))throw new Exception("Membrane no longer blends");checks++;
 }
 return new{passed=checks,passes=2};
}finally{
 UnityEditor.EditorUtility.CopySerialized(savedSkin,skin);UnityEditor.EditorUtility.CopySerialized(savedMembrane,membrane);
 UnityEditor.AssetDatabase.SaveAssetIfDirty(skin);UnityEditor.AssetDatabase.SaveAssetIfDirty(membrane);
 UnityEngine.Object.DestroyImmediate(savedSkin);UnityEngine.Object.DestroyImmediate(savedMembrane);
}
