using BusTycoon.Data;
using BusTycoon.Routes;

namespace BusTycoon.Game
{
    /// <summary>
    /// Mağaza ekran görüntüsü turu (<see cref="ShowcaseDirector"/>) için arayüzü belirli ekranlara getirir.
    /// Kare aralarında (Update/coroutine) çağrılır; OnGUI geçişinin ortasında düzen değişmez.
    /// </summary>
    public sealed partial class GameUI
    {
        internal void ShowcaseMap(CityData focus, float distance)
        {
            ClosePanel();
            mapCamera.FocusOn(world.GetCityPosition(focus), distance);
        }

        internal void ShowcasePlanner(CityData origin, CityData destination)
        {
            ClosePanel();
            OpenPlannerNow(origin);
            PlannerCityClicked(destination);
        }

        internal void ShowcaseRoute(RouteState route)
        {
            ClosePanel();
            OpenRoutePanelNow(route, RouteTab.Price);
        }

        internal void ShowcaseDealership(CityData focus, float distance)
        {
            ShowcaseMap(focus, distance);
            OpenPanelNow(Panel.Dealership);
        }

        internal void ShowcaseFinance(CityData focus, float distance)
        {
            ShowcaseMap(focus, distance);
            OpenPanelNow(Panel.Finance);
        }

        /// <summary>Görüntü alınmadan hemen önce: bildirimler ve olası açılır pencereler kapanır.</summary>
        internal void ShowcaseTidy()
        {
            notes.Clear();
            modal = Modal.None;
        }
    }
}
