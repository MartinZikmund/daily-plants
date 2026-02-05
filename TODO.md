# Daily Dozen - Pre-Release TODO List

This document tracks all tasks needed before open-sourcing and publishing to app stores.

---

## 1. Open-Source Readiness

### Documentation

- [x] LICENSE (MIT)
- [x] TODO.md
- [ ] README.md - Comprehensive with screenshots, build instructions, features
- [ ] CONTRIBUTING.md - Contribution guidelines
- [ ] CODE_OF_CONDUCT.md - Community standards
- [ ] SECURITY.md - Security policy and vulnerability reporting
- [ ] CHANGELOG.md - Version history
- [ ] THIRD-PARTY-NOTICES.md - Third-party license attributions

### GitHub Configuration

- [ ] Issue templates (bug report, feature request)
- [ ] Pull request template
- [ ] Dependabot configuration (exists but verify)
- [ ] Branch protection rules

### CI/CD

- [x] Basic CI (Windows Debug build)
- [ ] Multi-platform builds (Android, iOS, WebAssembly, macOS, Linux)
- [ ] Release build configurations
- [ ] Automated testing workflow
- [ ] Release/deployment workflows for app stores

---

## 2. Play Store (Android) Requirements

### Store Listing Assets

- [ ] App Icon - Distinctive Daily Dozen icon (512x512 PNG)
- [ ] Feature Graphic - 1024x500 banner image
- [ ] Screenshots - Phone (min 2) and tablet (min 1)
- [ ] Short description (80 chars max)
- [ ] Full description (4000 chars max)

### Technical Requirements

- [ ] AndroidManifest.xml - Add app label, icon references, permissions
- [ ] Adaptive icon background (icon_foreground.svg exists)
- [ ] Release keystore for signing
- [ ] App Bundle (.aab) build configuration
- [ ] Target SDK verification (Google's minimum requirements)
- [ ] ProGuard/R8 configuration for release builds

### Compliance

- [ ] Privacy Policy URL (required)
- [ ] Data safety form completion
- [ ] Content rating questionnaire
- [ ] Ads declaration (none)

---

## 3. Windows Store Requirements

### Store Listing Assets

- [ ] App Icon - All required tile sizes
- [ ] Screenshots - Desktop and tablet
- [ ] Store description
- [ ] Search terms/keywords

### Technical Requirements

- [ ] Package.appxmanifest configuration
- [ ] Package Identity for Microsoft Store
- [ ] Store association
- [ ] MSIX package configuration

### Compliance

- [ ] Privacy Policy URL
- [ ] Age rating questionnaire
- [ ] Microsoft Store Policies compliance

---

## 4. iOS App Store Requirements

### Store Listing Assets

- [ ] App Icon - All required sizes (1024x1024 for store)
- [ ] Screenshots - iPhone (6.7", 6.5", 5.5"), iPad (12.9", 11")
- [ ] App Preview videos (optional)
- [ ] App description
- [ ] Keywords
- [ ] Support URL
- [ ] Marketing URL (optional)

### Technical Requirements

- [ ] Info.plist - Complete all required keys
  - [ ] CFBundleDisplayName
  - [ ] CFBundleShortVersionString
  - [ ] ITSAppUsesNonExemptEncryption (set to false)
- [ ] Entitlements.plist - Configure if needed
- [x] PrivacyInfo.xcprivacy - API usage declared
- [ ] Apple Developer Account setup
- [ ] Distribution provisioning profile
- [ ] App Store Connect configuration

### Compliance

- [ ] Privacy Policy URL (required)
- [ ] App Privacy details (data collection form)
- [ ] Export compliance (encryption)
- [ ] Age rating

---

## 5. Code Quality & Testing

### Testing

- [ ] Create test project (DailyDozen.Tests)
- [ ] Unit tests for Services
  - [ ] SqliteDataService tests
  - [ ] ExportService tests
  - [ ] AchievementService tests
  - [ ] LocalizationService tests
- [ ] Unit tests for ViewModels
  - [ ] TodayViewModel tests
  - [ ] StatisticsViewModel tests
  - [ ] SettingsViewModel tests
  - [ ] AchievementsViewModel tests
- [ ] Integration tests
- [ ] UI tests (optional)
- [ ] Code coverage configuration

### Code Quality

- [ ] Code analysis/linting configuration
- [ ] EditorConfig file
- [ ] Nullable reference types review

---

## 6. App-Specific Improvements

### Required

- [ ] Distinctive app icon design
- [ ] Privacy Policy page (in-app or web)
- [ ] Dynamic version display (currently hardcoded "1.0" in AboutPage.xaml)

### Recommended

- [ ] Terms of Service
- [ ] Crash reporting integration (App Center, Sentry, etc.)
- [ ] In-app feedback mechanism
- [ ] Rate/review prompt

### Nice to Have

- [ ] Additional localizations beyond EN/CS
- [ ] Analytics (privacy-respecting)
- [ ] Onboarding/tutorial screens

---

## 7. Versioning & Release

- [ ] Semantic versioning strategy
- [ ] Version automation in CI/CD
- [ ] Release branch strategy
- [ ] Git tags for releases
- [ ] Code obfuscation for release builds

---

## 8. Legal & Compliance

### Required

- [ ] Privacy Policy document
  - [ ] Data collection disclosure (none - all local)
  - [ ] Data storage practices
  - [ ] Export functionality disclosure
  - [ ] Third-party services (none)
- [ ] GDPR compliance review
- [ ] CCPA compliance review

### Recommended

- [ ] Terms of Service
- [ ] Third-party license compliance verification
- [ ] NutritionFacts.org attribution verification
- [ ] Dr. Greger content licensing verification

---

## 9. Security

- [x] .env in .gitignore
- [x] No API keys in codebase
- [ ] Security audit of data handling
- [ ] Dependency vulnerability scan
- [ ] OWASP mobile security review

---

## Priority Summary

### Critical (Before Any Release)

1. Privacy Policy (hosted URL)
2. Comprehensive README.md
3. CONTRIBUTING.md, CODE_OF_CONDUCT.md, SECURITY.md
4. Distinctive app icon
5. Platform manifests configuration
6. Store assets (screenshots, descriptions)
7. Basic unit tests

### High Priority

8. Release build configurations
9. Signing certificates/keys
10. Release CI/CD workflows
11. Crash reporting
12. THIRD-PARTY-NOTICES.md

### Medium Priority

13. Additional localizations
14. Code obfuscation
15. UI tests
16. CHANGELOG.md maintenance

---

## Progress Tracking

| Category | Total | Completed | Percentage |
|----------|-------|-----------|------------|
| Open-Source Docs | 8 | 2 | 25% |
| GitHub Config | 4 | 0 | 0% |
| CI/CD | 5 | 1 | 20% |
| Play Store | 13 | 0 | 0% |
| Windows Store | 8 | 0 | 0% |
| iOS App Store | 16 | 1 | 6% |
| Testing | 12 | 0 | 0% |
| App Improvements | 10 | 0 | 0% |
| Legal | 8 | 0 | 0% |
| Security | 5 | 3 | 60% |
| **TOTAL** | **89** | **7** | **8%** |

---

*Last updated: 2026-01-22*
