# ZARORBIT Visual Identity System v1.2

Executive visual identity system, pure-vector logo masters, WCAG 2.2 AA verified design tokens, and product UI integration derived from **ZARORBIT Brand Book v1.1** and refined with the owner-approved **Zero-Interrupt Orbital Z (`S1`)** minimalist startup monogram (24 November 2026).

## Locked Brand Decisions

- **Official Latin Name:** `ZARORBIT` (uppercase, no hyphen or space)
- **Official Persian Spelling:** `زَر اوربیت`
- **Official Domains & Telegram Handles:** `zarorbit.ir`, `zarorbit.com`, `@zarorbitbot`, `@zarorbit`
- **Approved Logo Master:** **Zero-Interrupt Orbital Z (`S1`)** — open geometric `Z` with 180° rotational symmetry split cleanly around a floating central orbital `0` ring (`8.5u` concentric halo), 100% closed filled paths (`fill-rule="nonzero"`), zero coin medallion, zero un-expanded strokes, zero Zcash vertical stems.
- **Lockup Consistency:** Separated `[Symbol] ZARORBIT` lockup across Horizontal, Vertical, and Monochrome variants using genuine **Manrope Bold (SIL OFL-1.1)** font outlines with manual optical kerning.
- **Telegram Bot Copy Policy (Pilot Phase):** Live `TelegramBot` user messages intentionally remain **white-label** (`طلافروشی خود`) to fit the shop-based gold dealer model; `ZARORBIT` branding governs the web portal, official domains, and `@zarorbit` / `@zarorbitbot` public channels.
- **Iconography Boundary:** `lucide-react` is the standard UI icon library in `web/`; [`system/ZARORBIT_Icon_System_v1.0.svg`](./system/ZARORBIT_Icon_System_v1.0.svg) is reserved for brand diagrams, pitch decks, and marketing visuals.

## Primary Deliverables

| Artifact | Purpose |
|---|---|
| [`ZARORBIT_Visual_Identity_Guidelines_v1.0.md`](./ZARORBIT_Visual_Identity_Guidelines_v1.0.md) | Complete Markdown reference (updated for `S1` master, locked Persian spelling, white-label bot policy, WCAG 2.2 AA audit) |
| [`ZARORBIT_Visual_Identity_Guidelines_v1.0.pdf`](./ZARORBIT_Visual_Identity_Guidelines_v1.0.pdf) | Initial v1.0 editorial manual archive (PDF; Markdown above is the canonical v1.1 reference) |
| [`ZARORBIT_Logo_Symbol_Primary.svg`](./ZARORBIT_Logo_Symbol_Primary.svg) | Primary `S1` Zero-Interrupt Orbital `Z` vector master (`128×128`) |
| [`ZARORBIT_Logo_Horizontal_Dark.svg`](./ZARORBIT_Logo_Horizontal_Dark.svg) | Separated `[Symbol] ZARORBIT` horizontal lockup (`417×88`) for dark Obsidian surfaces |
| [`ZARORBIT_Logo_Horizontal_Light.svg`](./ZARORBIT_Logo_Horizontal_Light.svg) | Separated `[Symbol] ZARORBIT` horizontal lockup (`417×88`, WCAG AA/AAA: `#82611C` symbol + `#17191D` wordmark) for Ivory / light surfaces |
| [`ZARORBIT_Design_Tokens_v1.0.json`](./ZARORBIT_Design_Tokens_v1.0.json) | W3C Design Tokens (`v1.2.0`) covering Light/Dark themes, typography, spacing, borders, shadows, components |
| [`ZARORBIT_Brand_Assets_Preview.png`](./ZARORBIT_Brand_Assets_Preview.png) | Master visual preview sheet of `S1` lockups, monochrome, metallic campaign, app icons, Telegram avatar, and WCAG tokens |
| [`ZARORBIT_Telegram_Avatar_1024.png`](./ZARORBIT_Telegram_Avatar_1024.png) | Ready-to-upload `1024×1024` metallic circular-safe avatar for `@zarorbit` and `@zarorbitbot` |

## Extended Logo Variants (`logo-variants/`)

- [`ZARORBIT_Logo_Vertical_Dark.svg`](./logo-variants/ZARORBIT_Logo_Vertical_Dark.svg) & [`ZARORBIT_Logo_Vertical_Light.svg`](./logo-variants/ZARORBIT_Logo_Vertical_Light.svg)
- [`ZARORBIT_Logo_Monochrome_Dark.svg`](./logo-variants/ZARORBIT_Logo_Monochrome_Dark.svg) & [`ZARORBIT_Logo_Monochrome_Light.svg`](./logo-variants/ZARORBIT_Logo_Monochrome_Light.svg)
- [`ZARORBIT_Logo_AppIcon_Dark.svg`](./logo-variants/ZARORBIT_Logo_AppIcon_Dark.svg) & [`ZARORBIT_Logo_AppIcon_Light.svg`](./logo-variants/ZARORBIT_Logo_AppIcon_Light.svg)
- [`ZARORBIT_Logo_Favicon_16_32.svg`](./logo-variants/ZARORBIT_Logo_Favicon_16_32.svg) (dedicated optical small-scale master for `16px`–`28px`)
- [`ZARORBIT_Logo_Telegram_Avatar.svg`](./logo-variants/ZARORBIT_Logo_Telegram_Avatar.svg) (vector avatar with 16% Obsidian breathing margin for circular crop)
- [`ZARORBIT_Logo_Metallic_Campaign.svg`](./logo-variants/ZARORBIT_Logo_Metallic_Campaign.svg) (unified gradient campaign/presentation variant; do not use in standard UI controls)

