# Octopus Energy Heat Pump GraphQL API Reference

**Endpoint:** `https://api.backend.octopus.energy/v1/graphql/`
**Auth:** `Authorization` header with JWT token (expires after ~1 hour)
**Account:** `A-1C3B4330`
**Controller EUID:** `00:1e:5e:09:02:ba:5d:84`
**Heat Pump Model:** Cosy 6 (hardware v5.1.0, firmware ESP32 v1.1.1)

---

## Queries (Read-Only)

### 1. `heatPumpControllerStatus`

**Purpose:** Get the real-time status of all sensors and zones. Use this to check what temperature sensors are reading right now, whether zones are calling for heat, and if sensors are online.

**Arguments:**

| Arg | Type | Required | Description |
|-----|------|----------|-------------|
| `accountNumber` | `String!` | Yes | Octopus account number (e.g. `"A-1C3B4330"`) |
| `euid` | `ID!` | Yes | Controller hardware ID (e.g. `"00:1e:5e:09:02:ba:5d:84"`) |

**Returns:** `HeatPumpControllerStatus`

**Full query with all fields:**
```graphql
query {
  heatPumpControllerStatus(accountNumber: "A-1C3B4330", euid: "00:1e:5e:09:02:ba:5d:84") {
    sensors {
      code
      connectivity {
        online
        retrievedAt
      }
      telemetry {
        temperatureInCelsius
        humidityPercentage
        rssi
        voltage
        retrievedAt
      }
    }
    zones {
      zone
      telemetry {
        setpointInCelsius
        mode
        relaySwitchedOn
        heatDemand
        retrievedAt
      }
    }
  }
}
```

**Response structure:**

- `sensors[]` — Array of all sensors attached to the controller
  - `code` — Sensor identifier. `ADC1`-`ADC4` are wired NTC temperature probes (ADC1 = hot water tank). `SENSOR01`-`SENSOR03` are wireless Zigbee room sensors.
  - `connectivity.online` — Boolean, whether the sensor is currently communicating
  - `connectivity.retrievedAt` — Timestamp of last connectivity check
  - `telemetry.temperatureInCelsius` — Current reading. `-273.1` means sensor is not connected/no reading.
  - `telemetry.humidityPercentage` — Only available on Zigbee sensors (null for ADC)
  - `telemetry.rssi` — Signal strength, only for Zigbee sensors (null for ADC)
  - `telemetry.voltage` — Battery voltage, only for Zigbee sensors (null for ADC)
  - `telemetry.retrievedAt` — Timestamp of the reading
- `zones[]` — Array of heating/water zones
  - `zone` — Zone identifier: `WATER`, `ZONE_1`, `ZONE_2`, `AUXILIARY`
  - `telemetry.setpointInCelsius` — Target temperature. `-300` means no active setpoint (e.g. water zone when off).
  - `telemetry.mode` — Current mode: `AUTO`, `OFF`, `ON`, `BOOST`
  - `telemetry.relaySwitchedOn` — Boolean, whether the zone relay is currently energised
  - `telemetry.heatDemand` — Boolean, whether the zone is actively requesting heat

**When to use:** Check current sensor readings, verify sensors are online, see which zones are active, check if heating is running.

---

### 2. `heatPumpControllerConfiguration`

**Purpose:** Get the full configuration of the controller, zones, schedules, and heat pump settings. Use this to understand the system setup, read schedules, check weather compensation settings, and see sensor assignments.

**Arguments:**

| Arg | Type | Required | Description |
|-----|------|----------|-------------|
| `accountNumber` | `String!` | Yes | Octopus account number |
| `euid` | `ID!` | Yes | Controller EUID |

**Returns:** `ControllerAndZoneConfiguration`

