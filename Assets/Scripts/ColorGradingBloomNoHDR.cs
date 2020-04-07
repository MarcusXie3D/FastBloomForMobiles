// author : Marcus Xie

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[ExecuteInEditMode]
public class ColorGradingBloomNoHDR : MonoBehaviour
{
    public bool enableBloom = true;
    public bool enableColorGrading = false;
    [Range(2, 16)]
    public int bloomExtend = 4;
    public Texture2D ColorGradingLUT = null;
    public Color bloomColor = Color.white;
    public float globalBloomStrength = 1.5f;

    private RenderTexture myRenderTexture;
    private Camera cam;
    private Material dualFilterBloomMat;
    private const int DownPass = 0;
    private const int UpPass = 1;
    private const int ApplyColorGradingBloomPass = 2;
    private const int ApplyColorGradingPass = 3;
    private const int ApplyBloomPass = 4;
    private RenderTexture rt1;
    private RenderTexture rt2;
    private RenderBuffer[] _mrt;
    private bool flag = true;
    private Texture3D converted3DLut = null;
    private bool isSupported;

    void OnEnable()
    {
        CheckSupport(true);
        cam = transform.GetComponent<Camera>();
        dualFilterBloomMat = new Material(Shader.Find("Custom/DualFilterBloomNoHDR"));
        dualFilterBloomMat.hideFlags = HideFlags.DontSave;
        dualFilterBloomMat.SetColor("_BloomColor", bloomColor);
        dualFilterBloomMat.SetFloat("_GlobalBloomStrength", globalBloomStrength);
        _mrt = new RenderBuffer[2];
        if (ColorGradingLUT != null)
            Convert(ColorGradingLUT);
    }
    void OnDisable()
    {
        if (dualFilterBloomMat)
        {
            DestroyImmediate(dualFilterBloomMat);
            dualFilterBloomMat = null;
        }
    }

    // for every frame, before the camera renders, set up multiple-render-targets (MRT)
    void OnPreRender()
    {
        int resWidth = Screen.currentResolution.width; // width of resolution
        int resHeight = Screen.currentResolution.height; // height of resolution

        if (enableColorGrading && !enableBloom)
        {
            rt1 = RenderTexture.GetTemporary(resWidth, resHeight, 16);
            _mrt[0] = rt1.colorBuffer;
            cam.backgroundColor = Color.clear;
            cam.SetTargetBuffers(_mrt[0], rt1.depthBuffer);
        }
        else if (!enableColorGrading && enableBloom)
        {
            // render-texture to store other normal result except the bloom, 
            // later to be blended with bloom
            rt1 = RenderTexture.GetTemporary(resWidth, resHeight, 16);
            // render-texture to store the bloom
            rt2 = RenderTexture.GetTemporary(resWidth, resHeight, 0, RenderTextureFormat.R8);

            _mrt[0] = rt1.colorBuffer;
            _mrt[1] = rt2.colorBuffer;

            cam.backgroundColor = Color.clear;
            // let camera to render into 2 targets, 
            // rendering normal result and bloom simultaneously using multiple-render-targets (MRT)
            cam.SetTargetBuffers(_mrt, rt1.depthBuffer);
        }
        else if (enableColorGrading && enableBloom)
        {
            // render-texture to store other normal result except the bloom, 
            // later to be blended with bloom
            rt1 = RenderTexture.GetTemporary(resWidth, resHeight, 16);
            // render-texture to store the bloom
            rt2 = RenderTexture.GetTemporary(resWidth, resHeight, 0, RenderTextureFormat.R8);

            _mrt[0] = rt1.colorBuffer;
            _mrt[1] = rt2.colorBuffer;

            cam.backgroundColor = Color.clear;
            // let camera to render into 2 targets, 
            // rendering normal result and bloom simultaneously using multiple-render-targets (MRT)
            cam.SetTargetBuffers(_mrt, rt1.depthBuffer);
        }
    }

