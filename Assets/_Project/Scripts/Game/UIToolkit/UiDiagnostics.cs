using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüz tanılaması (<c>?uidiag=1</c>; yalnız ölçüm içindir). UI Toolkit, çizimden hemen önce kirli
    /// (yeniden çizilecek) öğeleri işler ve WebGL'de yalnız onların köşe aralıklarını GPU'ya yükler (Unity 6000.5+).
    /// Bu sınıf, PlayerLoop'ta çizimden hemen önce ağacı dolaşır ve kirli öğeleri sayar. İki saniyede bir konsola
    /// en sık kirlenen öğeleri yazar (sınıfları, yazısı, kirlilik türü). İç alanlara yansımayla erişilir; erişilemezse
    /// bunu bir kez yazar.
    /// </summary>
    public static class UiDiagnostics
    {
        struct Probe { }

        static VisualElement root;
        static FieldInfo renderDataField, dirtyField;
        static bool ready, failed;
        static int frames, dirtyFrames, dirtyElements;
        static float windowStart;
        static readonly Dictionary<string, int> hits = new Dictionary<string, int>();

        public static bool Enabled => LaunchOptions.Has("uidiag");

        public static void Install(VisualElement panelRoot)
        {
            if (!Enabled || root != null) return;
            root = panelRoot;
            renderDataField = typeof(VisualElement).GetField("renderData", BindingFlags.Instance | BindingFlags.NonPublic)
                              ?? typeof(VisualElement).GetField("renderChainData", BindingFlags.Instance | BindingFlags.NonPublic);
            if (renderDataField != null)
                dirtyField = renderDataField.FieldType.GetField("dirtiedValues", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ready = renderDataField != null && dirtyField != null;
            Debug.Log($"[UIDiag] alan: {renderDataField?.Name ?? "yok"}.{dirtyField?.Name ?? "yok"}");

            // Çizimden hemen önce çalışan bir adım: PostLateUpdate'te UI Toolkit'in çizim adımının önüne
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            string place = "bulunamadı";
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type == null || loop.subSystemList[i].type.Name != "PostLateUpdate") continue;
                var list = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList);
                int index = list.FindIndex(s => s.type != null && s.type.Name.Contains("UIElements"));
                if (index < 0) index = 0;
                place = index < list.Count ? list[index].type?.Name ?? "?" : "son";
                list.Insert(index, new PlayerLoopSystem { type = typeof(Probe), updateDelegate = Sample });
                loop.subSystemList[i].subSystemList = list.ToArray();
            }
            PlayerLoop.SetPlayerLoop(loop);
            Debug.Log($"[UIDiag] örnekleme {place} adımından önce");
            windowStart = Time.unscaledTime;
        }

        static void Sample()
        {
            if (root == null || failed) return;
            frames++;
            int dirty = 0;
            if (ready)
            {
                try { dirty = Walk(root); }
                catch (Exception e)
                {
                    failed = true;
                    Debug.Log("[UIDiag] okunamadı: " + e.Message);
                }
            }
            if (dirty > 0) dirtyFrames++;
            dirtyElements += dirty;

            if (Time.unscaledTime - windowStart < 2f) return;
            var top = new List<KeyValuePair<string, int>>(hits);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var text = new StringBuilder();
            text.Append($"[UIDiag] {frames} kare, kirli kare {dirtyFrames}, kare başı kirli öğe {(float)dirtyElements / Mathf.Max(1, frames):0.0}");
            for (int i = 0; i < Mathf.Min(8, top.Count); i++) text.Append($"\n   {top[i].Value}× {top[i].Key}");
            Debug.Log(text.ToString());
            frames = dirtyFrames = dirtyElements = 0;
            hits.Clear();
            windowStart = Time.unscaledTime;
        }

        static int Walk(VisualElement element)
        {
            int count = 0;
            object data = renderDataField.GetValue(element);
            if (data != null)
            {
                int flags = Convert.ToInt32(dirtyField.GetValue(data));
                if (flags != 0)
                {
                    count++;
                    string key = Describe(element) + " [" + flags + "]";
                    hits.TryGetValue(key, out int n);
                    hits[key] = n + 1;
                }
            }
            int children = element.hierarchy.childCount;
            for (int i = 0; i < children; i++) count += Walk(element.hierarchy[i]);
            return count;
        }

        static string Describe(VisualElement element)
        {
            var text = new StringBuilder(element.GetType().Name);
            foreach (string c in element.GetClasses()) text.Append('.').Append(c);
            if (element is TextElement t && !string.IsNullOrEmpty(t.text))
                text.Append(" \"").Append(t.text.Length > 24 ? t.text.Substring(0, 24) : t.text).Append('"');
            if (element.parent != null)
            {
                text.Append(" < ").Append(element.parent.GetType().Name);
                foreach (string c in element.parent.GetClasses()) text.Append('.').Append(c);
            }
            return text.ToString();
        }
    }
}
