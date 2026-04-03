# Design System Audit — OctopusCosyAnalyser

**Date:** 2026-04-03
**Scope:** `OctopusCosyAnalyser.Web` — Blazor Server frontend

---

## Summary

**Components reviewed:** 8 | **Issues found:** 18 | **Score: 54/100**

The design system has a solid token foundation in `app.css` and a coherent dark minimalist visual language. The critical gap is that the token system stops at semantic colors — typography, chart colors, and spacing are hardcoded throughout. No component documentation exists, and accessibility is largely unaddressed.

---

## Token Coverage

### What's Tokenised ✅

| Category | Tokens |
|----------|--------|
| Backgrounds | `--bg-base`, `--bg-surface`, `--bg-card`, `--bg-elevated` |
| Text | `--text-primary`, `--text-secondary`, `--text-muted` |
| Semantic colors | `--color-success`, `--color-warning`, `--color-danger`, `--color-primary`, `--color-info`, `--color-accent` |
| Borders | `--border-subtle`, `--border-card` |
| Border radius | `--radius-sm` (0.5rem), `--radius-md` (0.75rem), `--radius-lg` (1rem) |

### What's Hardcoded ⚠️

| Category | Instances | Example |
|----------|-----------|---------|
| Chart series colors | 14+ in `.razor` files | `rgb(0,123,255)`, `rgb(220,53,69)`, `rgb(255,193,7)` |
| Typography scale | 10+ sizes in `app.css` | `0.6rem`, `0.7rem`, `0.75rem`, `0.8rem`, `0.85rem`, `0.9rem`, `1.25rem`, `1.5rem`, `2rem` |
| Compare card accent colors | 4 in `app.css` | `#60a5fa` (A), `#f87171` (B) — not using `--color-primary`/`--color-danger` |
| Spacing values | ~20+ across `app.css` | `1.25rem`, `1.5rem`, `0.4rem`, `0.35rem` — no spacing scale |
| Chart-specific hex | 3 | `#0066cc`, `#ff6600`, `#dee2e6` — nowhere near token definitions |

---

## Naming Consistency

### Issues Found

| Issue | Affected Classes | Recommendation |
|-------|-----------------|----------------|
| Container naming inconsistency | `.gauge-section`, `.chart-section`, `.chart-header`, `.chart-body` | Standardise to `.[component]-section`, `.[component]-header`, `.[component]-body` |
| Toggle state suffix inconsistency | `.series-toggle-active` but `.status-pill` uses modifier via additional class (`.status-dot-success`) | Pick one pattern: BEM modifier (`.series-toggle--active`) or separate class (`.series-toggle-active`) — currently mixed |
| Compare card naming | `.compare-card-a`, `.compare-card-b` — letter suffixes are opaque | Rename to `.compare-card--baseline`, `.compare-card--change` or `.compare-card--period-a` |
| `metrics-strip` vs `metric-item` | Singular/plural mismatch in the same component | Both are correct English but should follow one convention |

### What's Consistent ✅
- All class names use kebab-case throughout
- Icon usage is uniform (`bi bi-*` Bootstrap Icons)
- Radzen component usage follows consistent prop patterns

---

## Component Inventory

| Component | Variants | States | Docs | A11y | Score |
|-----------|----------|--------|------|------|-------|
| **Gauge Card** | 1 | default, loading skeleton | ❌ | ❌ no ARIA | 4/10 |
| **Status Pill** | 4 (success/warning/danger/info) + pulsing dot | static, animated | ❌ | ❌ color only | 5/10 |
| **Metrics Strip** | 1 | default, loading | ❌ | ❌ | 4/10 |
| **Series Toggle** | 2 (active/inactive) | default, hover, active | ❌ | ❌ no role/label | 5/10 |
| **Period Selector** | 1 (1D/1W/1M/1Y) | default, active | ❌ | ❌ | 5/10 |
| **AI Panel** | 1 | expanded, collapsed | ❌ | ❌ | 4/10 |
| **Compare Cards** | 2 (A/B) | default | ❌ | ❌ | 4/10 |
| **Nav Item** | 1 | default, hover, active | ❌ | ⚠️ partial (`aria-current` missing) | 6/10 |

