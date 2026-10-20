// Records actual Unity Game View frames outside Assets to avoid importing screenshots.
var d = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if (d == null || !UnityEngine.Application.isPlaying) throw new System.Exception("Play the 3D preview first.");
d.Cancel(); d.screen.ClosePanels();
string folder = "/private/tmp/lumaquarium-preview-frames";
System.IO.Directory.CreateDirectory(folder);
var type = System.AppDomain.CurrentDomain.GetAssemblies().First(x=>x.GetName().Name=="Unity.Pipeline.Editor")
    .GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands");
var capture = type.GetMethod("EncodeScreenToPng",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
int index=0, firstFrame=UnityEngine.Time.frameCount;
double start=UnityEditor.EditorApplication.timeSinceStartup, next=start;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>
{
    if(!UnityEngine.Application.isPlaying || d==null) { UnityEditor.EditorApplication.update-=tick; return; }
    double now=UnityEditor.EditorApplication.timeSinceStartup;
    if(now<next) return;
    next=now+.12;
    if(index%16==0) d.water.Ripple(new UnityEngine.Vector3(.15f,0,0));
    var png=(byte[])capture.Invoke(null,new object[]{450,974});
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,$"frame-{index:000}.png"),png);
    index++;
    if(index<60) return;
    UnityEditor.EditorApplication.update-=tick;
    System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"capture.json"),
        Newtonsoft.Json.JsonConvert.SerializeObject(new {frames=index,firstFrame,lastFrame=UnityEngine.Time.frameCount,seconds=now-start}));
};
UnityEditor.EditorApplication.update+=tick;
return new {recording=true,folder,frames=60};