    // for every frame, after the camera renders, 
    void OnPostRender()
    {
        if (enableColorGrading && !enableBloom)
        {
            cam.targetTexture = null; //null means framebuffer

            dualFilterBloomMat.SetTexture("_SourceTex", rt1);// original rt
            int lutSize = converted3DLut.width;
            converted3DLut.wrapMode = TextureWrapMode.Clamp;
            dualFilterBloomMat.SetFloat("_Scale", (lutSize - 1) / (1.0f * lutSize));
            dualFilterBloomMat.SetFloat("_Offset", 1.0f / (2.0f * lutSize));
            if (converted3DLut == null)
            {
                SetIdentityLut();
            }
            dualFilterBloomMat.SetTexture("_ClutTex", converted3DLut);

            Graphics.Blit(rt1, null as RenderTexture, dualFilterBloomMat, ApplyColorGradingPass);
            rt1.DiscardContents();
            RenderTexture.ReleaseTemporary(rt1);
        }
        else if (!enableColorGrading && enableBloom)
        {
            cam.targetTexture = null; //null means framebuffer

            int width = rt2.width;
            int height = rt2.height;
            RenderTextureFormat format = rt2.format;

            RenderTexture[] textures = new RenderTexture[16];

            RenderTexture currentSource = textures[0] = rt2;// threshold rt

            // dual filtering, progressively scale down rt2 step-by-step, and scale it up back to normal resolution
            // do filtering / sampling every time it scales
            DualFilterIterations(ref currentSource, ref textures, ref width, ref height, ref format);

            dualFilterBloomMat.SetTexture("_SourceTex", rt1);// original rt

            Graphics.Blit(currentSource, null as RenderTexture, dualFilterBloomMat, ApplyBloomPass);
            currentSource.DiscardContents();
            rt1.DiscardContents();
            RenderTexture.ReleaseTemporary(currentSource);
            RenderTexture.ReleaseTemporary(rt1);
            //RenderTexture.ReleaseTemporary(rt2); currentSource and rt2 refer to the same block of memory, don't release it twice 
        }
        else if (enableColorGrading && enableBloom)
        {
            cam.targetTexture = null; //null means framebuffer

            int width = rt2.width;
            int height = rt2.height;
            RenderTextureFormat format = rt2.format;

            RenderTexture[] textures = new RenderTexture[16];

            RenderTexture currentSource = textures[0] = rt2;// threshold rt

            // dual filtering, progressively scale down rt2 step-by-step, and scale it up back to normal resolution
            // do filtering / sampling every time it scales
            DualFilterIterations(ref currentSource, ref textures, ref width, ref height, ref format);

            dualFilterBloomMat.SetTexture("_SourceTex", rt1);// original rt
            int lutSize = converted3DLut.width;
            converted3DLut.wrapMode = TextureWrapMode.Clamp;
            dualFilterBloomMat.SetFloat("_Scale", (lutSize - 1) / (1.0f * lutSize));
            dualFilterBloomMat.SetFloat("_Offset", 1.0f / (2.0f * lutSize));
            if (converted3DLut == null)
            {
                SetIdentityLut();
            }
            dualFilterBloomMat.SetTexture("_ClutTex", converted3DLut);

            Graphics.Blit(currentSource, null as RenderTexture, dualFilterBloomMat, ApplyColorGradingBloomPass);
            currentSource.DiscardContents();
            rt1.DiscardContents();
            RenderTexture.ReleaseTemporary(currentSource);
            RenderTexture.ReleaseTemporary(rt1);
            //RenderTexture.ReleaseTemporary(rt2); currentSource and rt2 refer to the same block of memory, don't release it twice 
        }
    }

    public void SetIdentityLut()
    {
        int dim = 16;
        var newC = new Color[dim * dim * dim];
        float oneOverDim = 1.0f / (1.0f * dim - 1.0f);

        for (int i = 0; i < dim; i++)
        {
            for (int j = 0; j < dim; j++)
            {
                for (int k = 0; k < dim; k++)
                {
                    newC[i + (j * dim) + (k * dim * dim)] = new Color((i * 1.0f) * oneOverDim, (j * 1.0f) * oneOverDim, (k * 1.0f) * oneOverDim, 1.0f);
                }
            }
        }

        if (converted3DLut)
            DestroyImmediate(converted3DLut);
        converted3DLut = new Texture3D(dim, dim, dim, TextureFormat.ARGB32, false);
        converted3DLut.SetPixels(newC);
        converted3DLut.Apply();
    }

    public void Convert(Texture2D temp2DTex)
    {

        // conversion fun: the given 2D texture needs to be of the format
        //  w * h, wheras h is the 'depth' (or 3d dimension 'dim') and w = dim * dim

        if (temp2DTex)
        {
            int dim = temp2DTex.width * temp2DTex.height;
            dim = temp2DTex.height;

            if (!ValidDimensions(temp2DTex))
            {
                Debug.LogWarning("The given 2D texture " + temp2DTex.name + " cannot be used as a 3D LUT.");
                return;
            }

            var c = temp2DTex.GetPixels();
            var newC = new Color[c.Length];

            for (int i = 0; i < dim; i++)
            {
                for (int j = 0; j < dim; j++)
                {
                    for (int k = 0; k < dim; k++)
                    {
                        int j_ = dim - j - 1;
                        newC[i + (j * dim) + (k * dim * dim)] = c[k * dim + i + j_ * dim * dim];
                    }
                }
            }

            if (converted3DLut)
                DestroyImmediate(converted3DLut);
            converted3DLut = new Texture3D(dim, dim, dim, TextureFormat.ARGB32, false);
            converted3DLut.SetPixels(newC);
            converted3DLut.Apply();
        }
        else
        {
            // error, something went terribly wrong
            Debug.LogError("Couldn't color correct with 3D LUT texture. Image Effect will be disabled.");
        }
    }

