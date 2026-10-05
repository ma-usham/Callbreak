# Google Play Billing setup (No Ads)

No Ads is a one-time purchase that removes the ads between matches (the owner, 5 Oct 2026). Earn
Coins videos stay for everyone, because the player chooses those. On phones the game sells it
through Unity IAP (`com.unity.purchasing` 5.4.4) over Google Play Billing. In the Editor, in the
tests and in desktop builds a stand-in sells it at once, in memory only (`StandInNoAds`), so no store
is touched there.

## The product

| What | Value |
| --- | --- |
| Product ID | `no_ads`: in code as `NoAdsProduct.Id`. Play never lets an id change or be reused. |
| Kind | A one-time product the player keeps (non-consumable in Unity IAP). |
| Price | $0.99, set in Play Console. The game shows Play's price string in the player's currency and never hard-codes one. |
| Name and description | Yours, in Play Console. Google's purchase sheet shows them. |

## How the game sells and keeps it

- **At every start** the game reads its own `no-ads.json`. An owner gets no ads between matches from
  the first frame, offline too. Then it connects to Google Play, reads the price and lists the
  purchases of the player's Google account. If a step fails (offline, no Play Store), it tries again
  after 5 s, 10 s, 20 s ... and then every two minutes at most. The button can sell only once the
  account has been listed, so a player who already owns No Ads never sees the offer.
- **A purchase** opens Google's sheet. Once the purchase is paid, the game saves No Ads as owned, and
  only then acknowledges the purchase to Google, as Google asks. Google refunds a purchase left
  unacknowledged for three days. If the acknowledgement fails, the game lists the account again
  after 5 s, 10 s ... and acknowledges again, and it does the same at every start.
- **A payment that settles later** (cash, some UPI and bank payments, Play's "slow" test cards)
  grants nothing until it is paid. Meanwhile no second purchase starts, and the No Ads button stays
  hidden. It is granted as soon as Google says it is paid: at once while the game is open, when the
  player comes back to the game, or at the next start. A payment that never comes brings the button
  back the same way.
- **Back in the game** (the game regains focus), it lists the account again when that can bring
  news: while No Ads isn't owned, or while a paid purchase still waits for its acknowledgement (a
  refund then shows at once). At most once every 30 s, never while Google's purchase sheet is up
  (it takes the focus as it opens and gives it back as it closes), and not while the start-up
  listing is still under way. An owner whose purchase is acknowledged is not listed again until the
  next start.
- **Restoring is automatic.** After a reinstall, or on a new phone, the listing at start brings No
  Ads back. Ownership follows the Google account that is signed in to the Play Store. There is no
  Restore button, as Android needs none.
- **Refunds.** When the listing no longer has `no_ads`, the ads come back at that start, and the
  button sells again. That happens when an order is refunded with its entitlement removed, when a
  player refunds through Google Play, and when Google refunds a purchase that was never
  acknowledged. A listing that fails never takes No Ads away: an error is not a refund. A refund
  that leaves the entitlement in place leaves No Ads in place.
- **What is kept on the phone:** `no-ads.json` with one `owned` flag. The game's own code saves and
  logs no receipt, order or purchase token. Unity IAP itself sends some to Unity: see "Unity IAP
  reports purchases to Unity" below. The game's log lines start with `[IAP] ` (the store side) and
  `No Ads:`.

## What the owner does

Agents can't sign or upload builds, or change Play Console.

1. **Upload a build that includes billing to a test track.** Play Console now says "To add
   one-time products, you need to add the BILLING permission to your APK". Every build made since
   the IAP package was added carries that permission (see "The Android build" below). Builds made
   before it, such as the last one on this machine (3 Oct), do not.
   - Build an Android App Bundle, signed with the upload key: the project's keystore, set in Player
     Settings > Publishing Settings. Its passwords stay with you.
   - Give it a higher **Bundle Version Code** than every build uploaded so far (Player Settings >
     Other Settings; it is 1 now).
   - Upload it to **Testing > Internal testing** and roll the release out to your testers. Play
     Console then lets you create one-time products.
