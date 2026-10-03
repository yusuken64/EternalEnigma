#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    public sealed class BiomeDecorationVisibilityTests
    {
        [UnityTest]
        public IEnumerator AnimationBudgetVisibilityAndCacheActivationAreBounded()
        {
            var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.CompareTag("MainCamera")).ToArray();
            foreach(var c in cameras)c.tag="Untagged";
            var cameraObject=new GameObject("Decoration test camera");var camera=cameraObject.AddComponent<Camera>();camera.tag="MainCamera";
            camera.orthographic=true;camera.orthographicSize=10;camera.transform.position=new Vector3(3,3,-10);
            var root=new GameObject("Cached decorations");var catalog=BiomeDecorationCatalog.Load();
            try {
                for(int i=0;i<40;i++){var effect=Object.Instantiate(catalog.Assets.First(a=>a.Effect!=null).Effect,root.transform);effect.transform.localPosition=new Vector3(i%8,i/8,0);effect.GetComponent<BiomeDecorationEffect>().SetScale(Vector3.one*.2f);}
                yield return null;yield return null;
                var effects=root.GetComponentsInChildren<BiomeDecorationEffect>();
                Assert.That(effects.Count(e=>e.GetComponent<Renderer>().enabled),Is.EqualTo(32));
                foreach(var e in effects)e.FloorVisible=false;
                yield return null;yield return null;Assert.That(effects.All(e=>!e.GetComponent<Renderer>().enabled),Is.True);
                root.SetActive(false);yield return null;root.SetActive(true);foreach(var e in effects)e.FloorVisible=true;
                yield return null;yield return null;Assert.That(effects.Count(e=>e.GetComponent<Renderer>().enabled),Is.EqualTo(32));
                Assert.That(effects.All(e=>e.transform.localScale.x>=.167f&&e.transform.localScale.x<=.201f),Is.True,"Cache activation must retain the authored glow scale");
                camera.transform.position=new Vector3(1000,1000,-10);yield return null;yield return null;
                Assert.That(effects.All(e=>!e.GetComponent<Renderer>().enabled),Is.True);
            }
            finally {Object.Destroy(root);Object.Destroy(cameraObject);foreach(var c in cameras)if(c!=null)c.tag="MainCamera";}
            yield return null;
        }
    }
}
#endif
