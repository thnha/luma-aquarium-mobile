var lighting=UnityEngine.Object.FindFirstObjectByType<AquariumLighting>();
var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if(lighting==null || !UnityEngine.Application.isPlaying)throw new System.Exception("Play Aquarium3DPreview first.");
d.Cancel();d.screen.ClosePanels();lighting.ApplyImmediate(false);
string folder="/private/tmp/lumaquarium-lighting-frames";System.IO.Directory.CreateDirectory(folder);
var capture=System.AppDomain.CurrentDomain.GetAssemblies().First(x=>x.GetName().Name=="Unity.Pipeline.Editor")
    .GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")
    .GetMethod("EncodeScreenToPng",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
int index=0,firstFrame=UnityEngine.Time.frameCount;double start=UnityEditor.EditorApplication.timeSinceStartup,next=start;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>
{
    if(!UnityEngine.Application.isPlaying || lighting==null){UnityEditor.EditorApplication.update-=tick;return;}
    double now=UnityEditor.EditorApplication.timeSinceStartup;if(now<next)return;next=now+.12;
    if(index==12)lighting.Toggle();
    if(index==58)lighting.Toggle();
    if(index%20==0)d.water.Ripple(new UnityEngine.Vector3(.15f,0,0));
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,$"frame-{index:000}.png"),(byte[])capture.Invoke(null,new object[]{450,974}));
    index++;
    if(index<90)return;
    UnityEditor.EditorApplication.update-=tick;lighting.ApplyImmediate(false);
    System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"capture.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{frames=index,firstFrame,lastFrame=UnityEngine.Time.frameCount,seconds=now-start}));
};
UnityEditor.EditorApplication.update+=tick;
return new{recording=true,folder,frames=90};