**Full query with all fields:**
```graphql
query {
  heatPumpControllerConfiguration(accountNumber: "A-1C3B4330", euid: "00:1e:5e:09:02:ba:5d:84") {
    controller {
      firmwareConfiguration {
        esp32
        efr32
        eui
      }
      state
      accessPointPassword
      heatPumpTimezone
      connected
      lastReset
    }
    zones {
      configuration {
        code
        zoneType
        enabled
        displayName
        primarySensor
        currentOperation {
          mode
          setpointInCelsius
          action
          end
        }
        previousOperation {
          mode
          setpointInCelsius
          action
        }
        callForHeat
        heatDemand
        emergency
        sensors {
          ... on ADCSensorConfiguration {
            code
            displayName
            type
            enabled
          }
          ... on ZigbeeSensorConfiguration {
            code
            displayName
            type
            id
            firmwareVersion
            boostEnabled
          }
        }
      }
      schedules {
        days
        settings {
          startTime
          action
          setpointInCelsius
          zoneState
        }
      }
    }
    heatPump {
      serialNumber
      model
      hardwareVersion
      faultCodes
      weatherCompensation {
        enabled
        allowableMinimumTemperatureRange {
          minimum { value unit }
          maximum { value unit }
        }
        allowableMaximumTemperatureRange {
          minimum { value unit }
          maximum { value unit }
        }
        currentRange {
          minimum { value unit }
          maximum { value unit }
        }
      }
      heatingFlowTemperature {
        allowableRange {
          minimum { value unit }
          maximum { value unit }
        }
        currentTemperature { value unit }
      }
      manifoldEnabled
      hasHeatPumpCompatibleCylinder
      maxWaterSetpoint
      minWaterSetpoint
      latestCounterReset
      quieterModeEnabled
    }
  }
}
```

**Response structure:**

- `controller` — Hardware and connection info
  - `firmwareConfiguration.esp32` — WiFi chip firmware version
  - `firmwareConfiguration.efr32` — Zigbee chip firmware version
  - `state` — Array of states, e.g. `["NORMAL_MODE"]`
  - `connected` — Boolean, whether controller is online
  - `lastReset` — When the controller last rebooted
  - `heatPumpTimezone` — POSIX timezone string
- `zones[]` — Array of zone configs and schedules
  - `configuration.code` — `WATER`, `ZONE_1`, `ZONE_2`, `AUXILIARY`
  - `configuration.zoneType` — `WATER` or `HEAT`
  - `configuration.enabled` — Whether this zone is active
  - `configuration.primarySensor` — Which sensor controls this zone (e.g. `SENSOR01`)
  - `configuration.currentOperation.mode` — `AUTO`, `OFF`, `ON`, `BOOST`
  - `configuration.emergency` — True if zone is in emergency/fallback mode
  - `configuration.sensors[]` — Sensors assigned to this zone. Uses GraphQL union type:
    - `ADCSensorConfiguration` for wired sensors (fields: `code`, `displayName`, `type`, `enabled`)
    - `ZigbeeSensorConfiguration` for wireless sensors (adds: `id`, `firmwareVersion`, `boostEnabled`)
  - `schedules[].days` — 7-char binary string for days of week (Mon-Sun), e.g. `"1111111"` = every day
  - `schedules[].settings[]` — Time slots with `startTime` (HH:MM:SS), `setpointInCelsius`, `zoneState` (`ON`/`OFF`)
- `heatPump` — Hardware config
  - `model` — e.g. `"Cosy 6"`
  - `faultCodes` — Array of active fault codes (empty = no faults)
  - `weatherCompensation.enabled` — Whether weather comp is active
  - `weatherCompensation.currentRange` — Current min/max flow temps based on weather comp curve
  - `heatingFlowTemperature.currentTemperature` — Current flow temp setting
  - `heatingFlowTemperature.allowableRange` — Min/max allowed flow temperature
  - `maxWaterSetpoint` / `minWaterSetpoint` — Hot water temperature limits
  - `quieterModeEnabled` — Whether quiet mode is on (reduces fan speed)

**When to use:** Read zone schedules, check weather compensation config, verify sensor assignments, check firmware versions, read fault codes, understand system topology.

