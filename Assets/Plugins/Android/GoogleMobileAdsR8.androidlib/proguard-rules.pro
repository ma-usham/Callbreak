# An R8 rule for the Google Mobile Ads Unity plugin (com.google.ads.mobile 11.5.0), applied to every
# minified (R8) build of the app as this library's consumer rules.
#
# The plugin's googlemobileads-unity.aar carries wrappers (com.google.unity.ads.nextgen.*) for the GMA
# Next-Gen SDK (com.google.android.libraries.ads.mobile.sdk.*). The game doesn't use that SDK and
# doesn't include it: its ads run on play-services-ads, through com.google.unity.ads.*. Without this
# rule R8 stops the release build: "Missing classes detected while running R8 ... Missing class
# com.google.android.libraries.ads.mobile.sdk.MobileAds (referenced from ...
# com.google.unity.ads.nextgen.MobileAdsWrapper ...)". That was the owner's bundleRelease, 5 Oct
# 2026; all 46 missing classes Gradle listed in missing_rules.txt are in that one package. With the
# rule, the same build's minifyReleaseWithR8 succeeds.
#
# Drop it once the plugin stops shipping the next-gen wrappers, or the game moves to that SDK.
-dontwarn com.google.android.libraries.ads.mobile.sdk.**
