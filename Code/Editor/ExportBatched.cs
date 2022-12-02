using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace EtherealEngine
{
    public partial class ExportGLTF : EditorWindow
    {
        public event EventHandler<OnGLTFExportArgs> OnExportBatched;

        public async void ExportBatched()
        {
            exporting = true;
            var objs = Selection.gameObjects;
            foreach (var obj in objs)
            {
                string name = string.Format("{0}.glb", obj.name);
                string originalDst = new string(EEExportSettings.dst);
                //string dst = Path.Join(Path.GetDirectoryName(originalDst), name);
                string dst = Regex.Match(originalDst, ".*(?=[\\/]+[^\\/]+$)").Value + "/" + name;
                EEExportSettings.dst = dst;
                foreach(var toHide in objs)
                {
                    if (obj == toHide) continue;
                    toHide.SetActive(false);
                }
                await DoExport();
                foreach(var toShow in objs)
                {
                    if (obj == toShow) continue;
                    toShow.SetActive(true);
                }
                EEExportSettings.dst = originalDst;
            }
            OnExportBatched?.Invoke(this, new OnGLTFExportArgs
            {
                success = true,
                log = ""
            });
        }
    }
}