---

### 3. `heatPumpLivePerformance`

**Purpose:** Get the current instantaneous performance reading. Use this for a quick "how is it doing right now" check.

**Arguments:**

| Arg | Type | Required | Description |
|-----|------|----------|-------------|
| `accountNumber` | `String!` | Yes | Octopus account number |
| `euid` | `ID!` | Yes | Controller EUID |

**Returns:** `HeatPumpLivePerformance`

**Full query:**
```graphql
query {
  heatPumpLivePerformance(accountNumber: "A-1C3B4330", euid: "00:1e:5e:09:02:ba:5d:84") {
    readAt
    coefficientOfPerformance
    powerInput { value unit }
    heatOutput { value unit }
    outdoorTemperature { value unit }
  }
}
```

**Response fields:**

| Field | Unit | Description |
|-------|------|-------------|
| `readAt` | ISO datetime | When the reading was taken |
| `coefficientOfPerformance` | Decimal string | Current CoP (heat out / electricity in). Higher = more efficient. |
| `powerInput.value` | `KILOWATT` | Current electrical power consumption |
| `heatOutput.value` | `KILOWATT` | Current heat being produced |
| `outdoorTemperature.value` | `DEGREES_CELSIUS` | Current outdoor temp from heat pump's sensor |

**When to use:** Quick snapshot of current performance. Is the pump running? What's the outdoor temp? What CoP is it achieving right now?

---

### 4. `heatPumpLifetimePerformance`

**Purpose:** Get the all-time cumulative performance since installation (or last counter reset). Use this for the big-picture efficiency number.

**Arguments:**

| Arg | Type | Required | Description |
|-----|------|----------|-------------|
| `accountNumber` | `String!` | Yes | Octopus account number |
| `euid` | `ID!` | Yes | Controller EUID |

**Returns:** `HeatPumpLifetimePerformance`

**Full query:**
```graphql
query {
  heatPumpLifetimePerformance(accountNumber: "A-1C3B4330", euid: "00:1e:5e:09:02:ba:5d:84") {
    readAt
    seasonalCoefficientOfPerformance
    energyInput { value unit }
    heatOutput { value unit }
  }
}
```

**Response fields:**

| Field | Unit | Description |
|-------|------|-------------|
| `readAt` | ISO datetime | When the reading was taken |
| `seasonalCoefficientOfPerformance` | Decimal string | Lifetime average CoP (SCOP). Accounts for all seasons. |
| `energyInput.value` | `KILOWATT_HOUR` | Total electricity consumed since counter reset |
| `heatOutput.value` | `KILOWATT_HOUR` | Total heat produced since counter reset |

**When to use:** Get the lifetime SCOP, total energy consumed/produced. Good for answering "how efficient has my heat pump been overall?"

---

### 5. `heatPumpTimeRangedPerformance`

**Purpose:** Get a single aggregated performance summary for any date range. Returns one CoP number and total energy figures for the entire period. No time series — just totals.

**Arguments:**

| Arg | Type | Required | Description |
|-----|------|----------|-------------|
| `accountNumber` | `String!` | Yes | Octopus account number |
| `euid` | `ID!` | Yes | Controller EUID |
| `startAt` | `DateTime!` | Yes | Start of period (ISO format) |
| `endAt` | `DateTime!` | Yes | End of period (ISO format) |

**Returns:** `HeatPumpTimeRangedPerformance`

**Full query:**
```graphql
query {
  heatPumpTimeRangedPerformance(
    accountNumber: "A-1C3B4330"
    euid: "00:1e:5e:09:02:ba:5d:84"
    startAt: "2026-04-02T00:00:00Z"
    endAt: "2026-04-09T00:00:00Z"
  ) {
    coefficientOfPerformance
    energyInput { value unit }
    energyOutput { value unit }
  }
}
```

**Response fields:**

