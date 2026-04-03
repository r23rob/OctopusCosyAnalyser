# OctopusCosyAnalyser — Component Reference

Design system for the Blazor Server frontend. Built on CSS custom properties, Bootstrap 5 (grid/buttons/forms), and Radzen Blazor (charts/gauges/tabs).

---

## Design Tokens

All tokens are defined in `wwwroot/app.css` under `:root`.

### Backgrounds
| Token | Value | Use |
|-------|-------|-----|
| `--bg-base` | `#0f1117` | Page background |
| `--bg-surface` | `#1a1d27` | Nav, tab bars |
| `--bg-card` | `#1e2130` | Cards, panels |
| `--bg-elevated` | `#262a3a` | Hover states, card headers |

### Text
| Token | Value | Use |
|-------|-------|-----|
| `--text-primary` | `rgba(255,255,255,0.92)` | Default text |
| `--text-secondary` | `rgba(255,255,255,0.6)` | Labels, descriptions |
| `--text-muted` | `rgba(255,255,255,0.38)` | Placeholders, ranges |

### Semantic Colors
| Token | Hex | Use |
|-------|-----|-----|
| `--color-primary` | `#3b82f6` | Actions, links, active states |
| `--color-primary-light` | `#60a5fa` | Compare period A, hover tints |
| `--color-success` | `#22c55e` | Good COP, online, success states |
| `--color-warning` | `#f59e0b` | Medium COP, warnings |
| `--color-danger` | `#ef4444` | Low COP, heating active, errors |
| `--color-danger-light` | `#f87171` | Compare period B |
| `--color-info` | `#06b6d4` | Idle state, informational |
| `--color-accent` | `#8b5cf6` | AI panel title, accent highlights |

### Chart Series Colors
Use these in `SeriesToggle` `Color` props and Radzen `Stroke`/`Fill` attributes. Radzen requires concrete hex strings (not CSS variables) — the token values are the canonical source of truth.

| Token | Hex | Assigned to |
|-------|-----|-------------|
| `--chart-1` | `#3b82f6` | Power / electricity (blue) |
| `--chart-2` | `#ef4444` | Heat output (red) |
| `--chart-3` | `#f59e0b` | Outdoor temp / heat out (amber) |
| `--chart-4` | `#22c55e` | COP (green) |
| `--chart-5` | `#f97316` | Cost (orange) |
| `--chart-6` | `#06b6d4` | Flow temp / WC min (cyan) |
| `--chart-7` | `#8b5cf6` | Zone setpoint (violet) |
| `--chart-8` | `#93c5fd` | Secondary / light blue |

### Typography Scale
| Token | Value | Used for |
|-------|-------|---------|
| `--font-size-2xs` | `0.6rem` | Metric ranges, gauge ticks |
| `--font-size-xs` | `0.65rem` | Metric labels, summary ranges |
| `--font-size-sm` | `0.7rem` | Card labels, toggle text, pill chevrons |
| `--font-size-base` | `0.75rem` | Status pills, period pills, table headers |
| `--font-size-md` | `0.8rem` | Chart titles, mini-trend headers |
| `--font-size-lg` | `0.85rem` | AI panel text, nav items |
| `--font-size-xl` | `0.9rem` | Setup card body text |
| `--font-size-2xl` | `1.1rem` | Setup card headings, delta values |
| `--font-size-metric` | `1.25rem` | Metrics strip values |
| `--font-size-summary` | `1.5rem` | Summary card values |
| `--font-size-gauge` | `2rem` | Gauge card hero values |

### Borders & Radius
| Token | Value |
|-------|-------|
| `--border-subtle` | `rgba(255,255,255,0.06)` |
| `--border-card` | `rgba(255,255,255,0.08)` |
| `--radius-sm` | `0.5rem` |
| `--radius-md` | `0.75rem` |
| `--radius-lg` | `1rem` |

---

## Shared Blazor Components

Located in `Components/Shared/`. Automatically available via `_Imports.razor`.

---

### `<SeriesToggle>`

Toggle button for showing/hiding a single chart series. Renders with a coloured dot and accessible `role="checkbox"`.

**File:** `Components/Shared/SeriesToggle.razor`

#### Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Color` | `string` | `"var(--chart-1)"` | CSS colour for the dot — use a `--chart-N` token or hex string |
| `Label` | `string` | `""` | Series name shown in the button and aria-label |
| `Active` | `bool` | `false` | Whether the series is visible — supports `@bind-Active` |
| `ActiveChanged` | `EventCallback<bool>` | — | Two-way binding callback |

#### Usage
```razor
<SeriesToggle Color="var(--chart-4)" Label="COP"      @bind-Active="showCop" />
<SeriesToggle Color="var(--chart-1)" Label="Power In" @bind-Active="showPowerIn" />
<SeriesToggle Color="var(--chart-3)" Label="Heat Out" @bind-Active="showHeatOut" />
```

#### Do's and Don'ts
| ✅ Do | ❌ Don't |
|------|---------|
| Use `--chart-N` token for `Color` so dots match chart lines | Hardcode `rgb(...)` strings |
| Group toggles inside `.chart-toggles` div | Use outside a `.chart-section` context |
| Keep labels short (1–2 words) | Use full sentences as labels |

