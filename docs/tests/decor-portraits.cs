// A rebuilt mesh must render current vertices and leave a transparent margin around every portrait.
var catalog=UnityEditor.AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>("Assets/_Project/Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset");
int passed=0;
foreach(var offer in catalog.offers)
{
 string path=UnityEditor.AssetDatabase.GetAssetPath(offer.portrait.texture);
 var image=new UnityEngine.Texture2D(2,2);
 try
 {
  image.LoadImage(System.IO.File.ReadAllBytes(path));var pixels=image.GetPixels32();int visible=0;bool clipped=false;
  for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
  {
   if(pixels[y*image.width+x].a<25)continue;visible++;
   if(x<2||y<2||x>=image.width-2||y>=image.height-2)clipped=true;
  }
  if(visible<100)throw new System.Exception("FAIL: blank portrait "+offer.id);passed++;
  if(clipped)throw new System.Exception("FAIL: portrait cuts off mesh "+offer.id);passed++;
 }
 finally{UnityEngine.Object.DestroyImmediate(image);}
}
return new{passed,checks=new[]{"Every portrait renders visible geometry with a transparent margin"}};
