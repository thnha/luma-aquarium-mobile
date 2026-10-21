using System;
using UnityEngine;

namespace Lumaquarium.UI
{
    public static class FrontEndScenes
    {
        public const string Start = "StartGame";
        public const string Login = "LoginGame";
        public const string Loading = "LoadingGame";
        public const string Aquarium = "MainAquarium";
        public const string GuestIdKey = "Lumaquarium.Profile.GuestId";

        public static bool HasGuestProfile => !string.IsNullOrEmpty(PlayerPrefs.GetString(GuestIdKey));

        // A local guest profile, not an authenticated account. An existing ID is kept.
        public static void EnsureGuestProfile()
        {
            if (HasGuestProfile) return;
            PlayerPrefs.SetString(GuestIdKey, Guid.NewGuid().ToString("N"));
            PlayerPrefs.Save();
        }
    }
}
