# Keep rules for Google's Play In-app Review plugin for Unity, which calls Play's review API only
# through JNI, by name: nothing in the app's Java code refers to it, so R8 would strip or rename it
# in a minified release build, and the game could never show Google Play's review card (ADR 080).
# Unity applies none of the plugin's own rule files, so this module brings them as consumer rules of
# its own; it has no code. Play Core's common rules come with PlayAppUpdateKeep. Copied verbatim:
# refresh it from the package named below whenever that package is upgraded.

# ---- com.google.play.review 1.8.4: Proguard/review.txt ----

# Copyright 2019 Google LLC
#
# The Google Play Core Library is licensed to you under the Play Core Software
# Development Kit Terms of Service
# (https://developer.android.com/guide/playcore/license.html).
# By using the Google Play Core Library, you agree to the Play Core Software
# Development Kit Terms of Service.

# Play Core Proguard Rules: Play In-app Review

-keep class com.google.android.play.core.review.ReviewManager {
  public com.google.android.gms.tasks.Task requestReviewFlow();
  public com.google.android.gms.tasks.Task launchReviewFlow(android.app.Activity, com.google.android.play.core.review.ReviewInfo);
}

-keepnames class com.google.android.play.core.review.ReviewInfo

-keep class com.google.android.play.core.review.ReviewManagerFactory {
  <init>();

  public static com.google.android.play.core.review.ReviewManager create(android.content.Context);
}

-keep class com.google.android.play.core.review.testing.FakeReviewManager {
  public <init>(android.content.Context);
  public com.google.android.gms.tasks.Task requestReviewFlow();
  public com.google.android.gms.tasks.Task launchReviewFlow(android.app.Activity, com.google.android.play.core.review.ReviewInfo);
}

-keep class com.google.android.play.core.review.model.ReviewErrorCode {
    public static int NO_ERROR;
    public static int PLAY_STORE_NOT_FOUND;
    public static int INVALID_REQUEST;
    public static int INTERNAL_ERROR;
}

-keep class com.google.android.play.core.review.ReviewException {
    public int getErrorCode();
}
