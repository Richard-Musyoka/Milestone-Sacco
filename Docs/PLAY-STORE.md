# Publishing Tajiri Sacco on Google Play

The system is a Progressive Web App (PWA). Google Play accepts PWAs wrapped as a
Trusted Web Activity (TWA): a tiny Android app that opens your live site full-screen,
with no browser bar. Every update you deploy to the server reaches app users instantly —
no new Play Store release needed.

## 1. Before you start
- Host the system on a real domain with HTTPS, e.g. https://portal.tajirisacco.co.ke
  (Play and "Install app" both require HTTPS; localhost is fine only for testing).
- Open https://<your-domain>/manifest.webmanifest and check it loads.
- In Chrome DevTools > Application > Manifest there should be no errors.

## 2. Build the Android app (pick one)
**PWABuilder (easiest, no Android Studio):**
1. Go to https://www.pwabuilder.com, enter your domain, click *Package for stores* > *Android*.
2. Package ID: e.g. `ke.co.tajirisacco.app`. App name: Tajiri Sacco.
3. Download the zip. It contains the `.aab` to upload, a signing key and `assetlinks.json`.

**Bubblewrap (command line):**
```
npm i -g @bubblewrap/cli
bubblewrap init --manifest https://<your-domain>/manifest.webmanifest
bubblewrap build
```

## 3. Link the app and the website (removes the browser bar)
Put the package name and SHA-256 fingerprint(s) in appsettings.json. Include BOTH your
upload key fingerprint and the "App signing key" fingerprint from Play Console >
Setup > App integrity.
```json
"Pwa": {
  "AndroidPackage": "ke.co.tajirisacco.app",
  "Sha256Fingerprints": [ "AA:BB:CC:...", "DD:EE:FF:..." ]
}
```
The app serves it at https://<your-domain>/.well-known/assetlinks.json automatically.

## 4. Play Console listing
- App icon 512×512: wwwroot/img/app/icon-512.png
- Feature graphic 1024×500: wwwroot/img/app/play-feature-graphic.png
- Screenshots: take 4–8 from a phone (member portal, statement, loans, pay).
- Category: Finance. Play asks finance apps for extra declarations: say it is the
  official app of a registered SACCO and give your SASRA/registration details.
- Privacy policy URL is required (a page on your website).
- Data safety form: you collect name, phone, email and financial info; data is encrypted
  in transit (HTTPS); users can ask for deletion through the SACCO office.

## 5. Keep in mind
- The app needs internet: balances are always live. Offline it shows a friendly page.
- Members sign in with member number + one-time code, so set up SMS (the "Sms" section
  with Africa's Talking) before launch.