---

### `<PeriodSelector>`

Pill-button group for selecting a time period. Supports two-way binding and a loading spinner.

**File:** `Components/Shared/PeriodSelector.razor`

#### Parameters
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `SelectedDays` | `int` | `7` | Currently selected period in days — supports two-way binding |
| `SelectedDaysChanged` | `EventCallback<int>` | — | Called when a period is selected |
| `IsLoading` | `bool` | `false` | Shows spinner when data is loading |
| `Options` | `List<PeriodOption>` | 1D/1W/1M/1Y | Override to provide custom period labels |

#### Usage (default periods)
```razor
<PeriodSelector SelectedDays="@selectedPeriodDays"
                SelectedDaysChanged="SetPeriod"
                IsLoading="@periodLoading" />
```

#### Usage (custom periods)
```razor
<PeriodSelector SelectedDays="@selectedPeriodDays"
                SelectedDaysChanged="SetPeriod"
                Options="@([ new(7, "Week"), new(30, "Month"), new(90, "Quarter") ])" />
```

---

## CSS-Only Components

These are defined purely in `app.css` and used via HTML + class names in razor files.

---

### Gauge Card

Displays a live metric with a Radzen radial gauge, a numeric value, and an optional unit.

**Classes:** `.gauge-card`, `.gauge-card-label`, `.gauge-card-value`, `.gauge-card-unit`

**Accessibility:** Add `role="figure"` and `aria-label="[Metric]: [value] [unit]"` to the card div. Add `aria-hidden="true"` to the `RadzenRadialGauge` since the value is shown as text.

```razor
<div class="gauge-card" role="figure" aria-label="Power Input: @PowerInputDisplay kW">
    <div class="gauge-card-label" aria-hidden="true">Power Input</div>
    <RadzenRadialGauge ... aria-hidden="true">...</RadzenRadialGauge>
    <div class="gauge-card-value">@PowerInputDisplay</div>
    <div class="gauge-card-unit">kW</div>
</div>
```

---

### Status Pill

Inline indicator combining a coloured dot and a text label.

**Classes:** `.status-pill`, `.status-dot`, `.status-dot-{success|danger|warning|info|muted}`, `.status-dot-pulse`

**Accessibility:** Add `aria-label` describing the status in plain language. Add `aria-hidden="true"` to the dot span. The pulse animation is automatically suppressed when `prefers-reduced-motion: reduce` is set.

```razor
<span class="status-pill" aria-label="Controller: heating">
    <span class="status-dot status-dot-danger status-dot-pulse" aria-hidden="true"></span>
    HEATING
</span>
```

| Dot class | Colour | Use |
|-----------|--------|-----|
| `.status-dot-success` | Green | Online, healthy, active good |
| `.status-dot-danger` | Red | Heating, error, critical |
| `.status-dot-warning` | Amber | Heat demand, delayed |
| `.status-dot-info` | Cyan | Idle, informational |
| `.status-dot-muted` | Grey | Unknown, unavailable |

---

### Metrics Strip

Horizontal bar of KPIs separated by 1px hairlines. Each item shows a label, value, and optional range.

**Classes:** `.metrics-strip`, `.metric-item`, `.metric-label`, `.metric-value`, `.metric-range`

```razor
<div class="metrics-strip">
    <div class="metric-item">
        <div class="metric-label">Avg COP</div>
        <div class="metric-value cop-green">3.42</div>
        <div class="metric-range">2.8 – 4.1</div>
    </div>
    ...
</div>
```

---

### AI Panel

Collapsible content panel. The header toggles body visibility.

**Classes:** `.ai-panel`, `.ai-panel-header`, `.ai-panel-title`, `.ai-panel-meta`, `.ai-panel-chevron`, `.ai-panel-chevron-open`, `.ai-panel-body`, `.ai-section`, `.ai-section-label`, `.ai-section-text`, `.ai-suggestions`

**Accessibility:** Add `role="button"`, `tabindex="0"`, `aria-expanded`, and `aria-controls` to the header. Add `id` matching `aria-controls` to the body.

```razor
<div class="ai-panel-header"
     @onclick="() => expanded = !expanded"
     role="button" tabindex="0"
     aria-expanded="@expanded.ToString().ToLower()"
     aria-controls="my-panel-body">
    <span class="ai-panel-title">Panel Title</span>
    <span class="ai-panel-chevron @(expanded ? "ai-panel-chevron-open" : "")" aria-hidden="true">▼</span>
</div>
@if (expanded)
{
    <div class="ai-panel-body" id="my-panel-body">
        ...
    </div>
}
```

---

### Compare Cards

Side-by-side comparison layout. Card A is blue-accented (baseline), card B is red-accented (after change).

**Classes:** `.compare-card`, `.compare-card-a`, `.compare-card-b`, `.compare-header-a`, `.compare-header-b`, `.compare-value-a`, `.compare-value-b`

Values are coloured with `--color-primary-light` (A) and `--color-danger-light` (B) via the `.compare-value-a/.compare-value-b` classes.
