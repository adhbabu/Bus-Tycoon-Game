using System.Collections.Generic;
using System.IO;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Haritadaki 3B araç örgüleri (Resources/BusTycoon/Models/vehicles.bytes): SketchUp'ta kurulan araç modellerinin az
    /// çokgenli sürümü (Tools/vehicles/export_unity.py). Konumlar metre, Unity ekseni (+x sağ: kapı tarafı, +y yukarı,
    /// +z ileri), taban y=0. Her köşenin bir malzeme yuvası var; renk ya da palet yuvasına kullanan taraf çevirir.
    /// Dosya yoksa ya da bozuksa <see cref="Get"/> null döner; çağıranlar eski kodla üretilen modellere düşer.
    /// </summary>
    public static class VehicleMeshes
    {
        /// <summary>Malzeme yuvaları (export_unity.py → SLOTS ile aynı sıra).</summary>
        public enum Slot : byte { Body, Stripe, Glass, DarkGlass, Frame, Tire, Hub, Trim, Arch, Headlight, Indicator, Taillight, Led, RoofUnit }

        public sealed class Model
        {
            public string Id;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Slot[] Slots;
            public int[] Triangles;
            /// <summary>Baştaki bu kadar köşe gövde (boya rengi değişebilir).</summary>
            public int BodyVertexCount;
            public Bounds Bounds;
        }

        static Dictionary<string, Model> models;

        /// <summary>Yuvaların renkleri (köşe renkli kullanım: şehir haritası dolmuşları). Gövde ve şerit çağırana kalır.</summary>
        public static Color SlotColor(Slot slot)
        {
            switch (slot)
            {
                case Slot.Glass: return new Color(0.15f, 0.21f, 0.29f);
                case Slot.DarkGlass: return new Color(0.08f, 0.1f, 0.13f);
                case Slot.Frame: return new Color(0.09f, 0.1f, 0.11f);
                case Slot.Tire: return new Color(0.11f, 0.11f, 0.12f);
                case Slot.Hub: return new Color(0.72f, 0.74f, 0.78f);
                case Slot.Trim: return new Color(0.2f, 0.21f, 0.24f);
                case Slot.Arch: return new Color(0.06f, 0.06f, 0.07f);
                case Slot.Headlight: return new Color(0.96f, 0.97f, 1f);
                case Slot.Indicator: return new Color(1f, 0.65f, 0.16f);
                case Slot.Taillight: return new Color(0.86f, 0.14f, 0.14f);
                case Slot.Led: return new Color(1f, 0.6f, 0.12f);
                case Slot.RoofUnit: return new Color(0.87f, 0.89f, 0.91f);
                case Slot.Stripe: return Color.white;
                default: return Color.white;
            }
        }

        /// <summary>Modelin örgüsü; bilinmeyen kimlik ya da dosya yoksa null.</summary>
        public static Model Get(string id)
        {
            if (models == null) models = Load();
            return id != null && models.TryGetValue(id, out Model model) ? model : null;
        }

        static Dictionary<string, Model> Load()
        {
            var asset = Resources.Load<TextAsset>(DefaultContent.VehicleMeshesResourcePath);
            if (asset == null) return new Dictionary<string, Model>();
            try
            {
                return Parse(asset.bytes);
            }
            catch (System.Exception e) when (e is EndOfStreamException || e is IOException || e is InvalidDataException || e is System.ArgumentException)
            {
                Debug.LogWarning("[Araçlar] Araç örgüleri okunamadı: " + e.Message);
                return new Dictionary<string, Model>();
            }
        }

        /// <summary>vehicles.bytes çözümleyicisi (biçim: Tools/vehicles/export_unity.py).</summary>
        public static Dictionary<string, Model> Parse(byte[] data)
        {
            var result = new Dictionary<string, Model>();
            if (data == null || data.Length < 8) return result;
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (reader.ReadByte() != 'B' || reader.ReadByte() != 'T' || reader.ReadByte() != 'V' || reader.ReadByte() != '1')
                    throw new InvalidDataException("başlık tanınmadı");
                int count = reader.ReadInt32();
                if (count < 0 || count > 256) throw new InvalidDataException("model sayısı geçersiz");
                for (int m = 0; m < count; m++)
                {
                    int nameLength = reader.ReadByte();
                    string id = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
                    int vertexCount = reader.ReadInt32(), indexCount = reader.ReadInt32();
                    if (vertexCount <= 0 || vertexCount > 65535 || indexCount <= 0 || indexCount % 3 != 0)
                        throw new InvalidDataException(id + ": boyut geçersiz");
                    var model = new Model
                    {
                        Id = id,
                        Positions = new Vector3[vertexCount],
                        Normals = new Vector3[vertexCount],
                        Slots = new Slot[vertexCount],
                        Triangles = new int[indexCount]
                    };
                    Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                    bool body = true;
                    for (int v = 0; v < vertexCount; v++)
                    {
                        var p = new Vector3(reader.ReadInt16() * 0.001f, reader.ReadInt16() * 0.001f, reader.ReadInt16() * 0.001f);
                        var n = new Vector3(reader.ReadSByte(), reader.ReadSByte(), reader.ReadSByte());
                        byte slot = reader.ReadByte();
                        if (slot > (byte)Slot.RoofUnit) throw new InvalidDataException(id + ": malzeme yuvası geçersiz");
                        model.Positions[v] = p;
                        model.Normals[v] = n.sqrMagnitude > 0f ? n.normalized : Vector3.up;
                        model.Slots[v] = (Slot)slot;
                        if (body && slot == (byte)Slot.Body) model.BodyVertexCount = v + 1;
                        else body = false;
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                    }
                    for (int i = 0; i < indexCount; i++)
                    {
                        int index = reader.ReadUInt16();
                        if (index >= vertexCount) throw new InvalidDataException(id + ": üçgen indeksi taşıyor");
                        model.Triangles[i] = index;
                    }
                    model.Bounds = new Bounds((min + max) * 0.5f, max - min);
                    result[id] = model;
                }
            }
            return result;
        }
    }
}
