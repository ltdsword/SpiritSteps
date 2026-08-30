using System;
using System.Collections.Generic;

namespace ARWalking.UI
{
    public static class UiRouteCatalog
    {
        public static readonly IReadOnlyList<UiRoute> All = Array.AsReadOnly(new[]
        {
            UiRoute.OnboardingPermissions,
            UiRoute.HomeMap,
            UiRoute.ActiveWalk,
            UiRoute.WalkSummary,
            UiRoute.SpiritCollection,
            UiRoute.SpiritDetail,
            UiRoute.SeedlingGrowth,
            UiRoute.HatchReveal,
            UiRoute.ArCompanion,
            UiRoute.ArPhoto,
            UiRoute.LandmarkMemory,
            UiRoute.JourneyJournal,
            UiRoute.JourneyDetail
        });

        public static UiRoute RootRoute(UiRootTab root)
        {
            switch (root)
            {
                case UiRootTab.Map: return UiRoute.HomeMap;
                case UiRootTab.Garden: return UiRoute.SeedlingGrowth;
                case UiRootTab.WalkAr: return UiRoute.ArCompanion;
                case UiRootTab.Journal: return UiRoute.JourneyJournal;
                case UiRootTab.Book: return UiRoute.SpiritCollection;
                default: throw new ArgumentOutOfRangeException(nameof(root), root, null);
            }
        }

        public static UiRootTab RootFor(UiRoute route)
        {
            switch (route)
            {
                case UiRoute.SeedlingGrowth:
                case UiRoute.HatchReveal:
                    return UiRootTab.Garden;
                case UiRoute.ArCompanion:
                case UiRoute.ArPhoto:
                    return UiRootTab.WalkAr;
                case UiRoute.JourneyJournal:
                case UiRoute.JourneyDetail:
                    return UiRootTab.Journal;
                case UiRoute.SpiritCollection:
                case UiRoute.SpiritDetail:
                    return UiRootTab.Book;
                default:
                    return UiRootTab.Map;
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
            if (route == CurrentRoute)
                return;
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

            if (_backStack.Count == 0)
                return false;

            CurrentRoute = _backStack.Pop();
            CurrentRoot = UiRouteCatalog.RootFor(CurrentRoute);
            Changed?.Invoke();
            return true;
        }

        public void ShowOverlay(UiOverlay overlay)
        {
            CurrentOverlay = overlay;
            Changed?.Invoke();
        }

        public void CloseOverlay()
        {
            if (!CurrentOverlay.HasValue)
                return;
            CurrentOverlay = null;
            Changed?.Invoke();
        }
    }
}
