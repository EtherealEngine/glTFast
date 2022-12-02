using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

using GLTFast.Export;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EtherealEngine
{
    public struct OnGLTFExportArgs
    {
        public bool success;
        public string log;
    }

    public class EEExportSettings
    {
        public static readonly string BASE_PATH = Application.dataPath;
        public static Shader eeStandardShader = Shader.Find("Ethereal Engine/PbrMetallicRoughness");
        public static Shader eeBasicShader = Shader.Find("Ethereal Engine/Unlit");
        public static string dst;
    }

    public struct MaterialInstance : IEquatable<MaterialInstance>
    {
        public Material material;
        public Renderer renderer;
        public int index;

        public MaterialInstance(Material _material, Renderer _renderer)
        {
            material = _material;
            renderer = _renderer;
            index = renderer.sharedMaterials.ToList().IndexOf(material);
        }

        public MaterialInstance(Renderer _renderer, int _index = 0)
        {
            renderer = _renderer;
            index = _index;
            material = renderer.sharedMaterials[index];
        }

        public bool Equals(MaterialInstance other)
        {
            return material == other.material && renderer == other.renderer && index == other.index;
        }
    }

    public struct MaterialReplacement : IEquatable<MaterialReplacement>
    {
        public HashSet<MaterialInstance> originals;
        public Material replacement;
        public readonly int lightmapIndex;
        public readonly Vector4 lightmapTransform;


        public MaterialReplacement(Renderer _renderer, int _index = 0)
        {
            originals = new HashSet<MaterialInstance>();
            originals.Add(new MaterialInstance(_renderer, _index));
            lightmapIndex = _renderer.lightmapIndex;
            lightmapTransform = _renderer.lightmapScaleOffset;
            replacement = null;
        }

        public bool Equals(MaterialReplacement other)
        {
            return GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            if (originals.Count < 1 || originals.ElementAt(0).material == null | lightmapTransform == null)
            {
                Debug.Log("here");
            }
            return originals.ElementAt(0).material.GetHashCode() ^ lightmapIndex ^ lightmapTransform.GetHashCode();
        }
    }

    public partial class ExportGLTF : EditorWindow
    {
        public event EventHandler<OnGLTFExportArgs> OnExportGLTF;

        [MenuItem("Ethereal Engine/Export")]
        static void Init()
        {
            ExportGLTF window = (ExportGLTF)GetWindow(typeof(ExportGLTF));
            window.Show();
        }

        HashSet<MaterialReplacement> replacements;

        void processObject(GameObject subject)
        {
            if (subject.TryGetComponent(out MeshRenderer renderer))
            {
                if (renderer.lightmapIndex != -1)
                {
                    MaterialReplacement key = new MaterialReplacement(renderer);
                    MaterialReplacement replacement;
                    if (replacements.TryGetValue(key, out replacement))
                    {
                        replacement.originals.Add(new MaterialInstance(renderer));
                    }
                    else
                    {
                        replacements.Add(key);
                    }
                }
            }
        }

        void replaceTexture(Material oldMat, Material newMat, string oldName, string newName)
        {
            Texture texture = oldMat.GetTexture(oldName);
            Vector2 offset = oldMat.GetTextureOffset(oldName);
            Vector2 scale = oldMat.GetTextureScale(oldName);
            newMat.SetTexture(newName, texture);
            newMat.SetTextureOffset(newName, offset);
            newMat.SetTextureScale(newName, scale);
        }

        void replaceTexture(Material oldMat, Material newMat, string[] oldNames, string newName)
        {
            string oldName = null;
            for (int i = 0; i < oldNames.Length; i++)
            {
                oldName = oldNames[i];
                if (oldMat.HasTexture(oldName)) break;
            }
            if (oldName == null || !oldMat.HasTexture(oldName))
            {
                throw new Exception("No matching property found on material " + oldMat.name + " in list " + oldNames.ToString());
            }
            replaceTexture(oldMat, newMat, oldName, newName);
        }

        void replaceFloat(Material oldMat, Material newMat, string oldName, string newName)
        {
            float value = oldMat.GetFloat(oldName);
            newMat.SetFloat(newName, value);
        }

        void replaceFloat(Material oldMat, Material newMat, string name)
        {
            replaceFloat(oldMat, newMat, name, name);
        }

        void replaceFloat(Material oldMat, Material newMat, string[] oldNames, string newName)
        {
            string oldName = null;
            for (int i = 0; i < oldNames.Length; i++)
            {
                oldName = oldNames[i];
                if (oldMat.HasFloat(oldName)) break;
            }
            if (oldName == null || !oldMat.HasFloat(oldName))
            {
                throw new Exception("No matching property found on material " + oldMat.name + " in list " + oldNames.ToString());
            }
            replaceFloat(oldMat, newMat, oldName, newName);
        }

        void replaceColor(Material oldMat, Material newMat, string oldName, string newName)
        {
            Color value = oldMat.GetColor(oldName);
            newMat.SetColor(newName, value);
        }

        void replaceColor(Material oldMat, Material newMat, string name)
        {
            replaceColor(oldMat, newMat, name, name);
        }

        void replaceColor(Material oldMat, Material newMat, string[] oldNames, string newName)
        {
            string oldName = null;
            for (int i = 0; i < oldNames.Length; i++)
            {
                oldName = oldNames[i];
                if (oldMat.HasColor(oldName)) break;
            }
            if (oldName == null || !oldMat.HasColor(oldName))
            {
                throw new Exception("No matching property found on material " + oldMat.name + " in list " + oldNames.ToString());
            }
            replaceColor(oldMat, newMat, oldName, newName);
        }

        void generateReplacements()
        {
            var entries = replacements.ToArray();
            Debug.Log("Found " + entries.Length + " replacements");
            for (int i = 0; i < entries.Length; i++)
            {
                var replacement = entries[i];
                Material original = replacement.originals.ElementAt(0).material;
                bool isUnlit = original.shader.name.Contains("Unlit");
                Shader replacementShader = isUnlit
                    ? EEExportSettings.eeBasicShader
                    : EEExportSettings.eeStandardShader;
                Material rMat = new Material(replacementShader);
                rMat.name = original.name + "-Replacement-" + (i + 1);

                replaceTexture(original, rMat, new string[] { "_MainTex", "baseColorTexture" }, "baseColorTexture");
                if (!isUnlit)
                {
                    replaceTexture(original, rMat, new string[] { "_BumpMap", "normalTexture" }, "normalTexture");
                    replaceTexture(original, rMat, new string[] { "_EmissionMap", "emissiveTexture" }, "emissiveTexture");
                    replaceTexture(original, rMat, new string[] { "_MetallicGlossMap", "metallicRoughnessTexture" }, "metallicRoughnessTexture");
                    replaceTexture(original, rMat, new string[] { "_OcclusionMap", "occlusionTexture" }, "occlusionTexture");

                    replaceFloat(original, rMat, new string[] { "_Metallic", "metallicFactor" }, "metallicFactor");
                    replaceFloat(original, rMat, new string[] { "_Glossiness", "roughnessFactor" }, "roughnessFactor");

                    replaceColor(original, rMat, new string[] { "_Color", "baseColorFactor" }, "baseColorFactor");
                    replaceColor(original, rMat, new string[] { "_EmissionColor", "emissiveFactor" }, "emissiveFactor");
                }
                replaceFloat(original, rMat, new string[] { "_Cutoff", "alphaCutoff" }, "alphaCutoff");

                replaceFloat(original, rMat, "_Mode");
                replaceFloat(original, rMat, "_DstBlend");
                replaceFloat(original, rMat, "_SrcBlend");
                replaceFloat(original, rMat, "_ZWrite");

                replaceFloat(original, rMat, "_CullMode");


                if (original.shader.name.Contains("PbrMetallicRoughness"))
                {
                    rMat.SetFloat("roughnessFactor", 1 - rMat.GetFloat("roughnessFactor"));
                }
                rMat.shaderKeywords = original.shaderKeywords;
                rMat.globalIlluminationFlags = original.globalIlluminationFlags;
                rMat.renderQueue = original.renderQueue;
                for (int j = 0; j < original.enabledKeywords.Length; j++)
                {
                    rMat.SetKeyword(new UnityEngine.Rendering.LocalKeyword(replacementShader, original.enabledKeywords[j].name), true);
                }
                rMat.SetOverrideTag("RenderType", original.GetTag("RenderType", false, "Opaque"));
                rMat.enableInstancing = original.enableInstancing;
                rMat.globalIlluminationFlags = original.globalIlluminationFlags;

                if (rMat.HasTexture("normalTexture"))
                {
                    rMat.SetKeyword(new UnityEngine.Rendering.LocalKeyword(EEExportSettings.eeStandardShader, "_NORMALMAP"), true);
                }

                if (rMat.HasTexture("metallicRoughnessTexture"))
                {
                    rMat.SetKeyword(new UnityEngine.Rendering.LocalKeyword(EEExportSettings.eeStandardShader, "_METALLICGLOSSMAP"), true);
                }

                rMat.SetTexture("lightMapTexture", LightmapSettings.lightmaps[replacement.lightmapIndex].lightmapColor);
                rMat.SetTextureScale("lightMapTexture", new Vector2(replacement.lightmapTransform[0], replacement.lightmapTransform[1]));
                rMat.SetTextureOffset("lightMapTexture", new Vector2(replacement.lightmapTransform[2], replacement.lightmapTransform[3]));

                replacement.replacement = rMat;

                replacement.originals.ToList().ForEach((instance) =>
                {
                    instance.renderer.sharedMaterials = instance.renderer.sharedMaterials.Select((material, index) =>
                    {
                        return index == instance.index ? rMat : material;
                    }).ToArray();
                });
            }
        }

        private void Stage()
        {
            replacements = new HashSet<MaterialReplacement>();
            var currentScene = EditorSceneManager.GetActiveScene();
            var roots = currentScene.GetRootGameObjects().ToList();
            Debug.Log("found " + roots.Count + " roots");
            while (roots.Count > 0)
            {
                var root = roots[0];
                roots.RemoveAt(0);
                processObject(root);
                foreach (var transform in root.GetComponentsInChildren<Transform>())
                {
                    if (transform != root.transform) roots.Add(transform.gameObject);
                }
            }
            generateReplacements();
            Debug.Log("Staged");
        }

        private void Unstage()
        {
            replacements.ToList().ForEach((entry) =>
            {
                int n = entry.originals.Count;
                var originals = entry.originals.ToArray();
                for (int i = 0; i < n; i++)
                {
                    var original = originals[i];
                    original.renderer.sharedMaterials = original.renderer.sharedMaterials.Select((material, index) =>
                    {
                        return index == original.index ? original.material : material;
                    }).ToArray();
                }
            });
            replacements.Clear();
            Debug.Log("Unstaged");
        }

        async Task DoExport()
        {
            exporting = true;
            try
            {
                var currentScene = EditorSceneManager.GetActiveScene();

                Stage();

                var roots = currentScene.GetRootGameObjects();
                var export = new GameObjectExport(new ExportSettings
                {
                    fileConflictResolution = FileConflictResolution.Overwrite,
                    format = GltfFormat.Binary,
                    imageDestination = ImageDestination.MainBuffer,
                    
                });

                export.AddScene(roots);
                bool success = await export.SaveToFileAndDispose(EEExportSettings.dst);

                Unstage();

                OnExportGLTF?.Invoke(this, new OnGLTFExportArgs
                {
                    success = success,
                    log = ""
                });
            }
            catch (Exception ex)
            {
                exporting = false;
                throw ex;
            }
        }

        bool exporting;
        EventHandler<OnGLTFExportArgs> handler;
        private void OnGUI()
        {
            GUILayout.Label("Export Path:");
            GUILayout.Space(8);
            GUILayout.Label(EEExportSettings.dst);

            if (GUILayout.Button("..."))
            {
                EEExportSettings.dst = EditorUtility.SaveFilePanel("dst", EEExportSettings.BASE_PATH, "dst", "");
            }

            if (GUILayout.Button("Stage"))
            {
                Stage();
            }

            if (GUILayout.Button("Unstage"))
            {
                Unstage();
            }

            if (EEExportSettings.dst != null && EEExportSettings.dst.Length > 0)
            {
                if (exporting)
                {
                    GUILayout.Label("Exporting...");
                }
                else
                {
                    handler = (obj, args) =>
                    {
                        exporting = false;
                        Debug.Log("Export: " + args.success);
                        Debug.Log(args.log);
                        OnExportGLTF -= handler;
                    };
                    if (GUILayout.Button("Export"))
                    {
                        OnExportGLTF += handler;
                        DoExport();
                    }
                    if (GUILayout.Button("Export Selected Individually"))
                    {
                        OnExportBatched += handler;
                        ExportBatched();
                    }
                }
            }
            else
            {
                exporting = false;
            }
        }
    }
}