2. **Create `no_ads`.** Monetize with Play > Products > **One-time products** > Create one-time
   product.
   - Product ID `no_ads`, exactly. Add a name and a description.
   - Add one purchase option of type **Buy**, priced at **$0.99**, and let Play set the other
     countries' prices from it.
   - Make sure this option is the product's **backwards compatible** one. Each product has one, and
     Play's documentation says it is the option older billing calls see. Unity IAP 5.4.4 reads the
     price and opens the purchase through such a call (`getOneTimePurchaseOfferDetails`, from the
     package's code). Without that option the button shows no price and nothing can be bought.
     Whether Play Console ticks it by default isn't verified here.
   - **Activate** the product and its purchase option. A new product can take a while (minutes, at
     times hours) to reach phones. Until then the No Ads button has no price to show.
3. **License testers.** In Play Console > Settings (the developer account's, from the All apps page)
   > **License testing**, add the Google accounts that will test. They buy with Google's test cards
   and are never charged. Add the same accounts as testers of the internal track and open its opt-in
   link on each phone.
4. **Test it** on a phone signed in to the Play Store with a license-tester account, with the game
   installed from the internal testing link.
   - The No Ads button shows the price in the phone's currency.
   - Buy with **Test card, always approves**: the ads between matches stop. Restart, and they stay
     off.
   - Close Google's sheet without buying: nothing changes.
   - Buy with **Slow test card, approves after a few minutes**: the game says the purchase is
     pending, then No Ads arrives a few minutes later, while the game is open or at the next start.
   - **Slow test card, declines after a few minutes**: No Ads never arrives, and after the next
     start the button sells again.
   - Uninstall, install again from Play and start with a network: No Ads comes back by itself.
   - Google's documentation gives test purchases a much shorter acknowledgement window than three
     days (not checked here). So if a test purchase still owns No Ads 10 minutes later, the
     acknowledgement works.
   - Each case can be repeated after a refund (below).
5. **Refunds.** Play Console > Order management: find the order, refund it and choose to remove the
   entitlement. At the game's next start with a network, the ads come back. Test orders cost nothing
   to refund.
6. **iOS (later).** The same code would sell through the App Store, but nothing is set up there.
   App Store Connect needs a non-consumable `no_ads`. Apple also requires a **Restore Purchases**
   button for non-consumables (App Review Guideline 3.1.1), which the game doesn't have yet, so iOS
   needs one before review. None of this is tested on iOS.

## The Android build

- **Play Billing Library 9.0.0.** Unity IAP adds it through its own Gradle step, not EDM4U and not
  `mainTemplate.gradle`. After Unity writes the Gradle project, Unity IAP's
  `IAPAndroidDependencies` puts `implementation 'com.android.billingclient:billing:9.0.0'` into
  `unityLibrary/build.gradle`. 9.0.0 is above Play's minimum: Billing Library 7, and 8 for app
  updates from 31 Aug 2026. The same step adds two empty Kotlin stdlib stubs (1.8.22) and the Gradle
  properties `android.useAndroidX=true` and `android.enableJetifier=true`.
- **The BILLING permission.** The package ships no Android manifest of its own. The permission
  (`com.android.vending.BILLING`) comes with Google's Billing Library, which declares it in its own
  manifest. Gradle's manifest merger then puts it into the app's. Not verified by a build here: no
  Android build has run since the package was added, and the library isn't in this machine's Gradle
  cache yet. To check a build, look for it in the merged manifest
  (`Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/intermediates/merged_manifests/release/processReleaseManifest/AndroidManifest.xml`),
  or, after an upload, in Play Console's App bundle explorer.
- **`Assets/Resources/BillingMode.json`** (`{"androidStore":"GooglePlay"}`) is Unity IAP's own file.
  The package writes it and reads it at runtime. Without it, an Android build would get Unity's fake
  store. Keep it in the repository. The project's no-`Resources.Load` rule still holds for our code:
  only Unity IAP reads that file.
- **No Unity Gaming Services.** Unity IAP connects to Google Play without
  `UnityServices.InitializeAsync()`, and the game never calls it. Unity's 5.4 build warning about a
  missing Google Play license key concerns Unity's receipt validation service, which the game
  doesn't use.
- **Unity IAP reports purchases to Unity, by itself.** In the package's code (`PurchaseEventEmitter`),
  Unity IAP 5.4.4 posts an event to Unity's analytics (`prd.insights.analytics.unity3d.com`) when a
  purchase starts, is paid, fails and is acknowledged. Nothing gates it on this Unity version.
  - The paid and acknowledged events carry Google's receipt for the purchase: its order id and
    purchase token.
  - Every event carries the product, the app's id and version, device details, the project's Unity
    Cloud id (the project is linked) and Unity's ids for the install.
  - The game's code can't switch this off: it is inside the package. The package says collection is
    managed per project in the Unity Dashboard (Project Settings > Developer Data).
  - Either turn it off there, or declare it in Play's Data safety form (Financial info > Purchase
    history, collected) and in the privacy policy.
  - Not verified on a phone or in the Dashboard.
- **Release builds are minified** (R8: Minify Release on, no custom ProGuard file). Unity IAP calls
  the Billing Library only from C#, through JNI by name, and implements its listeners with
  `AndroidJavaProxy`. R8 sees no Java caller, so it could strip or rename those classes.
  - Unity IAP 5.x ships no keep rules: it has no AAR, and its Gradle step adds none.
  - Unity's own `proguard-unity.txt` keeps nothing of the Billing Library.
  - The Billing Library's own consumer rules couldn't be read here, as its AAR isn't on this
    machine.
  - So `Assets/Plugins/Android/UnityIapKeep.androidlib` carries one rule as its consumer rules,
    `-keep class com.android.billingclient.api.** { *; }`, with its source in `proguard-rules.pro`.
    The library has no code, no permissions and its own namespace
    (`com.dayzerogames.callbreak.iapkeep`).
  - Not verified by a build. To check it, see that a release build's Gradle project includes the
    module (`settings.gradle` lists `unityLibrary:UnityIapKeep.androidlib`), and that a release
    build shows the price on a phone.

## The code

| Piece | Where |
| --- | --- |
| The port the game uses | `Application/Economy/INoAds.cs` (`INoAds`, `NoAdsPurchase`, `NoAdsProduct.Id`) |
| The store-backed No Ads (engine-free, tested) | `Application/Economy/StoreNoAds.cs`, its save `NoAdsData.cs`, over the store port `IStoreBilling.cs` |
| Unity IAP behind the store port | `Infrastructure/Purchasing/UnityIapBilling.cs` (assembly `Callbreak.Infrastructure.Purchasing`) |
| The stand-in (Editor, tests, desktop) | `Application/Economy/StandInNoAds.cs` |
| Which one runs | `ProjectLifetimeScope.RegisterPurchases`: the store's on Android and iOS players, the stand-in elsewhere |