| Field | Unit | Description |
|-------|------|-------------|
| `coefficientOfPerformance` | Decimal string | Average CoP for the period |
| `energyInput.value` | `KILOWATT_HOUR` | Total electricity consumed in the period |
| `energyOutput.value` | `KILOWATT_HOUR` | Total heat produced in the period |

**When to use:** Get a single summary number for any date range. "What was my CoP last week?" "How much energy did I use last month?" Does NOT return a breakdown over time — use `heatPumpTimeSeriesPerformance` for that.

---

### 6. `heatPumpTimeSeriesPerformance`

**Purpose:** Get time-bucketed performance data for charting and trending. Returns an array of entries, each covering a time bucket. The bucket size depends on the `performanceGrouping` parameter.

**Arguments:**

| Arg | Type | Required | Description |
|-----|------|----------|-------------|
| `accountNumber` | `String!` | Yes | Octopus account number |
| `euid` | `ID!` | Yes | Controller EUID |
| `startAt` | `DateTime!` | Yes | Start of period (ISO format) |
| `endAt` | `DateTime!` | Yes | End of period (ISO format) |
| `performanceGrouping` | `PerformanceGrouping!` | Yes | One of: `LIVE`, `DAY`, `WEEK`, `MONTH`, `YEAR` |

**Returns:** `[HeatPumpTimeSeriesPerformance]` (array)

**Full query:**
```graphql
query {
  heatPumpTimeSeriesPerformance(
    accountNumber: "A-1C3B4330"
    euid: "00:1e:5e:09:02:ba:5d:84"
    startAt: "2026-04-08T00:00:00Z"
    endAt: "2026-04-09T23:00:00Z"
    performanceGrouping: DAY
  ) {
    startAt
    endAt
    energyInput { value unit }
    energyOutput { value unit }
    outdoorTemperature { value unit }
  }
}
```

**Response fields (per entry):**

| Field | Unit | Description |
|-------|------|-------------|
| `startAt` | ISO datetime | Start of this time bucket |
| `endAt` | ISO datetime | End of this time bucket |
| `energyInput.value` | `KILOWATT_HOUR` | Electricity consumed in this bucket |
| `energyOutput.value` | `KILOWATT_HOUR` | Heat produced in this bucket |
| `outdoorTemperature.value` | `DEGREES_CELSIUS` | Average outdoor temp during this bucket |

#### `performanceGrouping` Options — CRITICAL CONSTRAINTS

| Grouping | Bucket Size | Max Date Window | Typical Entries | Best For |
|----------|-------------|-----------------|-----------------|----------|
| `LIVE` | **1 minute** | **< 1 hour** | ~60 | Real-time monitoring, see minute-by-minute activity |
| `DAY` | **1 hour** | **< 2 days** | ~48 | Hourly patterns within a day or two |
| `WEEK` | **1 day** | **< 14 days** | ~14 | Daily totals over a week or two |
| `MONTH` | **1 day** | **< 60 days** | ~60 | Daily totals over a month or two |
| `YEAR` | **1 month** | **< 13 months** | ~12 | Monthly totals for seasonal comparison |

**IMPORTANT: The API enforces strict max window sizes.** If you exceed the limit, you get error `OE-0315` with a validation message like:

- LIVE: `"The time duration between start at and end at for LIVE mode should be less than relativedelta(hours=+1)."`
- DAY: `"...for DAY mode should be less than relativedelta(days=+2)."`
- WEEK: `"...for WEEK mode should be less than relativedelta(days=+14)."`
- MONTH: `"...for MONTH mode should be less than relativedelta(days=+60)."`
- YEAR: `"Start at should not be earlier than 13 months ago."`

**When to use:** Any time you need a time-series breakdown for charts, graphs, or trend analysis. Choose the grouping based on what level of detail you need and how far back you're looking.

---

### 7. `heatPumpQuote` (requires UUID)

**Purpose:** Retrieve a heat pump installation quote by its unique code.

**Arguments:**

| Arg | Type | Required |
|-----|------|----------|
| `code` | `UUID!` | Yes |

