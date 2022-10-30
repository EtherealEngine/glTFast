using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLTFast.Schema;

namespace EtherealEngine
{
    [System.Serializable]
    public class MOZ_lightmap
    {
        public float intensity = 1f;
        public int texCoord = 1;
        public int index = -1;
        public Vector2 offset = Vector2.zero;
        public Vector2 scale = Vector2.one;
        
        internal void GltfSerialize(JsonWriter writer)
        {
            writer.AddObject();
            writer.AddProperty("intensity", intensity);

            writer.AddProperty("texCoord", texCoord);

            writer.AddProperty("index", index);

            writer.AddProperty("extensions");
            writer.AddObject();
            writer.AddProperty("KHR_texture_transform");
            writer.AddObject();
            float[] offsetVals = !(float.IsNaN(offset.x) || float.IsNaN(offset.y)) ?
                new float[] { offset.x, 1 - offset.y } : 
                new float[] { 0, 0 };
            float[] scaleVals = !(float.IsNaN(scale.x) || float.IsNaN(scale.y)) ? 
                new float[] { scale.x, -scale.y } : 
                new float[] { 1, 1 };
            writer.AddArrayProperty("offset", offsetVals);
            writer.AddArrayProperty("scale", scaleVals);
            writer.Close();
            writer.Close();
            writer.Close();
        }
    }
}