## Developer & Figma System (`system/`)

- [`ZARORBIT_Figma_Tokens_Studio_v1.0.json`](./system/ZARORBIT_Figma_Tokens_Studio_v1.0.json) — Direct import into Figma via Tokens Studio (`core`, `dark`, `light` theme sets)
- [`zarorbit-tokens.css`](./system/zarorbit-tokens.css) — CSS custom properties for `:root` / `[data-theme="dark"]` and `[data-theme="light"]`
- [`zarorbit-tokens.ts`](./system/zarorbit-tokens.ts) — TypeScript / Tailwind helper constants
- [`ZARORBIT_Icon_System_v1.0.svg`](./system/ZARORBIT_Icon_System_v1.0.svg) — 16 custom `24×24` geometric ecosystem `<symbol>` icons for diagrams & decks (`web/` UI uses `lucide-react`)
- [`ZARORBIT_Graphic_Motifs_v1.0.svg`](./system/ZARORBIT_Graphic_Motifs_v1.0.svg) — Restrained orbital lattice, coordinate grid, and dual-node motifs

## Product & Marketing Applications (`applications/`)

Legacy v1.0 HTML/PNG concept explorations retained for reference:

1. [`01_App_Icon_Showcase.html`](./applications/01_App_Icon_Showcase.html) ([PNG](./applications/01_App_Icon_Showcase.png))
2. [`02_Website_Hero_Section.html`](./applications/02_Website_Hero_Section.html) ([PNG](./applications/02_Website_Hero_Section.png))
3. [`03_Unified_Asset_Dashboard.html`](./applications/03_Unified_Asset_Dashboard.html) ([PNG](./applications/03_Unified_Asset_Dashboard.png))
4. [`04_Asset_Intro_Card.html`](./applications/04_Asset_Intro_Card.html) ([PNG](./applications/04_Asset_Intro_Card.png))
5. [`05_Gold_Asset_View.html`](./applications/05_Gold_Asset_View.html) ([PNG](./applications/05_Gold_Asset_View.png))
6. [`06_NFT_Asset_Card.html`](./applications/06_NFT_Asset_Card.html) ([PNG](./applications/06_NFT_Asset_Card.png))
7. [`07_User_Profile.html`](./applications/07_User_Profile.html) ([PNG](./applications/07_User_Profile.png))
8. [`08_DAO_Governance_Page.html`](./applications/08_DAO_Governance_Page.html) ([PNG](./applications/08_DAO_Governance_Page.png))
9. [`09_Social_Media_Templates.html`](./applications/09_Social_Media_Templates.html) ([PNG](./applications/09_Social_Media_Templates.png))
10. [`10_Presentation_Deck_Template.html`](./applications/10_Presentation_Deck_Template.html) ([PDF](./applications/10_Presentation_Deck_Template.pdf) | [PNG](./applications/10_Presentation_Deck_Preview.png))

## Font Licensing & WCAG 2.2 AA Verification

- **Typography Licenses:** `Inter`, `Manrope`, and `Vazirmatn` are verified under the **SIL Open Font License 1.1 (OFL-1.1)**.
- **WCAG 2.2 AA Contrast Verification:**
  - `#C9A45A` Metallic Gold scores `7.49:1` on Obsidian `#17191D` (AAA) and `6.72:1` on `#1F2329` (AA), but only `2.03:1` on Ivory `#F2EEE5`. Light Mode pairs `#17191D` with **Bronze Gold 700 (`#82611C`, `4.93:1` on Ivory)**.
  - `#286B63` Deep Teal scores `5.38:1` on Ivory and `6.23:1` with `#FFFFFF` text, but only `2.83:1` on `#17191D`. Dark Mode UI accents use **Orbit Teal 400 (`#4CB3A5`, `6.95:1` on Obsidian)**.

## Web Interface Integration (`web/`)

The [`web/`](../web/config/brand.config.ts) application is aligned with the locked `v1.2` identity and current product reality:
1. **Product-First Homepage (`/`):** Leads with today's live gold trading infrastructure (`طلای آبشده ۱۸ عیار`, Telegram bot `@zarorbitbot`, `0.00%` commission / transparent spread, cross-asset dealer-book ledger) with zero hardcoded fake market prices and zero internal design-sprint labels.
2. **Dedicated Ecosystem Roadmap ([`/vision`](../web/app/vision/page.tsx)):** Hosts the long-term multi-asset (`RWA`, `Crypto`, `NFT`, `Progressive DAO`, `AI/XR`) vision with explicit maturity tags.
3. **Synced Pure-Vector Assets:** `web/public/images/` holds byte-for-byte copies of the `S1` SVGs ([`zarorbit-logo-dark.svg`](../web/public/images/zarorbit-logo-dark.svg), [`zarorbit-symbol.svg`](../web/public/images/zarorbit-symbol.svg), [`zarorbit-favicon.svg`](../web/public/images/zarorbit-favicon.svg)) alongside regenerated `zarorbit-og.png` and `zarorbit-apple-touch-icon.png`.
4. **React Components:** [`BrandLogo.tsx`](../web/components/BrandLogo.tsx) renders the `S1` symbol and separated `[Symbol] ZARORBIT` lockup (`Manrope Bold` outlines, `fillRule="nonzero"`), and [`ZarorbitOrbitalMotion.tsx`](../web/components/ZarorbitOrbitalMotion.tsx) renders the 3D-spinning `S1` Zero-Interrupt Orbital Z centerpiece with `prefers-reduced-motion` support.
