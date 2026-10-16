#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class SilhouetteRenderingTests
    {
        private const int VisibleLayer = 29;
        private const int ExcludedLayer = 28;
        private const int Resolution = 256;
        private readonly List<Object> owned = new();
        private Camera camera;
        private SilhouetteRenderer shadows;
        private GameObject ground, caster;
        private Material white;
        private RenderTexture target, resolved;
        private Texture2D readback;

        [SetUp]
        public void Setup()
        {
            camera = Own(new GameObject("Silhouette regression camera", typeof(Camera))).GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 4;
            camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << VisibleLayer;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.renderingPath = RenderingPath.Forward;
            SetTarget(1);
            resolved = Own(new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGB32));
            readback = Own(new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false));
            white = Own(new Material(Shader.Find("Unlit/Color")) { color = Color.white });

            ground = Primitive("Visible ground", PrimitiveType.Quad, Vector3.zero, Vector3.one * 12,
                VisibleLayer, SilhouetteRole.Receiver);
            caster = Primitive("Asymmetric caster", PrimitiveType.Cube, new Vector3(-.7f, 1.2f, -1.2f),
                Vector3.one * .7f, VisibleLayer, SilhouetteRole.Caster);
            shadows = camera.gameObject.AddComponent<SilhouetteRenderer>();
            shadows.Direction = new Vector3(.6f, 0, .8f);
            Assert.That(shadows.enabled, Is.True, "The silhouette shader must be supported for these rendering tests.");
        }

        [TearDown]
        public void Cleanup()
        {
            if (camera != null) camera.targetTexture = null;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void OffCenterCasterShadowsItsProjectedPositionWithoutMirroring()
        {
            var expected = ProjectedCenter();
            var mirrored = new Vector3(expected.x, -expected.y, expected.z);
            shadows.enabled = false;
            Capture();
            float expectedBefore = Sample(expected), mirroredBefore = Sample(mirrored);
            shadows.enabled = true;
            Capture();
            Assert.That(Sample(expected), Is.LessThan(expectedBefore - .08f),
                "A caster above the view center must shade its actual ground projection.");
            Assert.That(Sample(mirrored), Is.EqualTo(mirroredBefore).Within(.015f),
                "The vertically mirrored location must remain lit.");
        }

        [Test]
        public void TranslatingTheCameraKeepsTheShadowAttachedToItsWorldPosition()
        {
            caster.transform.position = new Vector3(0, 0, -1.2f);
            AssertProjectedShadow("The centered reference shadow must be visible.");
            camera.transform.position += new Vector3(.35f, .8f, 0);
            AssertProjectedShadow("Moving the view must not move the shadow away from its caster's world projection.");
        }

        [Test]
        public void CameraExcludedCasterDoesNotCastOntoVisibleGround()
        {
            caster.transform.position = new Vector3(0, 0, -1.2f);
            AssertProjectedShadow("The caster must first produce a visible reference shadow.");
            caster.layer = ExcludedLayer;
            AssertUnshadowed(ProjectedCenter(), "A caster outside this camera's culling mask must not cast into its image.");
        }

        [Test]
        public void CameraExcludedReceiverCannotPaintOntoVisibleNonReceiverGeometry()
        {
            caster.transform.position = new Vector3(0, 0, -1.2f);
            ground.GetComponent<SilhouetteParticipant>().Role = SilhouetteRole.None;
            Primitive("Excluded receiver", PrimitiveType.Quad, new Vector3(0, 0, -.05f), Vector3.one * 12,
                ExcludedLayer, SilhouetteRole.Receiver);
            AssertUnshadowed(ProjectedCenter(),
                "An excluded receiver in front of visible geometry must not add a shadow mask to that geometry.");
        }

        [Test]
        public void MultisampledCameraTargetKeepsOffCenterShadowsAligned()
        {
            var descriptor = new RenderTextureDescriptor(Resolution, Resolution, RenderTextureFormat.ARGB32, 24)
                { msaaSamples = 4 };
            if (SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor) < 4)
                Assert.Ignore("This graphics device does not support a 4x MSAA render target.");
            SetTarget(4);
            AssertProjectedShadow("The shadow mask must align with the camera image and depth on a 4x MSAA target.");
        }

        [Test]
        public void PerspectiveCameraKeepsOffCenterShadowsAligned()
        {
            camera.orthographic = false;
            camera.fieldOfView = 45;
            AssertProjectedShadow("A perspective camera must sample the same world-space shadow projection.");
        }

        [Test]
        public void TiltedOrthographicCameraKeepsOffCenterShadowsAligned()
        {
            camera.transform.position = new Vector3(0, -8, -10);
            camera.transform.rotation = Quaternion.LookRotation(-camera.transform.position, Vector3.up);
            AssertProjectedShadow("The angled gameplay view must keep shadows attached to their ground projection.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisablingSilhouettesRestoresTheOriginalRenderTextureSetting(bool original)
        {
            shadows.enabled = false;
            camera.forceIntoRenderTexture = original;
            for (int i = 0; i < 2; i++)
            {
                shadows.enabled = true;
                Assert.That(camera.forceIntoRenderTexture, Is.True,
                    "Silhouette rendering needs a consistent camera render target orientation.");
                shadows.enabled = false;
                Assert.That(camera.forceIntoRenderTexture, Is.EqualTo(original),
                    "Disabling silhouettes must restore the camera's original target setting after every toggle.");
            }
        }

        [Test]
        public void HdrCameraTargetKeepsShadowsAlignedAndOffForegroundGeometry()
        {
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                Assert.Ignore("This graphics device does not support a floating-point HDR render target.");
            camera.allowHDR = true;
            SetTarget(1, RenderTextureFormat.ARGBHalf);
            AssertProjectedShadow("An HDR target must keep the off-center shadow aligned with its world projection.");
            var expected = ProjectedCenter();
            Primitive("HDR foreground cover", PrimitiveType.Quad, new Vector3(expected.x, expected.y, -2),
                Vector3.one, VisibleLayer, SilhouetteRole.None);
            AssertUnshadowed(expected, "HDR scene depth must prevent ground shadows from painting over foreground objects.");
        }

        [Test]
        public void OffCenterForegroundGeometryOccludesTheGroundShadow()
        {
            AssertProjectedShadow("The uncovered ground must first have an off-center shadow.");
            var expected = ProjectedCenter();
            Primitive("Foreground cover", PrimitiveType.Quad, new Vector3(expected.x, expected.y, -2),
                Vector3.one, VisibleLayer, SilhouetteRole.None);
            AssertUnshadowed(expected, "The ground mask must respect scene depth at off-center foreground geometry.");
        }

        [UnityTest]
        public IEnumerator BackbufferShadowsAlignAndRespectForegroundDepth()
        {
            var otherCameras = new List<Camera>();
            var canvases = new List<Canvas>();
            foreach (var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (other != camera && other.enabled) { otherCameras.Add(other); other.enabled = false; }
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled) { canvases.Add(canvas); canvas.enabled = false; }
            try
            {
                camera.targetTexture = null;
                camera.depth = 100;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.enabled = true;
                var expected = ProjectedCenter();
                var mirrored = new Vector3(expected.x, -expected.y, expected.z);
                foreach (bool hdr in new[] { false, true })
                foreach (bool msaa in new[] { false, true })
                {
                    camera.allowHDR = hdr;
                    camera.allowMSAA = msaa;
                    shadows.enabled = false;
                    yield return new WaitForEndOfFrame();
                    var before = Own(ScreenCapture.CaptureScreenshotAsTexture());
                    Assert.That(Sample(expected, before), Is.GreaterThan(.9f),
                        $"HDR={hdr}, MSAA={msaa}: the backbuffer reference must contain unobstructed white ground.");

                    shadows.enabled = true;
                    yield return new WaitForEndOfFrame();
                    var after = Own(ScreenCapture.CaptureScreenshotAsTexture());
                    Directory.CreateDirectory("Temp/ShadowValidation");
                    File.WriteAllBytes($"Temp/ShadowValidation/backbuffer-hdr-{hdr}-msaa-{msaa}.png", after.EncodeToPNG());
                    Assert.That(Sample(expected, after), Is.LessThan(Sample(expected, before) - .08f),
                        $"HDR={hdr}, MSAA={msaa}: the backbuffer must show the off-center shadow at its world projection.");
                    Assert.That(Sample(mirrored, after), Is.EqualTo(Sample(mirrored, before)).Within(.015f),
                        $"HDR={hdr}, MSAA={msaa}: the backbuffer must not display a mirrored shadow.");

                    var cover = Primitive("Backbuffer foreground cover", PrimitiveType.Quad,
                        new Vector3(expected.x, expected.y, -2), Vector3.one, VisibleLayer, SilhouetteRole.None);
                    yield return new WaitForEndOfFrame();
                    var covered = Own(ScreenCapture.CaptureScreenshotAsTexture());
                    Assert.That(Sample(expected, covered), Is.EqualTo(Sample(expected, before)).Within(.015f),
                        $"HDR={hdr}, MSAA={msaa}: backbuffer scene depth must keep ground shadows off foreground objects.");
                    cover.SetActive(false);
                }
            }
            finally
            {
                camera.enabled = false;
                camera.targetTexture = target;
                foreach (var other in otherCameras) if (other != null) other.enabled = true;
                foreach (var canvas in canvases) if (canvas != null) canvas.enabled = true;
            }
        }

        private void AssertProjectedShadow(string message)
        {
            var expected = ProjectedCenter();
            shadows.enabled = false;
            Capture();
            float before = Sample(expected);
            Assert.That(before, Is.GreaterThan(.9f), "The expected shadow sample must start on unobstructed white ground.");
            shadows.enabled = true;
            Capture();
            Assert.That(Sample(expected), Is.LessThan(before - .08f), message);
        }

        private void AssertUnshadowed(Vector3 position, string message)
        {
            shadows.enabled = false;
            Capture();
            float before = Sample(position);
            Assert.That(before, Is.GreaterThan(.9f), "The comparison sample must start on white ground.");
            shadows.enabled = true;
            Capture();
            Assert.That(Sample(position), Is.EqualTo(before).Within(.015f), message);
        }

        private Vector3 ProjectedCenter()
        {
            var position = caster.transform.position;
            return position + shadows.Direction * (-position.z / shadows.Direction.z);
        }

        private void Capture()
        {
            camera.Render();
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(target, resolved);
                RenderTexture.active = resolved;
                readback.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                readback.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        private float Sample(Vector3 position, Texture2D image = null)
        {
            if (image == null) image = readback;
            var viewport = camera.WorldToViewportPoint(position);
            Assert.That(viewport.x, Is.InRange(.02f, .98f));
            Assert.That(viewport.y, Is.InRange(.02f, .98f));
            int x = Mathf.FloorToInt(viewport.x * image.width), y = Mathf.FloorToInt(viewport.y * image.height);
            float total = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++) total += image.GetPixel(x + dx, y + dy).grayscale;
            return total / 9;
        }

        private void SetTarget(int samples, RenderTextureFormat format = RenderTextureFormat.ARGB32)
        {
            target = Own(new RenderTexture(Resolution, Resolution, 24, format)
                { antiAliasing = samples });
            camera.targetTexture = target;
        }

        private GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            int layer, SilhouetteRole role)
        {
            var obj = Own(GameObject.CreatePrimitive(type));
            obj.name = name;
            obj.layer = layer;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = white;
            SilhouetteParticipant.Register(obj.transform, role, true);
            return obj;
        }

        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
    }
}
#endif
