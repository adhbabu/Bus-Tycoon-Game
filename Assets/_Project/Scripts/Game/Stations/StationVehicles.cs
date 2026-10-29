using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Durak sahnesindeki araçlar (metre ölçeğinde): perondaki otobüsler (filodaki modellerin segment renkleriyle:
    /// minibüs, midibüs, şehirlerarası, VIP, çift katlı), taksiler ve yoldan geçen arabalar. Burun yerel +z.
    /// </summary>
    public static class StationVehicles
    {
        static readonly Color Glass = new Color(0.16f, 0.22f, 0.3f);
        static readonly Color DarkGlass = new Color(0.08f, 0.1f, 0.14f);
        static readonly Color Tire = new Color(0.1f, 0.1f, 0.11f);
        static readonly Color Hub = new Color(0.75f, 0.76f, 0.8f);
        static readonly Color Skirt = new Color(0.22f, 0.22f, 0.25f);
        static readonly Color Headlight = new Color(1f, 0.95f, 0.7f);
        static readonly Color Taillight = new Color(0.95f, 0.15f, 0.12f);
        static readonly Color Display = new Color(1f, 0.6f, 0.1f);
        static readonly Color[] StripeColors =
        {
            new Color(1f, 0.57f, 0f), Color.white, new Color(1f, 0.57f, 0f), new Color(1f, 0.77f, 0f), new Color(0.2f, 0.2f, 0.25f), Color.white
        };
        static readonly Color[] CarColors =
        {
            new Color(0.85f, 0.2f, 0.2f), new Color(0.25f, 0.45f, 0.85f), new Color(0.95f, 0.95f, 0.93f), new Color(0.3f, 0.32f, 0.36f),
            new Color(0.3f, 0.7f, 0.45f), new Color(0.95f, 0.75f, 0.2f)
        };

        static readonly Dictionary<int, (Mesh mesh, MeshLayer[] layers)> buses = new Dictionary<int, (Mesh, MeshLayer[])>();
        static readonly Dictionary<int, (Mesh mesh, MeshLayer[] layers)> cars = new Dictionary<int, (Mesh, MeshLayer[])>();
        static (Mesh mesh, MeshLayer[] layers) taxi;

        /// <summary>Modelin segmentine göre otobüs (segment: BusModelData.Tier, 0 minibüs … 5 çift katlı).</summary>
        public static Mesh Bus(BusModelData model, out MeshLayer[] layers) => Bus(model != null ? Mathf.Clamp(model.Tier, 0, 5) : 2, out layers);

        public static float BusLength(int segment) => segment == 0 ? 7.4f : segment == 1 ? 9.2f : segment == 5 ? 12.8f : 12.2f;

        public static Mesh Bus(int segment, out MeshLayer[] layers)
        {
            if (!buses.TryGetValue(segment, out var entry) || entry.mesh == null)
            {
                entry.mesh = BuildBus(segment, out entry.layers);
                buses[segment] = entry;
            }
            layers = entry.layers;
            return entry.mesh;
        }

        public static Mesh Car(int variant, out MeshLayer[] layers)
        {
            variant = (variant % CarColors.Length + CarColors.Length) % CarColors.Length;
            if (!cars.TryGetValue(variant, out var entry) || entry.mesh == null)
            {
                var k = new MeshKit();
                BuildCar(k, CarColors[variant], false);
                entry.mesh = k.Build("Car_" + variant, out entry.layers);
                cars[variant] = entry;
            }
            layers = entry.layers;
            return entry.mesh;
        }

        public static Mesh Taxi(out MeshLayer[] layers)
        {
            if (taxi.mesh == null)
            {
                var k = new MeshKit();
                BuildCar(k, new Color(1f, 0.8f, 0.1f), true);
                taxi.mesh = k.Build("Taxi", out taxi.layers);
            }
            layers = taxi.layers;
            return taxi.mesh;
        }

        static Mesh BuildBus(int segment, out MeshLayer[] layers)
        {
            var k = new MeshKit();
            Color body = segment < BusModels.SegmentColors.Length ? BusModels.SegmentColors[segment] : Color.white;
            Color stripe = StripeColors[Mathf.Clamp(segment, 0, StripeColors.Length - 1)];
            bool van = segment == 0, doubleDeck = segment == 5, vip = segment == 4;
            float length = BusLength(segment);
            float width = van ? 2.1f : 2.55f;
            float height = van ? 2.5f : doubleDeck ? 4.1f : 3.4f;
            float floor = 0.55f, front = length / 2f, rear = -length / 2f;
            float cabinFront = van ? front - 1.3f : front;

            // Gövde ve etek
            k.Box(new Vector3(0f, floor + height / 2f, (rear + cabinFront) / 2f), new Vector3(width, height, cabinFront - rear), body);
            if (van) k.Box(new Vector3(0f, floor + 0.55f, (cabinFront + front) / 2f), new Vector3(width * 0.96f, 1.1f, front - cabinFront), body);
            k.Box(new Vector3(0f, floor + 0.2f, 0f), new Vector3(width + 0.04f, 0.4f, length * 0.97f), Skirt);
            k.Box(new Vector3(0f, floor + height * (doubleDeck ? 0.55f : 0.4f), 0f), new Vector3(width + 0.05f, 0.22f, length * 0.96f), stripe);

            // Pencereler
            float[] rows = doubleDeck ? new[] { floor + 1.5f, floor + 3.2f } : new[] { floor + height * 0.68f };
            float rowHeight = doubleDeck ? 0.8f : van ? 0.75f : 1.05f;
            float windowFront = van ? cabinFront - 0.2f : front - 1.6f;
            foreach (float y in rows)
                k.Box(new Vector3(0f, y, (windowFront + rear + 0.5f) / 2f), new Vector3(width + 0.04f, rowHeight, windowFront - rear - 0.5f), vip ? DarkGlass : Glass);
            // Ön cam, kapı, arka cam
            if (van) k.Box(new Vector3(0f, floor + 1.55f, cabinFront + 0.03f), new Vector3(width * 0.9f, 0.85f, 0.06f), Glass);
            else k.Box(new Vector3(0f, floor + height * (doubleDeck ? 0.4f : 0.6f), front + 0.03f), new Vector3(width * 0.9f, doubleDeck ? 1.3f : 1.7f, 0.06f), Glass);
            if (doubleDeck) k.Box(new Vector3(0f, floor + 3.2f, front + 0.03f), new Vector3(width * 0.9f, 0.8f, 0.06f), Glass);
            k.Box(new Vector3(width / 2f + 0.03f, floor + 1.25f, van ? cabinFront - 0.9f : front - 1.0f), new Vector3(0.06f, 2.2f, 1.0f), DarkGlass);
            k.Box(new Vector3(0f, floor + height * 0.72f, rear - 0.03f), new Vector3(width * 0.8f, 0.8f, 0.06f), DarkGlass);

            // Tavan ve klima
            k.Box(new Vector3(0f, floor + height + 0.05f, (rear + cabinFront) / 2f), new Vector3(width * 0.95f, 0.1f, (cabinFront - rear) * 0.98f), Color.white);
            if (!van) k.Box(new Vector3(0f, floor + height + 0.22f, -length * 0.05f), new Vector3(width * 0.6f, 0.3f, length * 0.25f), new Color(0.8f, 0.82f, 0.86f));

            // Işıklar ve hat göstergesi (gece parlar)
            k.Layer = MeshLayer.Glow;
            foreach (float side in new[] { -1f, 1f })
            {
                k.Box(new Vector3(side * width * 0.32f, floor + 0.5f, front + 0.04f), new Vector3(0.45f, 0.18f, 0.04f), Headlight);
                k.Box(new Vector3(side * width * 0.38f, floor + 0.9f, rear - 0.04f), new Vector3(0.25f, 0.4f, 0.04f), Taillight);
            }
            if (!van) k.Box(new Vector3(0f, floor + height * (doubleDeck ? 0.98f : 0.93f), front + 0.05f), new Vector3(width * 0.6f, 0.22f, 0.04f), Display);
            k.Layer = MeshLayer.Lit;

            // Aynalar ve tekerlekler
            foreach (float side in new[] { -1f, 1f })
                k.Box(new Vector3(side * (width / 2f + 0.25f), floor + height * 0.7f, front - 0.2f), new Vector3(0.12f, 0.4f, 0.12f), Skirt);
            float wheel = van ? 0.36f : 0.5f;
            float[] axles = van ? new[] { -length * 0.3f, length * 0.32f } : segment >= 4 ? new[] { -length * 0.33f, -length * 0.22f, length * 0.3f } : new[] { -length * 0.3f, length * 0.3f };
            foreach (float z in axles)
                foreach (float side in new[] { -1f, 1f })
                {
                    k.Push(new Vector3(side * (width / 2f - 0.12f), wheel, z), 0f);
                    k.CylinderX(Vector3.zero, wheel, 0.32f, Tire, 10);
                    k.CylinderX(new Vector3(side * 0.17f, 0f, 0f), wheel * 0.55f, 0.02f, Hub, 8);
                    k.Pop();
                }
            return k.Build("StationBus_" + segment, out layers);
        }

        static void BuildCar(MeshKit k, Color body, bool isTaxi)
        {
            k.Box(new Vector3(0f, 0.55f, 0f), new Vector3(1.75f, 0.6f, 4.2f), body);
            k.Box(new Vector3(0f, 1.1f, -0.25f), new Vector3(1.55f, 0.55f, 2.1f), body);
            k.Box(new Vector3(0f, 1.1f, -0.25f), new Vector3(1.58f, 0.42f, 1.9f), Glass);
            k.Box(new Vector3(0f, 1.05f, 0.83f), new Vector3(1.45f, 0.42f, 0.06f), Glass);
            k.Layer = MeshLayer.Glow;
            foreach (float side in new[] { -1f, 1f })
            {
                k.Box(new Vector3(side * 0.6f, 0.62f, 2.11f), new Vector3(0.35f, 0.14f, 0.03f), Headlight);
                k.Box(new Vector3(side * 0.65f, 0.65f, -2.11f), new Vector3(0.3f, 0.14f, 0.03f), Taillight);
            }
            if (isTaxi) k.Box(new Vector3(0f, 1.47f, -0.3f), new Vector3(0.7f, 0.2f, 0.3f), new Color(1f, 0.95f, 0.6f));
            k.Layer = MeshLayer.Lit;
            foreach (float z in new[] { -1.35f, 1.35f })
                foreach (float side in new[] { -1f, 1f })
                    k.CylinderX(new Vector3(side * 0.82f, 0.33f, z), 0.33f, 0.24f, Tire, 8);
            if (isTaxi)
            {
                k.Box(new Vector3(0.9f, 0.6f, 0f), new Vector3(0.02f, 0.12f, 3.6f), new Color(0.1f, 0.1f, 0.1f));
                k.Box(new Vector3(-0.9f, 0.6f, 0f), new Vector3(0.02f, 0.12f, 3.6f), new Color(0.1f, 0.1f, 0.1f));
            }
        }
    }
}