---

## Accessibility Findings

| Component | Issue | Fix |
|-----------|-------|-----|
| Status Pills | Status conveyed by color only | Add `role="status"` and `aria-label="Heating: active"` |
| Gauge Cards | RadzenRadialGauge has no text alternative | Wrap in `<figure>` with `<figcaption>` showing the value |
| Series Toggles | `<input type="checkbox">` used but label association unclear | Ensure `<label for="...">` wraps or references the RadzenCheckBox |
| Nav Items | No `aria-current="page"` on active link | Add `aria-current="page"` when route matches |
| AI Panel | Chevron toggle has no accessible label | Add `aria-expanded` and `aria-controls` to the toggle button |
| Pulsing dots | Animation not controllable | Respect `prefers-reduced-motion` — animation currently has no media query guard |

---

## Duplication

| Pattern | Where Duplicated | Impact |
|---------|-----------------|--------|
| Series toggle markup + CSS | `PerformanceTab.razor`, `ComfortTab.razor`, `AnalysisTab.razor` | Any style change requires 3 edits |
| Period selector (1D/1W/1M/1Y) | `History.razor`, `Costs.razor`, `Performance.razor` | Behavior inconsistent — date ranges computed differently per page |
| Loading skeleton pattern | `Dashboard.razor` and tab components | Copy-paste HTML with no shared component |

---

## Priority Actions

### 1. Add chart color tokens (High impact / Low effort)
Add `--chart-series-1` through `--chart-series-8` to `:root` in `app.css`. Replace 14+ hardcoded `rgb(...)` values in razor files. Prevents color collisions when adding new chart series.

```css
/* Proposed additions to :root */
--chart-series-1: #3b82f6;   /* blue — power/electricity */
--chart-series-2: #ef4444;   /* red — heat output */
--chart-series-3: #f59e0b;   /* amber — outdoor temp */
--chart-series-4: #22c55e;   /* green — COP */
--chart-series-5: #f97316;   /* orange — cost */
--chart-series-6: #06b6d4;   /* cyan — flow temp */
--chart-series-7: #8b5cf6;   /* violet — return temp */
--chart-series-8: #ec4899;   /* pink — spare */
```

### 2. Extract SeriesToggle and PeriodSelector into shared Blazor components (High impact / Medium effort)
Create `Components/Shared/SeriesToggle.razor` and `PeriodSelector.razor`. This eliminates 3× duplication, centralises behavior, and makes the period-selector date logic consistent.

### 3. Add typography tokens (Medium impact / Low effort)
Add a `--font-size-*` scale to `:root`. Map all hardcoded font-size values to these tokens.

```css
--font-size-xs:   0.65rem;
--font-size-sm:   0.75rem;
--font-size-base: 0.875rem;
--font-size-md:   1rem;
--font-size-lg:   1.25rem;
--font-size-xl:   1.5rem;
--font-size-2xl:  2rem;
```

### 4. Fix accessibility on Status Pills and Gauge Cards (Medium impact / Low effort)
These are the most user-visible components. Add `role`, `aria-label`, and `aria-current` attributes. Guard the pulsing animation with `prefers-reduced-motion`.

```css
@media (prefers-reduced-motion: reduce) {
  .status-dot-success { animation: none; }
}
```

### 5. Document the component library (Low impact now / High long-term value)
Create a `/docs/components.md` reference (or a live `/styleguide` Blazor page) cataloguing each component with usage guidance, props, and do/don't examples. Currently tribal knowledge — any new contributor has no reference.

---

## What's Working Well

- **Token foundation is solid** — the 4-layer background system (`base → surface → card → elevated`) is coherent and consistently applied
- **Semantic color usage** — `var(--color-success)`, `var(--color-danger)` etc. are used correctly for status states; no color-meaning mismatches found
- **Radzen overrides are clean** — CSS overrides for Radzen components are grouped and use tokens correctly (`.rz-tick-text`, `.rz-grid-line`, etc.)
- **Responsive breakpoint is simple and clear** — single breakpoint at 641px, not over-engineered
- **Dark-first from the start** — no `prefers-color-scheme` hacks; the system was designed dark, not retrofitted
