using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Çarpıştırıcısız (collider) ilkel nesneler. <see cref="GameObject.CreatePrimitive"/> collider ekler; oysa
    /// seçim ekran uzayında yapılır, fizik gerekmez — ve web derlemesinde fizik kodu ayıklandığı için collider
    /// eklemek her nesnede konsola hata yazar.
    /// </summary>
    public static class Primitives
    {
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();

        public static GameObject Create(PrimitiveType type, string name)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh(type);
            go.AddComponent<MeshRenderer>();
            return go;
        }

        static Mesh GetMesh(PrimitiveType type)
        {
            if (meshes.TryGetValue(type, out Mesh mesh) && mesh != null) return mesh;

            // Unity'nin yerleşik ilkel modelleri ("Cube.fbx", "Cylinder.fbx" …); bulunamazsa bir kez CreatePrimitive.
            mesh = Resources.GetBuiltinResource<Mesh>(type + ".fbx");
            if (mesh == null)
            {
                var probe = GameObject.CreatePrimitive(type);
                mesh = probe.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(probe);
            }
            meshes[type] = mesh;
            return mesh;
        }
    }
}
