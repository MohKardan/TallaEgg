# ZARORBIT Visual Identity System v1.0

Executive visual identity system, pure vector logo masters, WCAG 2.2 AA verified design tokens, and product UI applications derived from **ZARORBIT Brand Book v1.1** (11 November 2026).

## Primary Deliverables

| Artifact | Purpose |
|---|---|
| [`ZARORBIT_Visual_Identity_Guidelines_v1.0.pdf`](./ZARORBIT_Visual_Identity_Guidelines_v1.0.pdf) | 10-page editorial visual identity manual (A4, embedded OFL fonts & vector diagrams) |
| [`ZARORBIT_Visual_Identity_Guidelines_v1.0.md`](./ZARORBIT_Visual_Identity_Guidelines_v1.0.md) | Complete Markdown reference (18 guideline sections, Design Brief, 3 directions, WCAG audit) |
| [`ZARORBIT_Logo_Symbol_Primary.svg`](./ZARORBIT_Logo_Symbol_Primary.svg) | Primary `R1` (`1–0–1` orbital `Z`) monogram vector master (`128×128`) |
| [`ZARORBIT_Logo_Horizontal_Dark.svg`](./ZARORBIT_Logo_Horizontal_Dark.svg) | Integrated `[R1]ARORBIT` horizontal logotype (`321×88`) for dark Obsidian surfaces (eliminates double-`Z` repetition) |
| [`ZARORBIT_Logo_Horizontal_Light.svg`](./ZARORBIT_Logo_Horizontal_Light.svg) | Integrated `[R1]ARORBIT` horizontal logotype (`321×88`, WCAG AA/AAA: `#82611C` `Z` + `#17191D` `ARORBIT`) for Ivory / light surfaces |
| [`ZARORBIT_Design_Tokens_v1.0.json`](./ZARORBIT_Design_Tokens_v1.0.json) | W3C Design Tokens format covering Light/Dark themes, typography, spacing, borders, shadows, components |
| [`ZARORBIT_Brand_Assets_Preview.png`](./ZARORBIT_Brand_Assets_Preview.png) | High-resolution overview sheet of logos, clear space, color contrast, typography, and icons |

## Extended Logo Variants (`logo-variants/`)

- [`ZARORBIT_Logo_Vertical_Dark.svg`](./logo-variants/ZARORBIT_Logo_Vertical_Dark.svg) & [`ZARORBIT_Logo_Vertical_Light.svg`](./logo-variants/ZARORBIT_Logo_Vertical_Light.svg)
- [`ZARORBIT_Logo_Monochrome_Dark.svg`](./logo-variants/ZARORBIT_Logo_Monochrome_Dark.svg) & [`ZARORBIT_Logo_Monochrome_Light.svg`](./logo-variants/ZARORBIT_Logo_Monochrome_Light.svg)
- [`ZARORBIT_Logo_AppIcon_Dark.svg`](./logo-variants/ZARORBIT_Logo_AppIcon_Dark.svg) & [`ZARORBIT_Logo_AppIcon_Light.svg`](./logo-variants/ZARORBIT_Logo_AppIcon_Light.svg)
- [`ZARORBIT_Logo_Favicon_16_32.svg`](./logo-variants/ZARORBIT_Logo_Favicon_16_32.svg) (dedicated optical small-scale master for `16px`–`28px`)
- [`ZARORBIT_Logo_Metallic_Campaign.svg`](./logo-variants/ZARORBIT_Logo_Metallic_Campaign.svg) (optional campaign/presentation variant per Brand Book §8.3.6; do not use in UI)

## Developer & Figma System (`system/`)

- [`ZARORBIT_Figma_Tokens_Studio_v1.0.json`](./system/ZARORBIT_Figma_Tokens_Studio_v1.0.json) — Direct import into Figma via Tokens Studio (`core`, `dark`, `light` theme sets)
- [`zarorbit-tokens.css`](./system/zarorbit-tokens.css) — CSS custom properties for `:root` / `[data-theme="dark"]` and `[data-theme="light"]`
- [`zarorbit-tokens.ts`](./system/zarorbit-tokens.ts) — TypeScript / Tailwind helper constants
- [`ZARORBIT_Icon_System_v1.0.svg`](./system/ZARORBIT_Icon_System_v1.0.svg) — 16 custom `24×24` geometric ecosystem `<symbol>` icons (`1.8px` stroke)
- [`ZARORBIT_Graphic_Motifs_v1.0.svg`](./system/ZARORBIT_Graphic_Motifs_v1.0.svg) — Restrained orbital lattice, coordinate grid, and dual-node motifs

## Product & Marketing Applications (`applications/`)

