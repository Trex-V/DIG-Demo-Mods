using System;
using HarmonyLib;
using UnityEngine;

namespace NoPopup;

[HarmonyPatch(typeof(Application), nameof(Application.OpenURL), new[] { typeof(string) })]
internal static class BlockGoogleFormsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(string url)
    {
        // Let every URL through except links recognized as Google Forms.
        if (!IsGoogleFormUrl(url))
        {
            return true;
        }

        Plugin.Log.LogInfo("Blocked a Google Forms browser launch.");
        return false;
    }

    private static bool IsGoogleFormUrl(string url)
    {
        // Parse the host and path rather than matching a substring that could be spoofed.
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            return false;
        }

        string host = uri.Host;
        if (host.Equals("forms.gle", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/forms/", StringComparison.OrdinalIgnoreCase);
    }
}
