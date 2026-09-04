using System;
using System.Collections.Generic;

namespace ARWalking.UI
{
    public static class UiRouteCatalog
    {
        public static readonly IReadOnlyList<UiRoute> All = Array.AsReadOnly(new[]
        {
            UiRoute.OnboardingSetup, UiRoute.HomeMap, UiRoute.ActiveWalk, UiRoute.WalkResult,
            UiRoute.CompanionCollection, UiRoute.CompanionDetail, UiRoute.ShopFood,
            UiRoute.LandmarkDetail, UiRoute.PetAr,
            UiRoute.JourneyList, UiRoute.JourneyDetail
        });

        public static UiRoute RootRoute(UiRootTab root)
        {
            switch (root)
            {
                case UiRootTab.Map: return UiRoute.HomeMap;
                case UiRootTab.Companions: return UiRoute.CompanionCollection;
                case UiRootTab.Shop: return UiRoute.ShopFood;
                case UiRootTab.Journey: return UiRoute.JourneyList;
                default: throw new ArgumentOutOfRangeException(nameof(root), root, null);
            }
        }

        public static UiRootTab RootFor(UiRoute route)
        {
            switch (route)
            {
                case UiRoute.CompanionCollection:
                case UiRoute.CompanionDetail: return UiRootTab.Companions;
                case UiRoute.ShopFood: return UiRootTab.Shop;
                case UiRoute.JourneyList:
                case UiRoute.JourneyDetail: return UiRootTab.Journey;
                default: return UiRootTab.Map;
            }
        }
    }

    public sealed class UiNavigationStack : IAppNavigator
    {
        readonly Stack<UiRoute> _backStack = new Stack<UiRoute>();
        public UiRoute CurrentRoute { get; private set; } = UiRoute.HomeMap;
        public UiRootTab CurrentRoot { get; private set; } = UiRootTab.Map;
        public UiOverlay? CurrentOverlay { get; private set; }
        public bool CanGoBack => CurrentOverlay.HasValue || _backStack.Count > 0;
        public event Action Changed;

        public void ResetToSetup()
        {
            CurrentRoot = UiRootTab.Map;
            CurrentRoute = UiRoute.OnboardingSetup;
            CurrentOverlay = null;
            _backStack.Clear();
            Changed?.Invoke();
        }

        public void SwitchRoot(UiRootTab root)
        {
            CurrentRoot = root;
            CurrentRoute = UiRouteCatalog.RootRoute(root);
            CurrentOverlay = null;
            _backStack.Clear();
            Changed?.Invoke();
        }

        public void Push(UiRoute route)
        {
            if (route == CurrentRoute) return;
            _backStack.Push(CurrentRoute);
            CurrentRoute = route;
            CurrentRoot = UiRouteCatalog.RootFor(route);
            CurrentOverlay = null;
            Changed?.Invoke();
        }

        public bool Back()
        {
            if (CurrentOverlay.HasValue)
            {
                CurrentOverlay = null;
                Changed?.Invoke();
                return true;
            }
            if (_backStack.Count == 0) return false;
            CurrentRoute = _backStack.Pop();
            CurrentRoot = UiRouteCatalog.RootFor(CurrentRoute);
            Changed?.Invoke();
            return true;
        }

        public void ShowOverlay(UiOverlay overlay) { CurrentOverlay = overlay; Changed?.Invoke(); }
        public void CloseOverlay() { if (!CurrentOverlay.HasValue) return; CurrentOverlay = null; Changed?.Invoke(); }
    }
}
