#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#endif

namespace pi.LTCGI
{
    #if UNITY_EDITOR
    public partial class LTCGI_Controller
    {
        public bool PrecomputeOnBuild = true;
        private Texture2D[] CreateLODs(Texture2D input, int width, int height)
        {
            var result = new Texture2D[4];
            
            // apply gamma to lod0 too and resize it
            var rt0 = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt0.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(input, rt0);
            result[0] = ReadIntoTexture2D(rt0);
            RenderTexture.DestroyImmediate(rt0);

            var CustomRenderTextureCenters = new Vector4[1];
            CustomRenderTextureCenters[0] = new Vector4(0.5f, 0.5f, 0, 0);
            LOD1.material.SetVectorArray("CustomRenderTextureCenters", CustomRenderTextureCenters);
            var CustomRenderTextureSizesAndRotations = new Vector4[1];
            CustomRenderTextureSizesAndRotations[0] = new Vector4(1, 1, 0, 0);
            LOD1.material.SetVectorArray("CustomRenderTextureSizesAndRotations", CustomRenderTextureSizesAndRotations);
            
            for (int i = 0; i < 3; i++)
            {
                var crt = new CustomRenderTexture[] { LOD1, LOD2, LOD3 } [i];
                var crtS = new CustomRenderTexture[] { LOD1s, LOD2s, LOD3s } [i];
                var origTex = crt.material.GetTexture("_MainTex");
                var origTexS = crtS.material.GetTexture("_MainTex");
                crtS.material.SetTexture("_MainTex", result[i]);
                var rt = new RenderTexture(width/Mathf.NextPowerOfTwo((i+1)*2), height/Mathf.NextPowerOfTwo((i+1)*2), 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                rt.wrapMode = TextureWrapMode.Clamp;
                Graphics.Blit(result[i], rt, crtS.material, 0);
                var rt2 = new RenderTexture(rt.width, rt.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                rt.wrapMode = TextureWrapMode.Clamp;
                crt.material.SetTexture("_MainTex", rt);
                Graphics.Blit(rt, rt2, crt.material, 1);
                result[i + 1] = ReadIntoTexture2D(rt2);
                crt.material.SetTexture("_MainTex", origTex);
                crtS.material.SetTexture("_MainTex", origTexS);
                RenderTexture.DestroyImmediate(rt);
                RenderTexture.DestroyImmediate(rt2);
            }
            return result;
        }

        [MenuItem("Tools/LTCGI/Precompute Static Textures")]
        public static void CreateLODTextureArraysMenu() => LTCGI_Controller.Singleton?.CreateLODTextureArrays();

        public void CreateLODTextureArrays()
        {
            var curscene = EditorSceneManager.GetActiveScene().name;
            
            if (StaticTextures == null || StaticTextures.Length == 0)
            {
                for (int lod = 0; lod < 4; lod++)
                {
                    try
                    {
                        AssetDatabase.DeleteAsset("Assets/_pi_/_LTCGI/Generated/lod-" + curscene + "-" + lod + ".asset");
                    }
                    catch {}
                }
                return;
            }

            EditorUtility.DisplayProgressBar("LTCGI: Precomputing Static Textures", "Calculating LODs...", 0.0f);

            try
            {
                // Create LODs by applying blur shader
                var inputLods = new Texture2D[StaticTextures.Length, 4];
                var width = StaticTextures.Max(x => x.width);
                var height = StaticTextures.Max(x => x.height);
                for (int i = 0; i < StaticTextures.Length; i++)
                {
                    var lods = CreateLODs(StaticTextures[i], width, height);
                    inputLods[i, 0] = lods[0];
                    inputLods[i, 1] = lods[1];
                    inputLods[i, 2] = lods[2];
                    inputLods[i, 3] = lods[3];

                    EditorUtility.DisplayProgressBar("LTCGI: Precomputing Static Textures", "Calculating LODs...", 0.5f * ((float)i / (float)StaticTextures.Length));
                }

                EditorUtility.DisplayProgressBar("LTCGI: Precomputing Static Textures", "Generating Texture Arrays...", 0.5f);

                // Fill into compressed Texture2DArrays
                for (int lod = 0; lod < 4; lod++)
                {
                    Texture2DArray texture2DArray = new Texture2DArray(
                        inputLods[0, lod].width,
                        inputLods[0, lod].height,
                        StaticTextures.Length,
                        TextureFormat.BC7, true, false);
                    texture2DArray.wrapMode = TextureWrapMode.Clamp;

                    for (int i = 0; i < StaticTextures.Length; i++)
                    {
                        SetTextureImporterFormat(inputLods[i, lod], true);
                        Texture2D temp = new Texture2D(texture2DArray.width, texture2DArray.height, TextureFormat.RGBA32, true, true);
                        temp.wrapMode = TextureWrapMode.Clamp;
                        temp.SetPixels32(inputLods[i, lod].GetPixels32(0));
                        temp.Apply();
                        EditorUtility.CompressTexture(temp, TextureFormat.BC7, UnityEditor.TextureCompressionQuality.Best);
                        temp.Apply();
                        for (int mip = 0; mip < temp.mipmapCount; mip++) {
                            Graphics.CopyTexture(temp, 0, mip, texture2DArray, i, mip);
                        }
                        Texture2D.DestroyImmediate(temp);
                    }

                    // Save as asset
                    if (!AssetDatabase.IsValidFolder("Assets/_pi_/_LTCGI/Generated"))
                        AssetDatabase.CreateFolder("Assets/_pi_/_LTCGI", "Generated");
                    AssetDatabase.CreateAsset(texture2DArray, "Assets/_pi_/_LTCGI/Generated/lod-" + curscene + "-" + lod + ".asset");

                    EditorUtility.DisplayProgressBar("LTCGI: Precomputing Static Textures", "Generating Texture Arrays...", 0.5f + 0.5f * (lod / 3.0f));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            UpdateMaterials();
        }
    }
    #endif
}