    public bool ValidDimensions(Texture2D tex2d)
    {
        if (!tex2d) return false;
        int h = tex2d.height;
        if (h != Mathf.FloorToInt(Mathf.Sqrt(tex2d.width)))
        {
            return false;
        }
        return true;
    }

    // dual filtering, progressively scale down rt2 step-by-step, and scale it up back to normal resolution
    // do filtering / sampling every time it scales
    private void DualFilterIterations(ref RenderTexture currentSource, ref RenderTexture[] textures, ref int width, ref int height, ref RenderTextureFormat format)
    {
        RenderTexture currentDestination;
        int i;
        for (i = 0; i < bloomExtend; i++)
        {
            if (i > 0) // first, down-scale once without dual-fitering, and only do dual-filtering on smaller resolutions, which is too save performance
            {
                dualFilterBloomMat.SetFloat("_HalfPixelX", 1f / (float)width);
                dualFilterBloomMat.SetFloat("_HalfPixelY", 1f / (float)height);
            }
            width /= 2;
            height /= 2;
            // 
            if (height < 2 || width < 2)
            {
                break;
            }
            // in textures[], save a reference to the generated temporary render-texture, avoiding to generate RTs again in the up-scaling phase
            currentDestination = textures[i + 1] = RenderTexture.GetTemporary(width, height, 0, format);
            // DiscardContents to avoid read-back operations on mobile platforms, which costs performance
            currentDestination.DiscardContents();
            if (i > 0) // first, down-scale once without dual-fitering, and only do dual-filtering on smaller resolutions, which is too save performance
                Graphics.Blit(currentSource, currentDestination, dualFilterBloomMat, DownPass);
            else
                Graphics.Blit(currentSource, currentDestination);
            currentSource.DiscardContents();
            // ping-pong operation between currentSource and currentDestination
            currentSource = currentDestination;
        }

        textures[i] = null;

        for (i -= 1; i >= 0; i--)
        {
            // in textures[], references to the generated temporary render-textures are saved, avoiding to generate RTs again in the up-scaling phase
            currentDestination = textures[i];
            textures[i] = null;
            width *= 2;
            height *= 2;
            if (i > 0) // last, up-scale once without dual-fitering, and by now dual-filtering has been applied on smaller resolutions, which is too save performance
            {
                dualFilterBloomMat.SetFloat("_HalfPixelX", 1f / (float)width);
                dualFilterBloomMat.SetFloat("_HalfPixelY", 1f / (float)height);
            }
            // DiscardContents to avoid read-back operations on mobile platforms, which costs performance
            currentDestination.DiscardContents();
            if (i > 0) // last, up-scale once without dual-fitering, and by now dual-filtering has been applied on smaller resolutions, which is too save performance
                Graphics.Blit(currentSource, currentDestination, dualFilterBloomMat, UpPass);
            else
                Graphics.Blit(currentSource, currentDestination);
            currentSource.DiscardContents();
            RenderTexture.ReleaseTemporary(currentSource);
            // ping-pong operation between currentSource and currentDestination
            currentSource = currentDestination;
        }
    }

    // pass in "true" if depth-texture is needed
    void CheckSupport(bool needDepth)
    {
        isSupported = true;

        if (!SystemInfo.supportsImageEffects)
        {
            Debug.Log("supportsImageEffects fails");
            NotSupported();
            return;
        }

        if (needDepth && !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
        {
            Debug.Log("SupportsRenderTextureFormat(RenderTextureFormat.Depth) fails");
            NotSupported();
            return;
        }

        if (needDepth)
            GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;

        if (!SystemInfo.supports3DTextures)
        {
            Debug.Log("supports3DTextures fails");
            NotSupported();
            return;
        }

        return;
    }

    // if the platform doesn't support these effects, this script will be disabled
    void NotSupported()
    {
        enabled = false;
        isSupported = false;
        return;
    }
}
