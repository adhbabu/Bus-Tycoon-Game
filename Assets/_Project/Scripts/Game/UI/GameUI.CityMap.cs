using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Urban;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Harita sekmesinin iki görünümü: <b>şehir</b> (merkez şehrin gerçek sokak haritası, duraklar ve ünlü mekânlar;
    /// harita sekmesi bununla açılır) ve <b>Türkiye</b> (ülke haritası, şehirlerarası hatlar). Sağ paneldeki
    /// "Şehir | Türkiye" geçişi ikisi arasında geçirir. Kamera her görünümde kendi alanında gezer (<see cref="MapCamera.SetArea"/>);
    /// görünüm değişince kamera o alanda en son bırakıldığı yere döner.
    /// </summary>
    public sealed partial class GameUI
    {
        CityView cityView;
        /// <summary>Oyuncu şehir görünümünü mü seçti (harita hazır değilse ülke haritası gösterilir).</summary>
        bool cityMapWanted = true;
        bool cameraInCity;
        Vector3 cityCameraFocus;
        float cityCameraDistance = -1f;
        Vector3 countryCameraFocus;
        float countryCameraDistance = 34f;

        readonly List<UrbanLandmark> urbanLabelLandmarks = new List<UrbanLandmark>();
        readonly List<UrbanStop> urbanLabelStops = new List<UrbanStop>();
        UrbanMap urbanLabelsFor;

        /// <summary>Şu an şehir haritası mı gösteriliyor (oyunda, harita sekmesinde, harita hazır).</summary>
        bool CityMapShown => cityView != null && cameraInCity && mode == Mode.Playing;

        /// <summary>Merkez şehrin sokak haritası var mı (yoksa harita sekmesi yalnız ülke haritasıdır).</summary>
        bool HasCityMap => NewUi && mode == Mode.Playing && CityView.Supports(CurrentCity);

        void InitCityView()
        {
            cityView = new GameObject("CityView").AddComponent<CityView>();
            cityView.Init(mapCamera);
        }

        /// <summary>Harita sekmesinde şehir/Türkiye seçimi (sağ panel düğmeleri).</summary>
        void SelectCityMap(bool city)
        {
            cityMapWanted = city;
            // Planlayıcı haritasına bağlı: şehirlerarası planlayıcı ülke, dolmuş planlayıcısı şehir haritasında
            if (panel == Panel.Planner && city != urbanPlanning) ClosePanel();
        }

        /// <summary>
        /// Her kare: merkez şehrin haritasını önceden yükler (ana ekran açıkken iner, harita sekmesi beklemeden açılır)
        /// ve kamerayı istenen görünümün alanına taşır. Durak içindeyken kameraya dokunulmaz.
        /// </summary>
        void UpdateCityMap()
        {
            if (cityView == null) return;
            bool playing = mode == Mode.Playing && NewUi; // şehir haritası yeni arayüzde (eski arayüz yalnız ülke haritası)
            CityData hq = playing ? CurrentCity : null; // haritada gösterilen lisanslı şehir
            if (hq != null && CityView.Supports(hq) && cityView.City != hq) cityView.Load(hq);

            // Şehirlerarası planlayıcı ülke haritasında açılır
            bool wantCity = playing && cityMapWanted && (panel != Panel.Planner || urbanPlanning) && cityView.Ready && cityView.City == hq;
            if (cityView.Ready) SyncCityRoutes();
            cityView.Paused = session.Clock == null || session.Clock.IsPaused;
            if (StationUi || StationBusy) return;

            if (wantCity != cameraInCity)
            {
                if (wantCity) EnterCityCamera();
                else LeaveCityCamera();
            }
            else if (cameraInCity && mapCamera.InCountryArea)
            {
                EnterCityCamera(); // durak sahnesi ya da başka bir odaklanma kamerayı ülke haritasına götürdüyse geri al
            }
            cityView.SetVisible(cameraInCity && MapShown);
        }

        void EnterCityCamera()
        {
            if (!cameraInCity && mapCamera.InCountryArea)
            {
                countryCameraFocus = CameraFocus();
                countryCameraDistance = mapCamera.Distance;
            }
            cameraInCity = true;
            Rect card = NewUi ? MapCardRect : new Rect(0f, 0f, W, H);
            float fit = cityView.FitDistance(card.width / Mathf.Max(1f, card.height));
            if (cityCameraDistance < 0f)
            {
                cityCameraFocus = cityView.Center;
                cityCameraDistance = fit * 0.55f; // açılışta şehrin merkezi, biraz yakından
            }
            mapCamera.SetArea(cityView.WorldBounds, CityView.MinCameraDistance, fit, cityCameraFocus, cityCameraDistance);
            hoveredCity = null;
        }

        void LeaveCityCamera()
        {
            if (cameraInCity && !mapCamera.InCountryArea)
            {
                cityCameraFocus = CameraFocus();
                cityCameraDistance = mapCamera.Distance;
            }
            cameraInCity = false;
            cityView.SetVisible(false);
            if (!mapCamera.InCountryArea)
            {
                Vector3 focus = countryCameraFocus;
                if (focus == Vector3.zero && session.Headquarters != null) focus = world.GetCityPosition(session.Headquarters);
                mapCamera.SetCountryArea(focus, countryCameraDistance);
            }
        }

        /// <summary>Yeni oyun ya da ana menü: şehir kamerası sıfırlanır (yeni şehir, yeni başlangıç noktası).</summary>
        void ResetCityMap()
        {
            cityMapWanted = true;
            cityCameraDistance = -1f;
            cityMapCityId = null;
            countryCameraFocus = Vector3.zero;
            countryCameraDistance = 34f;
            if (cityView != null && cameraInCity) LeaveCityCamera();
        }

        /// <summary>Kameranın baktığı yer (harita kartının ortası).</summary>
        Vector3 CameraFocus()
        {
            Vector2 center = MapCardCenterScreen;
            return mapCamera.TryGetGroundPoint(center, out Vector3 point) ? point : Vector3.zero;
        }

        /// <summary>Şehir görünümünde merkez düğmesi: şehrin ortası; ülke görünümünde merkez ofis.</summary>
        bool FocusCityCenter()
        {
            if (!CityMapShown) return false;
            mapCamera.FocusOn(cityView.Center, cityView.FitDistance(MapCardRect.width / Mathf.Max(1f, MapCardRect.height)) * 0.55f);
            return true;
        }

        /// <summary>
        /// Şehir görünümünde dokunuş: planlayıcı açıksa durak seçilir; değilse dokunulan duraktan yeni hat başlar.
        /// </summary>
        bool OnCityMapClick(Vector2 guiPoint)
        {
            if (!CityMapShown) return false;
            if (!cityView.TryPickStop(mapCamera.Camera, GuiToScreen(guiPoint), PickRadius, out UrbanStop stop)) return true;
            if (UrbanPlannerOpen) UrbanStopClicked(stop);
            else if (panel == Panel.None) OpenUrbanPlanner(stop);
            return true;
        }

        // ------------------------------------------------------------------ şehir haritası etiketleri

        /// <summary>
        /// Mekân ve durak adları: önce ünlü mekânlar (ünden düşüğe), sonra duraklar (önemden düşüğe). Yakınlaştıkça daha
        /// çok ad görünür; çakışan ad çizilmez.
        /// </summary>
        void DrawUrbanLabels()
        {
            UrbanMap map = cityView.Map;
            if (map == null) return;
            if (urbanLabelsFor != map)
            {
                urbanLabelsFor = map;
                urbanLabelLandmarks.Clear();
                urbanLabelLandmarks.AddRange(map.Landmarks);
                urbanLabelLandmarks.Sort((a, b) => b.Fame.CompareTo(a.Fame));
                urbanLabelStops.Clear();
                urbanLabelStops.AddRange(map.Stops);
                urbanLabelStops.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            }

            Camera cam = mapCamera.Camera;
            float distance = mapCamera.Distance;
            Rect card = NewUi ? MapCardRect : new Rect(0f, SafeTop + TopBarHeight, W, H);
            placedLabels.Clear();

            // Ünlü mekânlar: uzakta yalnız en ünlüleri
            float fameLimit = distance < 40f ? 0f : distance < 100f ? 0.6f : distance < 260f ? 0.85f : 2f;
            foreach (UrbanLandmark landmark in urbanLabelLandmarks)
            {
                if (landmark.Fame < fameLimit) break;
                DrawUrbanLabel(cam, card, cityView.ToWorld(landmark.Position), landmark.Name, s.MapLabel, 10f * u);
            }

            // Duraklar: uzakta ilçe merkezleri, yakında hepsi
            float weightLimit = distance < 40f ? 0f : distance < 100f ? 0.45f : distance < 260f ? 0.7f : 0.85f;
            foreach (UrbanStop stop in urbanLabelStops)
            {
                if (stop.Weight < weightLimit) break;
                DrawUrbanLabel(cam, card, cityView.ToWorld(stop.Position), stop.Name, stop.Weight >= 0.75f ? s.MapLabelBold : s.MapLabel, 12f * u);
            }
        }

        void DrawUrbanLabel(Camera cam, Rect card, Vector3 worldPoint, string text, GUIStyle style, float lift)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldPoint);
            if (sp.z <= 0f) return;
            var p = new Vector2(sp.x, Screen.height - sp.y - lift);
            if (p.y - 24f * u < card.y || p.y > card.yMax || p.x < card.x + 30f * u || p.x > card.xMax - 30f * u) return;
            if (NewUi && ToolkitCovers(new Vector2(p.x, p.y - 12f * u))) return;

            LabelContent.text = text;
            float width = style.CalcSize(LabelContent).x;
            var box = new Rect(p.x - width * 0.5f - 3f * u, p.y - 22f * u, width + 6f * u, 20f * u);
            if (OverlapsPlacedLabel(box)) return;
            placedLabels.Add(box);

            var rect = new Rect(p.x - 110f * u, p.y - 22f * u, 220f * u, 20f * u);
            GUIStyle halo = style == s.MapLabelBold ? s.MapLabelBoldShadow : s.MapLabelShadow;
            GUI.Label(new Rect(rect.x + u, rect.y, rect.width, rect.height), text, halo);
            GUI.Label(new Rect(rect.x - u, rect.y, rect.width, rect.height), text, halo);
            GUI.Label(new Rect(rect.x, rect.y + u, rect.width, rect.height), text, halo);
            GUI.Label(new Rect(rect.x, rect.y - u, rect.width, rect.height), text, halo);
            GUI.Label(rect, text, style);
        }
    }
}