1. [`01_App_Icon_Showcase.html`](./applications/01_App_Icon_Showcase.html) ([PNG](./applications/01_App_Icon_Showcase.png)) — App Icons & 16px Optical Favicon Matrix
2. [`02_Website_Hero_Section.html`](./applications/02_Website_Hero_Section.html) ([PNG](./applications/02_Website_Hero_Section.png)) — Bilingual Ecosystem Website Hero
3. [`03_Unified_Asset_Dashboard.html`](./applications/03_Unified_Asset_Dashboard.html) ([PNG](./applications/03_Unified_Asset_Dashboard.png)) — Multi-Asset Dashboard (RWA + Crypto + NFTs + DAO)
4. [`04_Asset_Intro_Card.html`](./applications/04_Asset_Intro_Card.html) ([PNG](./applications/04_Asset_Intro_Card.png)) — Multi-Class Asset Intro Cards
5. [`05_Gold_Asset_View.html`](./applications/05_Gold_Asset_View.html) ([PNG](./applications/05_Gold_Asset_View.png)) — Gold Asset Detail with Brand Book §10.1 Ownership Checklist
6. [`06_NFT_Asset_Card.html`](./applications/06_NFT_Asset_Card.html) ([PNG](./applications/06_NFT_Asset_Card.png)) — NFT Showcase Cards (Dark & Light) with IP Boundary Notice
7. [`07_User_Profile.html`](./applications/07_User_Profile.html) ([PNG](./applications/07_User_Profile.png)) — Light Theme Member Profile & Governance Eligibility
8. [`08_DAO_Governance_Page.html`](./applications/08_DAO_Governance_Page.html) ([PNG](./applications/08_DAO_Governance_Page.png)) — Progressive DAO & Community Participation (`In Development`)
9. [`09_Social_Media_Templates.html`](./applications/09_Social_Media_Templates.html) ([PNG](./applications/09_Social_Media_Templates.png)) — 3 Cohesive Social Media Post Templates
10. [`10_Presentation_Deck_Template.html`](./applications/10_Presentation_Deck_Template.html) ([PDF](./applications/10_Presentation_Deck_Template.pdf) | [PNG](./applications/10_Presentation_Deck_Preview.png)) — 5-Slide Widescreen 16:9 Presentation Template

## Font Licensing & WCAG 2.2 AA Verification

- **Typography Licenses:** `Inter`, `Manrope`, and `Vazirmatn` are all verified under the **SIL Open Font License 1.1 (OFL-1.1)**, permitting unrestricted commercial, web, mobile, and PDF embedding.
- **WCAG 2.2 AA Accessibility Findings:**
  - Brand Book's `#C9A45A` Metallic Gold scores `7.49:1` on Obsidian `#17191D` (AAA), but only `2.03:1` on Ivory `#F2EEE5`. Light Mode therefore pairs `#17191D` with accessible **Bronze Gold 700 (`#82611C`, `4.93:1` on Ivory)** for text, icons, and light-mode orbital accents.
  - Brand Book's `#286B63` Deep Teal scores `5.38:1` on Ivory and `6.23:1` with white text, but only `2.83:1` on Obsidian `#17191D`. Dark Mode UI text/icons therefore use **Orbit Teal 400 (`#4CB3A5`, `6.95:1` on Obsidian)** while solid Teal action fills keep `#286B63` with `#FFFFFF` text.
  - Interactive control borders use `#6B7485` (`3.21:1` on `#1F2329`) and `#7C776B` (`3.85:1` on `#F2EEE5`) to satisfy WCAG 2.2 SC 1.4.11 (`>= 3.0:1`).

## Web Interface Integration (`web/`)

The [`web/`](../web/config/brand.config.ts) application is aligned with **Brand Book v1.1** and **Visual Identity System v1.0**:
1. [`web/config/brand.config.ts`](../web/config/brand.config.ts) uses canonical uppercase `ZARORBIT` spelling and reflects the full ecosystem scope (`RWA` with 18k gold initial focus, `Crypto & NFTs`, and `Progressive DAO`).
2. Legacy raster artwork in `web/public/images/` was replaced with pure-vector SVGs ([`zarorbit-logo-dark.svg`](../web/public/images/zarorbit-logo-dark.svg), [`zarorbit-symbol.svg`](../web/public/images/zarorbit-symbol.svg), [`zarorbit-favicon.svg`](../web/public/images/zarorbit-favicon.svg)) and [`BrandLogo.tsx`](../web/components/BrandLogo.tsx) renders the integrated `[R1]ARORBIT` lockup without duplicate-`Z` repetition.
3. [`ZarorbitOrbitalMotion.tsx`](../web/components/ZarorbitOrbitalMotion.tsx) implements the geometric `1–0–1` center mark with two restrained orbital paths, minimal participant nodes, and `prefers-reduced-motion` support.
4. [`tailwind.config.ts`](../web/tailwind.config.ts) and [`globals.css`](../web/app/globals.css) consume the WCAG 2.2 AA Obsidian (`#17191D`), Metallic Gold (`#C9A45A`), Ivory (`#F2EEE5`), and Deep/Orbit Teal (`#286B63` / `#4CB3A5`) palette tokens.