**Fields:** `code`, `address { line1 line2 postalCode city state geographicalId country }`, `eligibility { isEligible ineligibilityReason }`, `status`, `quotedProducts { price product { code } pricingMethod estimatedRunningCosts { tariffName cost timePeriod } status }`, `clientParams`

**When to use:** Look up a specific quote during the sales/installation journey. Requires the quote UUID.

---

### 8. `heatPumpQuoteFinanceOffer` (requires UUID)

**Purpose:** Get finance offer details for a heat pump quote.

**Arguments:**

| Arg | Type | Required |
|-----|------|----------|
| `code` | `UUID!` | Yes |

**Fields:** `term`, `depositPercentage`, `price`, `apr`, `availableTerms`

**When to use:** Check finance options for a quote. Requires the quote UUID.

---

### 9. `heatPumpControllersAtLocation` (requires propertyId)

**Purpose:** List all heat pump controllers at a property.

**Arguments:**

| Arg | Type | Required |
|-----|------|----------|
| `accountNumber` | `String!` | Yes |
| `propertyId` | `ID!` | Yes |

**Fields:** `controller { euid }`, `heatPump { model }`

**When to use:** Discover which controllers/heat pumps exist at a property. Useful if you don't already know the EUID.

---

## Mutations (Write Operations)

These mutations modify the heat pump configuration. Each takes an `input` object. **Do not call these without explicit user intent.**

### Control Mutations (safe for automation with user permission)

| Mutation | Input Type | Purpose |
|----------|-----------|---------|
| `heatPumpSetZoneMode` | `HeatPumpSetZoneModeInput!` | Change a zone's operating mode (AUTO/OFF/ON/BOOST) |
| `heatPumpSetZoneSchedules` | `HeatPumpSetZoneSchedulesInput!` | Update the heating schedule for a zone |
| `heatPumpSetHushMode` | `HeatPumpSetHushModeInput!` | Enable/disable quiet mode |
| `heatPumpSetZonePrimarySensor` | `HeatPumpSetZonePrimarySensorInput!` | Change which sensor controls a zone |
| `heatPumpBulkUpdateSensorDisplayName` | `HeatPumpBulkUpdateSensorDisplayNameInput!` | Rename sensors |
| `heatPumpBulkUpdateZoneDisplayNames` | `HeatPumpBulkUpdateZoneDisplayNamesInput!` | Rename zones |
| `heatPumpUpdateFlowTemperatureConfiguration` | `HeatPumpUpdateFlowTemperatureConfigurationInput!` | Change flow temperature / weather comp settings |
| `heatPumpSetupSmartControl` | `HeatPumpSetupSmartControlInput!` | Configure smart control features |

### Installation/Provisioning Mutations (admin use only)

| Mutation | Input Type | Purpose |
|----------|-----------|---------|
| `heatPumpRequestProvisioningClaimCertificate` | `HeatPumpRequestProvisioningClaimCertificateInput!` | Request provisioning certificate |
| `heatPumpMarkAsDeprovisioned` | `HeatPumpMarkAsDeprovisionedInput!` | Mark controller as removed |

### Quote/Sales Mutations (sales journey only)

