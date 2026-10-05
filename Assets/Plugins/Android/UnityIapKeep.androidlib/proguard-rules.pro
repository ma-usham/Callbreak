# Keep rules for Unity IAP (com.unity.purchasing 5.4.4) on Google Play, applied to every minified
# (R8) build of the app as this library's consumer rules.
#
# Source: Unity IAP drives the Google Play Billing Library (com.android.billingclient:billing:9.0.0,
# added to the Gradle build by the package's Plugins/UnityPurchasing/Android/IAPResolver/
# IAPAndroidDependencies.cs) only from C#, through JNI by name: AndroidJavaClass/AndroidJavaObject in
# Runtime/Stores/Android/GooglePlay/AAR/Models/BillingClient.cs and the converters beside it, and its
# listener interfaces (PurchasesUpdatedListener, BillingClientStateListener, ...) implemented with
# AndroidJavaProxy in Runtime/Stores/Android/GooglePlay/AAR/Listeners. R8 sees no Java caller of
# those classes, so without a keep rule it may strip or rename them, and IAP fails at runtime
# (IAP's 2.2.2 changelog: "ClassNotFoundException: com.android.billingclient.api.
# PurchasesUpdatedListener" with minification). Since 5.x the package ships no AAR and no keep rules
# of its own; Unity's proguard-unity.txt keeps nothing of the Billing Library.
#
# One rule for the whole public API package, so a newer IAP that calls more of it stays covered.
-keep class com.android.billingclient.api.** { *; }