| Mutation | Input Type | Purpose |
|----------|-----------|---------|
| `createHeatPumpQuote` | `CreateHeatPumpQuoteInput!` | Create a new quote |
| `updateHeatPumpQuote` | `UpdateHeatPumpQuoteInput!` | Update quote details |
| `acceptHeatPumpQuote` | `AcceptHeatPumpQuoteInput!` | Accept a quote |
| `emailHeatPumpQuote` | `EmailHeatPumpQuoteInput!` | Email a quote |
| `selectHeatPumpQuoteProduct` | `SelectHeatPumpQuoteProductInput!` | Select a product from quote options |
| `initiateHeatPumpDepositPayment` | `InitiateHeatPumpDepositPaymentInput!` | Start deposit payment |
| `confirmHeatPumpQuoteDepositPayment` | `ConfirmHeatPumpQuoteDepositPaymentInput!` | Confirm deposit payment |
| `updateHeatPumpQuoteFinanceOfferTerms` | `UpdateHeatPumpQuoteFinanceOfferTermsInput!` | Change finance terms |
| `requestHeatPumpQuoteCallback` | `RequestHeatPumpQuoteCallbackInput!` | Request a callback |
| `requestHeatPumpDepositFollowUp` | `RequestHeatPumpDepositFollowUpInput!` | Follow up on deposit |
| `requestIneligibleHeatPumpQuoteFollowUp` | `RequestIneligibleHeatPumpQuoteFollowUpInput!` | Follow up on ineligible quote |
| `setHeatPumpQuoteJourneyVersion` | `SetHeatPumpQuoteJourneyVersionInput!` | Set quote journey version |

---

## Quick Decision Guide

| I want to... | Use this query | Grouping |
|--------------|---------------|----------|
| Check if heating is running right now | `heatPumpControllerStatus` | — |
| See current room temperatures | `heatPumpControllerStatus` | — |
| Check current outdoor temp | `heatPumpLivePerformance` | — |
| See current CoP and power usage | `heatPumpLivePerformance` | — |
| Get lifetime efficiency (SCOP) | `heatPumpLifetimePerformance` | — |
| Get total energy for a period | `heatPumpTimeRangedPerformance` | — |
| Compare CoP across date ranges | `heatPumpTimeRangedPerformance` (multiple calls) | — |
| Read the heating schedule | `heatPumpControllerConfiguration` | — |
| Check for fault codes | `heatPumpControllerConfiguration` | — |
| See weather compensation settings | `heatPumpControllerConfiguration` | — |
| Plot minute-by-minute energy (last hour) | `heatPumpTimeSeriesPerformance` | `LIVE` |
| Plot hourly energy for today | `heatPumpTimeSeriesPerformance` | `DAY` |
| Plot daily energy for the last 2 weeks | `heatPumpTimeSeriesPerformance` | `WEEK` |
| Plot daily energy for the last 2 months | `heatPumpTimeSeriesPerformance` | `MONTH` |
| Plot monthly energy for the year | `heatPumpTimeSeriesPerformance` | `YEAR` |
| Change a zone mode (e.g. turn on boost) | `heatPumpSetZoneMode` mutation | — |
| Update the schedule | `heatPumpSetZoneSchedules` mutation | — |

---

## Known Sensor Mappings (This System)

| Code | Type | Location | Zone |
|------|------|----------|------|
| `ADC1` | NTC (wired) | Hot water tank | WATER |
| `ADC2` | NTC (wired) | Unused (-273.1°C) | WATER |
| `ADC3` | NTC (wired) | Unused (-273.1°C) | WATER |
| `ADC4` | NTC (wired) | Unused (-273.1°C) | WATER |
| `SENSOR01` | Zigbee (wireless) | Lounge | ZONE_1 (primary) |
| `SENSOR02` | Zigbee (wireless) | Lily's room | ZONE_1 |
| `SENSOR03` | Zigbee (wireless) | Rob's Office | ZONE_1 |

## Zone Configuration (This System)

| Zone | Type | Enabled | Primary Sensor | Mode |
|------|------|---------|----------------|------|
| `WATER` | Water heating | Yes | ADC1 | AUTO |
| `ZONE_1` | Space heating | Yes | SENSOR01 (lounge) | AUTO |
| `ZONE_2` | Space heating | No | SENSOR02 | OFF |
| `AUXILIARY` | Space heating | No | None | OFF |

---

## HTTP Headers Required

```json
{
  "Content-Type": "application/json",
  "Authorization": "<JWT_TOKEN>"
}
```

The JWT token is obtained by logging in via the `obtainKrakenToken` mutation or the Octopus Energy auth flow. Tokens expire after approximately 1 hour. If you receive `"'Authorization' header has expired."`, you need a fresh token.
