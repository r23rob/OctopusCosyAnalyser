# Octopus Energy GraphQL API Schema Reference

> Auto-generated from introspection of `https://api.backend.octopus.energy/v1/graphql/`
> Generated: 2026-04-05

This documents queries and mutations available in the Octopus Energy GraphQL API,
focusing on heat pump, cosy, account, and energy-related operations.

## Heat Pump Queries

### `heatPumpControllerConfiguration`
> The configuration of the heat pump controller and its zones.
```graphql
heatPumpControllerConfiguration(accountNumber: String!, euid: ID!): ControllerAndZoneConfiguration
```
**Return type fields:**
- `controller`: ControllerConfiguration — Controller configuration.
  - `firmwareConfiguration`: FirmwareConfiguration — Controller firmware options for ESP and EFR chips.
    - `esp32`: String — ESP32 firmware version (EFI System Partition).
    - `efr32`: String — EFR32 firmware version (Emergency Firmware Recovery).
    - `eui`: String — The EUI of the controller being queried.
  - `state`: [State] — Current state of the controller.
  - `accessPointPassword`: String — 8 character password used to access the access point mode webpage.
  - `heatPumpTimezone`: String — Timezone the controller is in.
  - `connected`: Boolean — Whether or not the controller is currently connected.
  - `lastReset`: DateTime — When the controller was last reset.
- `zones`: [ZoneInformation] — List of zones with their configuration and schedules.
  - `configuration`: ZoneConfiguration — Configuration for a zone.
    - `code`: Zone — Code name of the zone.
    - `zoneType`: ZoneType — What the zone is used for (heating, water etc).
    - `enabled`: Boolean — Enabled allows zone to heat when `callForHeat` is True.
    - `displayName`: String — User determined name for a zone.
    - `primarySensor`: String — Primary sensor for the zone.
    - `currentOperation`: ZoneCurrentOperation — Current operation.
    - `previousOperation`: ZonePreviousOperation — Previous operation.
    - `callForHeat`: Boolean — Whether the zone is calling for heat.
    - `heatDemand`: Boolean — Whether there is demand for heat (True) or not (False).
    - `emergency`: Boolean — Default mode, if the zone loses connection to the primary sensor.
    - `sensors`: [SensorConfiguration] — All associated sensors and ADCs.
  - `schedules`: [Schedule] — The heating schedules being used by this zone.
    - `days`: String — A bitmask of the days this program is active for.
    - `settings`: [Setting] — A list of the Setting objects which make up this schedule.
- `heatPump`: HeatPumpConfiguration — Controller configuration.
  - `serialNumber`: String — The serial number of the heat pump.
  - `model`: String — The heat pump model (eg. COSY).
  - `hardwareVersion`: String — The hardware version.
  - `faultCodes`: [String] — Any fault codes present on the device.
  - `weatherCompensation`: WeatherCompensationConfiguration — The current weather compensation settings.
    - `enabled`: Boolean! — Whether weather compensation is enabled or not.
    - `allowableMinimumTemperatureRange`: TemperatureRange! — The allowable range for the minimum value of flow temperature.
    - `allowableMaximumTemperatureRange`: TemperatureRange! — The allowable range for the maximum value of flow temperature.
    - `currentRange`: TemperatureRange! — The min and max flow temperatures currently selected by the user.
  - `heatingFlowTemperature`: FlowTemperatureConfiguration — The flow temperature set for heating.
    - `allowableRange`: TemperatureRange! — The minimum and maximum temperatures which may be selected, dictated by the device.
    - `currentTemperature`: Temperature! — The flow temperature currently selected by the user.
  - `manifoldEnabled`: Boolean — Whether the system has a manifold set up.
  - `hasHeatPumpCompatibleCylinder`: Boolean — Whether the system has a hot water cylinder (tank) which is compatible with heat pumps.
  - `maxWaterSetpoint`: Float — The maximum temperature which the hot water can be set to.
  - `minWaterSetpoint`: Float — The minimum temperature which the hot water can be set to.
  - `latestCounterReset`: DateTime — The last time the heat pump metrics were reset to zero.
  - `quieterModeEnabled`: Boolean — Whether quieter mode is enabled for this heat pump controller.

### `heatPumpControllerStatus`
> Retrieve the heat pump controller device status.
```graphql
heatPumpControllerStatus(accountNumber: String!, euid: ID!): ControllerStatus
```
**Return type fields:**
- `sensors`: [SensorStatus] — The status and telemetry information of sensors.
  - `code`: String — The code name for a sensor.
  - `connectivity`: SensorConnectivity — The connectivity/online status of a sensor.
    - `online`: Boolean — Whether or not a sensor is currently online.
    - `retrievedAt`: DateTime — Time at which the status was retrieved.
  - `telemetry`: SensorTelemetry — The telemetry data of a sensor.
    - `temperatureInCelsius`: Float — Temperature recorded by a sensor in celsius.
    - `humidityPercentage`: Int — Percentage humidity recorded by the sensor.
    - `rssi`: Int — Signal strength of a sensor measured in dBm (Received Signal Strength Indicator).
    - `voltage`: Float — Voltage recorded by a sensor.
    - `retrievedAt`: DateTime — Time at which the telemetry was retrieved.
- `zones`: [ZoneStatus] — The status and telemetry information of zones.
  - `zone`: Zone — The heat pump controller zone, i.e. WATER, ZONE_1, ZONE_2 or AUXILIARY.
  - `telemetry`: ZoneTelemetry — The telemetry data of a zone.
    - `setpointInCelsius`: Float — Target temperature for a zone, in celsius.
    - `mode`: Mode — Heating mode in zone.
    - `relaySwitchedOn`: Boolean — Whether relay is currently switched on (True) or off (False).
    - `heatDemand`: Boolean — Whether there is demand for heat (True) or not (False).
    - `retrievedAt`: DateTime — Time at which the telemetry was retrieved.

### `heatPumpControllersAtLocation`
> The heat pump controllers attached to an account at a given location.
```graphql
heatPumpControllersAtLocation(accountNumber: String!, propertyId: ID!): [ControllerAtLocation]
```
**Return type fields:**
- `controller`: Controller! — The controller attached to the account.
  - `euid`: String! — The EUID of the controller.
- `heatPumpModel`: String! — The model of the heat pump.
- `location`: Location! — The location at which the device exists.
  - `propertyId`: ID! — The property ID of the location.
- `provisionedAt`: DateTime — When the controller was provisioned (at this location for this account).

### `heatPumpLifetimePerformance`
> Heat pump lifetime performance data.
```graphql
heatPumpLifetimePerformance(accountNumber: String!, euid: ID!): LifetimeTelemetry
```
**Return type fields:**
- `readAt`: DateTime! — When these measurements were taken.
- `seasonalCoefficientOfPerformance`: Decimal — The average lifetime coefficient of performance up to the time of measurement.
- `energyInput`: Energy! — The electrical energy drawn by the heat pump over its lifetime.
  - `value`: Decimal — The amount of energy (not power) transmitted.
  - `unit`: EnergyUnit! — The units in which the energy is being measured.
- `heatOutput`: Energy! — The heat energy emitted by the heat pump over its lifetime.
  - `value`: Decimal — The amount of energy (not power) transmitted.
  - `unit`: EnergyUnit! — The units in which the energy is being measured.

### `heatPumpQuote`
> The heat pump quote which was given to the customer.
```graphql
heatPumpQuote(code: UUID!): HeatPumpQuote
```
**Return type fields:**
- `code`: UUID! — The quote code.
- `address`: Address! — The address the quote is for.
  - `line1`: NonEmptyString! — Address line 1 (e.g., street, PO Box, or company name).
  - `line2`: String — Address line 2 (e.g., apartment, suite, unit, or building).
  - `postalCode`: NonEmptyString! — ZIP or postal code.
  - `city`: String — City, district, suburb, town, or village.
  - `state`: String — State, county, province, or region.
  - `geographicalId`: String — Country-specific unique identifier for an address, e.g. the UPRN in GB.
  - `country`: CountryCode — Two-letter country code.
- `eligibility`: QuoteEligibility — Whether this quote is eligible to proceed.
  - `isEligible`: Boolean! — Whether the quote is eligible or not.
  - `ineligibilityReason`: HeatPumpQuoteIneligibilityReason — The reason this quote is ineligible, if ineligible.
- `status`: HeatPumpQuoteStatus! — The status of the quote.
- `quotedProducts`: [QuotedProduct] — The quoted products the customer can choose from.
  - `price`: Int — The quoted product price, in the currency's minor unit.
  - `product`: Product — The canonical product quoted.
    - `code`: String! — The unique product code.
  - `pricingMethod`: HeatPumpPricingMethod — Pricing method.
  - `estimatedRunningCosts`: [EstimatedRunningCost] — The estimated running costs.
    - `tariffName`: String — The tariff name.
    - `cost`: Int — The estimated cost of running this product on this tariff.
    - `timePeriod`: HeatPumpRunningCostTimePeriod — The time period this running cost is calculated for.
  - `status`: QuotedProductStatus! — The status of the quoted product.
- `clientParams`: JSONString — A JSON object containing any client params stored on the quote.

### `heatPumpQuoteFinanceOffer`
> The heat pump finance offer which was selected by the customer for the given quote.
```graphql
heatPumpQuoteFinanceOffer(code: UUID!): HeatPumpQuoteFinanceOffer
```
**Return type fields:**
- `term`: PositiveInt — The term selected by the customer in months.
- `depositPercentage`: Percentage — The deposit percentage selected by the customer (e.g., 5%, 10%..).
- `price`: PositiveInt — The price of the cheapest quoted product in the currency's minor unit.
- `apr`: Decimal — The interest rate for the finance offer loan (e.g., 5.50 for 5.5%).
- `availableTerms`: [Int] — List of available terms in months.

### `heatPumpTimeRangedPerformance`
> Heat pump performance data between two specific dates.
```graphql
heatPumpTimeRangedPerformance(accountNumber: String!, euid: ID!, startAt: DateTime!, endAt: DateTime!): HeatPumpTimeRangedPerformance
```
**Return type fields:**
- `coefficientOfPerformance`: Decimal — The coefficient of performance (COP) of the heat pump.
- `energyInput`: Energy! — The field to show energy input.
  - `value`: Decimal — The amount of energy (not power) transmitted.
  - `unit`: EnergyUnit! — The units in which the energy is being measured.
- `energyOutput`: Energy! — The field to show energy output.
  - `value`: Decimal — The amount of energy (not power) transmitted.
  - `unit`: EnergyUnit! — The units in which the energy is being measured.

### `heatPumpTimeSeriesPerformance`
> Heat pump performance time series data.
```graphql
heatPumpTimeSeriesPerformance(accountNumber: String!, euid: ID!, startAt: DateTime!, endAt: DateTime!, performanceGrouping: PerformanceGrouping!): [HeatPumpPerformanceGroupingNode]
```
**Return type fields:**
- `startAt`: DateTime! — The start datetime of the node.
- `endAt`: DateTime! — The end datetime of the node.
- `energyInput`: Energy — The field to show energy input.
  - `value`: Decimal — The amount of energy (not power) transmitted.
  - `unit`: EnergyUnit! — The units in which the energy is being measured.
- `energyOutput`: Energy — The field to show energy output.
  - `value`: Decimal — The amount of energy (not power) transmitted.
  - `unit`: EnergyUnit! — The units in which the energy is being measured.
- `outdoorTemperature`: Temperature — The field to show outdoor temperature.
  - `value`: Decimal — The temperature measured.
  - `unit`: TemperatureUnit! — The units in which the temperature is being measured.

## Cosy Queries

### `cosyCharacterisation`
> Average coefficient of performance metrics (COPs) for the entire cosy fleet against external temperature.
```graphql
cosyCharacterisation: [CosyCharacterisation!]!
```
**Return type fields:**
- `averageExternalTemperature`: Int! — The readings directly from the heatpumps themselves.
- `medianCop`: Float! — The level of efficiency at that temperature.

### `cosyDistribution`
> The distribution of seasonal performance factors (SPF) across the Cosy fleet.
```graphql
cosyDistribution: [CosyDistribution!]!
```
**Return type fields:**
- `deviceSpf`: Float! — The seasonal performance factor for a device.
- `devicePercentage`: Float! — The percentage of the total fleet with this SPF.

### `cosyEfficiency`
> The average coefficient of performance (COPs) for the entire cosy fleet historically.
```graphql
cosyEfficiency: CosyEfficiency
```
**Return type fields:**
- `averageCop30Day`: Float! — The average coefficient of performance over the last 30 days. (used for measuring how efficient the device is for transferring energy to heat).
- `averageCop90Day`: Float! — The average coefficient of performance over the last 90 days. (used for measuring how efficient the device is for transferring energy to heat).
- `averageSpf`: Float! — The average coefficient of performance over the devices whole lifespan (to show how efficient its been overall)

### `cosyRegional`
> The median seasonal performance factor (SPF) per region across the Cosy fleet.
```graphql
cosyRegional: [CosyRegional!]!
```
**Return type fields:**
- `lctRegion`: String! — The low carbon technology (LCT) region identifier.
- `medianSpf`: Float! — The median seasonal performance factor (SPF) for this region.
- `averageAnnualSavings`: Float — The average annual savings for customers in this region.

### `cosySavings`
> The average annual savings for Cosy tariff customers.
```graphql
cosySavings: [CosySavings!]!
```
**Return type fields:**
- `tariffType`: String! — The tariff type.
- `averageAnnualSavings`: Float! — The average annual savings for this tariff type.

## Account & Meter Queries

### `account`
```graphql
account(accountNumber: String!): AccountType
```
**Return type fields:**
- `number`: String! — The account number.
- `canModifyPayments`: CanModifyPayments
  - `canAmendPayments`: Boolean — Whether or not the account can amend payments.
  - `canCancelPayments`: Boolean — Whether or not the account can cancel payments.

### `meterPoint`
> The meter point.
```graphql
meterPoint(mpxn: String!, fuelType: FuelType!): MeterPoint
```
**Return type fields:**
- `supplyStatus`: SupplyStatus! — The supply status of this meter point.
- `occupierFrom`: Date — The date the previous tenant moved out and the account associated with the meter point became an occupier account, if the meter point has a supply status of OCCUPIER.

### `supplyPointMetaData`
> Retrieve the supply point metadata for the given account number and external identifier.
```graphql
supplyPointMetaData(accountNumber: String!, externalIdentifier: String!): SupplyPointMetaData
```
**Return type fields:**
- `label`: String — A user defined label for the supply point.

### `version`
```graphql
version: String
```

## Heat Pump Mutations

### `acceptHeatPumpQuote`
> Accept Heat Pump quote.

The possible errors that can be raised are:
- OE-0101: 'Authorization' header not provided.
- OE-0102: 'Authorization' header is invalid.
- OE-0104: 'Authorization' header has expired.
- OE-0301: Heat pump quote not found.
- OE-0302: Viewer not authorized to accept quote.
- OE-0306: Heat pump quote status invalid.

```graphql
acceptHeatPumpQuote(input: AcceptHeatPumpQuoteInput!): AcceptHeatPumpQuote
```
**`input` (AcceptHeatPumpQuoteInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `accountNumber`: NonEmptyString! — Account number of the account accepting the quote.
- `customer`: CustomerInput! — The customer accepting the quote.
**Return type fields:**
- `accepted`: Boolean! — Whether the quote was successfully accepted.

### `confirmHeatPumpQuoteDepositPayment`
> Confirm a payment against a Heat Pump quote

The possible errors that can be raised are:
- OE-0301: Heat pump quote not found.
- OE-0306: Heat pump quote status invalid.

```graphql
confirmHeatPumpQuoteDepositPayment(input: ConfirmHeatPumpQuoteDepositPaymentInput!): ConfirmHeatPumpQuoteDepositPayment
```
**`input` (ConfirmHeatPumpQuoteDepositPaymentInput) fields:**
- `code`: UUID! — Heat pump quote code.
**Return type fields:**
- `confirmed`: Boolean! — Whether the quote deposit payment was successfully confirmed.
- `surveyAppointment`: FieldAppointment
  - `bookingToken`: UUID! — The appointment booking token.
  - `appointmentType`: WorkOrderType! — The type of appointment.
  - `bookedAt`: DateTime — The date abd time the appointment was booked at.
  - `startsAt`: Date — The date and time the appointment starts at.
  - `endsAt`: Date — The date and time the appointment ends at.
  - `recommendedSchedulingDate`: Date — The recommended date for booking appointment.

### `createHeatPumpQuote`
> Create a Heat Pump quote.

The possible errors that can be raised are:
- OE-0307: Unable to create heat pump quote.

```graphql
createHeatPumpQuote(input: CreateHeatPumpQuoteInput!): CreateHeatPumpQuote
```
**`input` (CreateHeatPumpQuoteInput) fields:**
- `address`: HeatPumpAddressInput! — The address to be quoted.
- `utmParams`: UTMParams
- `clientParams`: JSONString — A JSON object containing any extra params to store on the quote.
**Return type fields:**
- `quote`: HeatPumpQuote
  - `code`: UUID! — The quote code.
  - `address`: Address! — The address the quote is for.
  - `eligibility`: QuoteEligibility — Whether this quote is eligible to proceed.
  - `status`: HeatPumpQuoteStatus! — The status of the quote.
  - `quotedProducts`: [QuotedProduct] — The quoted products the customer can choose from.
  - `clientParams`: JSONString — A JSON object containing any client params stored on the quote.

### `emailHeatPumpQuote`
> Email a Heat Pump quote.

The possible errors that can be raised are:
- OE-0301: Heat pump quote not found.
- OE-0303: Heat pump quote ineligible.
- OE-0305: Heat pump quote expired.

```graphql
emailHeatPumpQuote(input: EmailHeatPumpQuoteInput!): EmailHeatPumpQuote
```
**`input` (EmailHeatPumpQuoteInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `email`: EmailAddress! — The email address to send the quote to.
- `accountNumber`: String — The account number of the logged in user.
- `phone`: String — Phone number.
**Return type fields:**
- `sent`: Boolean! — Whether the email was sent.

### `heatPumpBulkUpdateSensorDisplayName`
> Update the display names for a set of sensors in bulk.

The possible errors that can be raised are:
- OE-0315: Invalid data submitted for heat pump operation.

```graphql
heatPumpBulkUpdateSensorDisplayName(accountNumber: String!, euid: ID!, updates: [SensorDisplayNameUpdate]!): BulkUpdateSensorDisplayName
```
**`updates` (SensorDisplayNameUpdate) fields:**
- `sensorCode`: String! — The code of the sensor you want to update.
- `newDisplayName`: String! — The new display name to set for this sensor.
**Return type fields:**
- `transactionIds`: [SensorUpdateTransactionId] — A mapping of transaction ids for each sensor.
  - `sensorCode`: String! — The code of the sensor which was updated.
  - `transactionId`: String! — A UUID representing the update operation for this sensor.

### `heatPumpBulkUpdateZoneDisplayNames`
> Update the display names for a set of zones in bulk.

The possible errors that can be raised are:
- OE-0321: Error trying to update heat pump zone names.

```graphql
heatPumpBulkUpdateZoneDisplayNames(accountNumber: String!, euid: ID!, updates: [SetZoneDisplayNameParameters]!): BulkUpdateZoneDisplayNames
```
**`updates` (SetZoneDisplayNameParameters) fields:**
- `zoneCode`: Zone! — The code of the heat pump controller zone.
- `newDisplayName`: String! — The new display name of the zone.
**Return type fields:**
- `transactionIds`: [ZoneDisplayNameUpdates]! — The transaction ID for each zone based on the zone code.
  - `zoneCode`: Zone! — The code of the heat pump controller zone.
  - `transactionId`: String! — The transaction ID issued for the zone's name update.

### `heatPumpMarkAsDeprovisioned`
> Mark a heat pump as deprovisioned.

The possible errors that can be raised are:
- OE-0315: Invalid data submitted for heat pump operation.

```graphql
heatPumpMarkAsDeprovisioned(accountNumber: String!, euid: ID!): HeatPumpMarkAsDeprovisioned
```
**Return type fields:**
- `success`: Boolean!

### `heatPumpRequestProvisioningClaimCertificate`
> Request a certificate to provision a heating controller.

The possible errors that can be raised are:
- OE-0315: Invalid data submitted for heat pump operation.

```graphql
heatPumpRequestProvisioningClaimCertificate(accountNumber: String!, propertyId: ID, requestParameters: ProvisioningClaimRequestParameters!): RequestProvisioningClaimCertificate
```
**`requestParameters` (ProvisioningClaimRequestParameters) fields:**
- `euid`: String! — The EUID of the device we are trying to provision.
- `nonce`: String! — The SHA256 hash of the EUID and timestamp.
- `signature`: String! — The signature of the nonce generated by the device's private key.
- `timestamp`: String! — A string representing the integer number of milliseconds since the epoch.
**Return type fields:**
- `provisioningClaimBundle`: ProvisioningClaimBundle — The certificate and private key required to create a provisioning claim.
  - `certificate`: String — The certificate which the controller will present to AWS IoT in order to validate its provisioning claim. It will contain the public key of the controller, and be signed by Kraken Flex.
  - `encryptedPrivateKey`: String — The private key which will be used by the controller to make the provisioning claim, encrypted with the public key of the controller.
  - `awsIotHostname`: String — The AWS IoT endpoint hostname to connect to AWS APIs & services.

### `heatPumpSetHushMode`
> Set the heat pump hush mode (quieter mode).
```graphql
heatPumpSetHushMode(accountNumber: String!, euid: ID!, hushModeEnabled: Boolean!): SetHushMode
```
**Return type fields:**
- `transactionId`: ID — Unique ID associated with this operation.

### `heatPumpSetZoneMode`
> Turn a specific heating controller zone ON/OFF, set it to AUTO mode or give it a BOOST.

The possible errors that can be raised are:
- OE-0315: Invalid data submitted for heat pump operation.

```graphql
heatPumpSetZoneMode(accountNumber: String!, euid: ID!, operationParameters: SetZoneModeParameters!): SetZoneMode
```
**`operationParameters` (SetZoneModeParameters) fields:**
- `zone`: Zone! — The heat pump controller zone, i.e. WATER, ZONE_1, ZONE_2 or AUXILIARY.
- `mode`: Mode! — The zone mode, i.e. ON, OFF, AUTO or BOOST.
- `setpointInCelsius`: Float — Target temperature for a zone in celsius.
- `scheduleOverrideAction`: ScheduleOverrideAction — Allows overriding just the currently active schedule action with a different one,whilst remaining in AUTO mode.
- `endAt`: DateTime — Time at which boost should end.
**Return type fields:**
- `transactionId`: ID — Unique ID associated with a zone's mode operation.

### `heatPumpSetZonePrimarySensor`
> Set the primary sensor for a heat pump zone.
```graphql
heatPumpSetZonePrimarySensor(accountNumber: String!, euid: ID!, operationParameters: SetZonePrimarySensorParameters!): SetZonePrimarySensor
```
**`operationParameters` (SetZonePrimarySensorParameters) fields:**
- `zone`: Zone! — The heat pump controller zone.
- `sensorCode`: String! — The code for the sensor we want to make primary, such as 'SENSOR01'.
**Return type fields:**
- `transactionId`: ID — The unique ID associated with this operation.

### `heatPumpSetZoneSchedules`
> Add schedules for a heating controller zone.

The possible errors that can be raised are:
- OE-0315: Invalid data submitted for heat pump operation.

```graphql
heatPumpSetZoneSchedules(accountNumber: String!, euid: ID!, zoneScheduleParameters: SetZoneSchedulesParameters!): SetZoneSchedules
```
**`zoneScheduleParameters` (SetZoneSchedulesParameters) fields:**
- `zone`: Zone! — The heat pump controller zone, i.e. WATER, ZONE_1, ZONE_2 or AUXILIARY.
- `schedules`: [ZoneSchedule]! — The schedule(s) to be set for a specific zone.
**Return type fields:**
- `transactionId`: String

### `heatPumpSetupSmartControl`
> Set up smart control for a heat pump.

The possible errors that can be raised are:
- OE-0315: Invalid data submitted for heat pump operation.
- OE-0317: That heat pump model is currently unsupported.

```graphql
heatPumpSetupSmartControl(input: HeatPumpSetupSmartControlInput!): HeatPumpSetupSmartControl
```
**`input` (HeatPumpSetupSmartControlInput) fields:**
- `accountNumber`: String! — The account number.
- `propertyId`: ID! — The property id of the location.
- `euid`: ID! — The EUID of the heat pump controller.
**Return type fields:**
- `success`: Boolean! — Whether the setup was successful.

### `heatPumpUpdateFlowTemperatureConfiguration`
> Update the flow temperature configuration for the heat pump.

The possible errors that can be raised are:
- OE-0315: Invalid data submitted for heat pump operation.

```graphql
heatPumpUpdateFlowTemperatureConfiguration(accountNumber: String!, euid: ID!, flowTemperatureInput: FlowTemperatureInput!): UpdateFlowTemperatureConfiguration
```
**`flowTemperatureInput` (FlowTemperatureInput) fields:**
- `useWeatherCompensation`: Boolean! — Whether weather compensation should be enabled or not.
- `weatherCompensationValues`: TemperatureRangeInput — The min and max temperatures for when weather compensation is enabled.
- `flowTemperature`: TemperatureInput — Flow temperature if weather compensation is off.
**Return type fields:**
- `transactionIds`: [AttributeUpdateTransactionId]! — A mapping of transaction ids for each attribute update.
  - `attributeName`: String! — The name of the attribute that was updated.
  - `transactionId`: String! — A UUID representing the update operation for this attribute.

### `initiateHeatPumpDepositPayment`
> Make a payment against a Heat Pump quote.

The possible errors that can be raised are:
- OE-0101: 'Authorization' header not provided.
- OE-0103: Unauthorized.
- OE-0104: 'Authorization' header has expired.
- OE-0301: Heat pump quote not found.
- OE-0304: Heat pump quote has no price.
- OE-0305: Heat pump quote expired.
- OE-0303: Heat pump quote ineligible.
- OE-0306: Heat pump quote status invalid.

```graphql
initiateHeatPumpDepositPayment(input: InitiateHeatPumpDepositPaymentInput!): InitiateHeatPumpDepositPayment
```
**`input` (InitiateHeatPumpDepositPaymentInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `accountNumber`: NonEmptyString! — Account number of the account for which this payment is being made.
**Return type fields:**
- `payment`: InitiateStandalonePaymentOutput
  - `retrievalToken`: String! — The retrieval token for this standalone payment.
  - `secretToken`: String! — The secret used to collect the payment.

### `requestHeatPumpDepositFollowUp`
> Request heat pump deposit follow up

The possible errors that can be raised are:
- OE-0301: Heat pump quote not found.
- OE-0303: Heat pump quote ineligible.
- OE-0306: Heat pump quote status invalid.
- OE-GBR-0901: Unable to request deposit follow up for quote.

```graphql
requestHeatPumpDepositFollowUp(input: RequestHeatPumpDepositFollowUpInput!): RequestHeatPumpDepositFollowUp
```
**`input` (RequestHeatPumpDepositFollowUpInput) fields:**
- `code`: UUID! — Heat pump quote code.
**Return type fields:**
- `requested`: Boolean! — Whether the follow up was successfully requested.

### `requestHeatPumpQuoteCallback`
> Request a callback from a human for a Heat Pump quote.

The possible errors that can be raised are:
- OE-0301: Heat pump quote not found.
- OE-0303: Heat pump quote ineligible.
- OE-0306: Heat pump quote status invalid.

```graphql
requestHeatPumpQuoteCallback(input: RequestHeatPumpQuoteCallbackInput!): RequestHeatPumpQuoteCallback
```
**`input` (RequestHeatPumpQuoteCallbackInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `customer`: CustomerInput! — The customer requesting the callback.
- `property`: PropertyInput — The property details provided by the customer.
- `accountNumber`: String — The account number of the logged in user.
**Return type fields:**
- `requested`: Boolean! — Whether the callback was successfully requested.
- `eligibility`: QuoteEligibility! — Whether the customer is eligible for a callback.
  - `isEligible`: Boolean! — Whether the quote is eligible or not.
  - `ineligibilityReason`: HeatPumpQuoteIneligibilityReason — The reason this quote is ineligible, if ineligible.

### `requestIneligibleHeatPumpQuoteFollowUp`
> Register interest in a follow-up for an ineligible quote if it becomes eligible in future.

The possible errors that can be raised are:
- OE-0301: Heat pump quote not found.
- OE-0305: Heat pump quote expired.

```graphql
requestIneligibleHeatPumpQuoteFollowUp(input: RequestIneligibleHeatPumpQuoteFollowUpInput!): RequestIneligibleHeatPumpQuoteFollowUp
```
**`input` (RequestIneligibleHeatPumpQuoteFollowUpInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `email`: EmailAddress! — The email address to follow up with.
- `accountNumber`: String — The account number of the logged in user.
- `eligibleDate`: Date — The date in the future at which the customer would be eligible.
- `clientParams`: JSONString — A JSON object containing any extra params to store on the quote.
**Return type fields:**
- `requested`: Boolean! — Whether the follow-up was successfully requested.

### `selectHeatPumpQuoteProduct`
> Select a Heat Pump quoted product.

The possible errors that can be raised are:
- OE-0101: 'Authorization' header not provided.
- OE-0102: 'Authorization' header is invalid.
- OE-0103: Unauthorized.
- OE-0104: 'Authorization' header has expired.
- OE-0301: Heat pump quote not found.
- OE-0306: Heat pump quote status invalid.
- OE-0308: Heat pump quote product not found.
- OE-0309: Heat pump quote product already selected.

```graphql
selectHeatPumpQuoteProduct(input: SelectHeatPumpQuoteProductInput!): SelectHeatPumpQuoteProduct
```
**`input` (SelectHeatPumpQuoteProductInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `productCode`: NonEmptyString! — The product code of the quoted product to select.
**Return type fields:**
- `selected`: Boolean! — Whether the quoted product was successfully selected.

### `setHeatPumpQuoteJourneyVersion`
> Set heat pump quote journey version

The possible errors that can be raised are:
- OE-0301: Heat pump quote not found.
- OE-GBR-0902: Heat pump quote journey version already exists.
- OE-GBR-0903: Heat pump quote journey version is invalid.
- OE-0306: Heat pump quote status invalid.
- OE-GBR-0904: Heat pump quote journey version is ineligible.

```graphql
setHeatPumpQuoteJourneyVersion(input: SetHeatPumpQuoteJourneyVersionInput!): SetHeatPumpQuoteJourneyVersion
```
**`input` (SetHeatPumpQuoteJourneyVersionInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `version`: String! — The journey version to set for the heat pump quote.
**Return type fields:**
- `versionSet`: Boolean! — Whether the journey version was successfully set.

### `updateHeatPumpQuote`
> Update Heat Pump quote details.

The possible errors that can be raised are:
- OE-0301: Heat pump quote not found.
- OE-0303: Heat pump quote ineligible.
- OE-0306: Heat pump quote status invalid.

```graphql
updateHeatPumpQuote(input: UpdateHeatPumpQuoteInput!): UpdateHeatPumpQuote
```
**`input` (UpdateHeatPumpQuoteInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `property`: PropertyInput! — The property details provided by the customer.
- `installationOptions`: PropertyInstallationOptionsInput — Details about the property install options provided by the customer.
- `financingOptions`: FinancingOptionsInput — Details about the financing options selected by the customer.
- `clientParams`: JSONString — A JSON object containing any extra params to store on the quote.
**Return type fields:**
- `quote`: HeatPumpQuote
  - `code`: UUID! — The quote code.
  - `address`: Address! — The address the quote is for.
  - `eligibility`: QuoteEligibility — Whether this quote is eligible to proceed.
  - `status`: HeatPumpQuoteStatus! — The status of the quote.
  - `quotedProducts`: [QuotedProduct] — The quoted products the customer can choose from.
  - `clientParams`: JSONString — A JSON object containing any client params stored on the quote.

### `updateHeatPumpQuoteFinanceOfferTerms`
> Update a Heat Pump quote finance offer terms.

The possible errors that can be raised are:
- OE-0001: Feature unavailable.
- OE-0301: Heat pump quote not found.
- OE-0305: Heat pump quote expired.
- OE-0306: Heat pump quote status invalid.
- OE-0310: Heat pump finance offer term invalid.
- OE-0311: Heat pump finance offer loan amount below minimum allowed.
- OE-0312: Heat pump finance offer deposit percentage invalid.
- OE-0313: Heat pump finance offer loan amount above maximum allowed.

```graphql
updateHeatPumpQuoteFinanceOfferTerms(input: UpdateHeatPumpQuoteFinanceOfferTermsInput!): UpdateHeatPumpQuoteFinanceOfferTerms
```
**`input` (UpdateHeatPumpQuoteFinanceOfferTermsInput) fields:**
- `code`: UUID! — Heat pump quote code.
- `term`: PositiveInt! — Selected finance loan term in months.
- `depositPercentage`: Percentage! — Selected quote deposit percentage.
**Return type fields:**
- `offer`: HeatPumpQuoteFinanceOffer — The selected finance offer terms.
  - `term`: PositiveInt — The term selected by the customer in months.
  - `depositPercentage`: Percentage — The deposit percentage selected by the customer (e.g., 5%, 10%..).
  - `price`: PositiveInt — The price of the cheapest quoted product in the currency's minor unit.
  - `apr`: Decimal — The interest rate for the finance offer loan (e.g., 5.50 for 5.5%).
  - `availableTerms`: [Int] — List of available terms in months.

## Authentication Mutations

### `deauthenticateChargePoint`
> Deauthenticate an electric charger.

The possible errors that can be raised are:
- OE-0001: Feature unavailable.
- OE-0808: Charger is not already onboarded.

```graphql
deauthenticateChargePoint(input: DeauthenticateChargePointInput!): DeauthenticateChargePoint
```
**`input` (DeauthenticateChargePointInput) fields:**
- `accountNumber`: String!
- `deviceUUID`: UUID!
**Return type fields:**
- `deauthenticatedChargePoint`: DeauthenticatedChargePoint
  - `deviceUUID`: UUID!
  - `onboardedAt`: DateTime!
  - `deauthenticatedAt`: DateTime!

## Key Enums

### `PerformanceGrouping`
> The time interval that performance is reported for.
- `LIVE`
- `DAY`
- `WEEK`
- `MONTH`
- `YEAR`

### `HeatPumpQuoteStatus`
- `ACCEPTED`
- `ISSUED`
- `PURCHASED`
- `SURVEYED`
- `REISSUED`

### `HeatType`
- `MAINS_GAS_BOILER`
- `LPG_BOILER`
- `ELECTRIC_BOILER`
- `ELECTRIC_RADIATOR`
- `OIL_BOILER`
- `ELECTRIC_STORAGE_HEATER`
- `OTHER`

### `HeatPumpPricingMethod`
- `ARBITRARY`
- `INDICATIVE`
- `PREDICTIVE`
- `SURVEY`

### `HeatPumpQuoteIneligibilityReason`
- `ADDRESS_OUT_OF_SERVICE_AREA`
- `PROPERTY_HEAT_LOSS_TOO_HIGH`
- `PROPERTY_UNDERGOING_RENOVATION`
- `PROPERTY_TYPE_NOT_SUPPORTED`
- `NO_SPACE_FOR_HEAT_PUMP`
- `NO_SPACE_FOR_CYLINDER`
- `HEAT_SOURCE_UNKNOWN`
- `HEAT_SOURCE_NOT_SUPPORTED`

### `HeatPumpRunningCostTimePeriod`
- `YEAR`

## All Available Queries (Index)

| Query | Description |
|-------|-------------|
| `account` |  |
| `addressUprns` | Search for address UPRNs matching the provided postcode. |
| `availableFieldAppointmentSlots` | The available Kraken Field appointment slots for a given search ID. If Kraken Field has not returned results yet, this e |
| `backendScreen` | Get mobile screen details to render. |
| `backendScreenEventIds` | Get all registered backend screen event IDs. |
| `backendScreenIds` | Get all registered backend screen IDs. |
| `chargePointFirmwareVersionState` | The firmware version state of the charge point. |
| `chargePointSchedules` | The charge-point-schedules associated with the device |
| `chargePointsAtLocation` | The charge points related to an account in a specific location. |
| `chargingSessions` | The history of charging sessions associated with the charger. |
| `chargingStreaks` | Charging streak data for the requested account. |
| `charityDonationAmountChoices` | Show the account's charity donation amount choices. |
| `charityDonationHistory` | Show the account's charity donation records, and the total amount donated. |
| `charityDonationSettings` | Show the account's charity donation settings. |
| `cosyCharacterisation` | Average coefficient of performance metrics (COPs) for the entire cosy fleet against external temperature. |
| `cosyDistribution` | The distribution of seasonal performance factors (SPF) across the Cosy fleet. |
| `cosyEfficiency` | The average coefficient of performance (COPs) for the entire cosy fleet historically. |
| `cosyRegional` | The median seasonal performance factor (SPF) per region across the Cosy fleet. |
| `cosySavings` | The average annual savings for Cosy tariff customers. |
| `customerPayouts` | Retrieve all customer payouts for a given account number. |
| `customerPayoutsSummary` | Get aggregate summary of customer payouts (total balance and count). |
| `dashboardScreen` | Get a dashboard screen to render. |
| `evChargerPartnershipOffer` |  |
| `fieldAppointment` | Get the field appointment for a booking token. |
| `flexibilitySchemeSessions` | Get sessions for a Flexibility Scheme that the member is enrolled in. |
| `freeElectricityAccountEvents` | List of Free Electricity events that an account participated in. |
| `freeElectricityAccountSummary` | Summary of an account's participation in Free Electricity across all events. |
| `freeElectricityCollectiveStats` | Collective statistics for the Free Electricity campaign. |
| `getAddressesForPostcode` | Get a list of addresses for a given postcode. |
| `greenerNightsAccountEvChargeHistory` | Get account EV charge history during Greener Nights. |
| `greenerNightsAccountStats` |  |
| `greenerNightsForecast` | Get upcoming week Greener Nights forecast |
| `heatPumpControllerConfiguration` | The configuration of the heat pump controller and its zones. |
| `heatPumpControllerStatus` | Retrieve the heat pump controller device status. |
| `heatPumpControllersAtLocation` | The heat pump controllers attached to an account at a given location. |
| `heatPumpLifetimePerformance` | Heat pump lifetime performance data. |
| `heatPumpQuote` | The heat pump quote which was given to the customer. |
| `heatPumpQuoteFinanceOffer` | The heat pump finance offer which was selected by the customer for the given quote. |
| `heatPumpTimeRangedPerformance` | Heat pump performance data between two specific dates. |
| `heatPumpTimeSeriesPerformance` | Heat pump performance time series data. |
| `inkConversation` | Retrieve an Ink live chat conversation by account number and optional conversation relay ID. |
| `isEligibleForScheme` | Check if a member candidate is eligible for the scheme at the time of the request. |
| `meterPoint` | The meter point. |
| `octoChargerDeviceCredentials` | The device-specific credentials registered with the Kraken platform. This field does not include OCCP server related cre |
| `octoplusActiveCharityPartnership` | The currently active charity partnership. Returns null if there are currently no active partnerships. |
| `octoplusCharityDonations` | The donations a user has previously made to charity partnerships |
| `octoplusOffer` | Octoplus Offer available for a given account_user and account combination. |
| `octoplusOfferGroups` | Octoplus Offer Groups available for a given account_user and account combination. |
| `octoplusRewards` | Octoplus Rewards for a given account user. |
| `productEnrollmentCandidatesEligibility` | Query product eligibility against all potentially eligible enrollment candidates for the given property and account. |
| `propertyAerialData` | Aerial data for a property by its UPRN and postcode. |
| `quoteRegionalPricing` | The regional pricing adjustment for the quote. |
| `recommendedSystems` | Get recommended solar panel systems for a property. |
| `renewableEnergyCommunity` | Renewable Energy Community stats, details and consumption data for an account (if they're a REC member). |
| `retailFinanceInstalmentsTerms` | Get retail finance instalments for certain purchase amount. |
| `retailFinanceRepresentativeExample` | Generate an breakdown example of an instalment plan |
| `savingSessions` | Saving sessions queries for an account. |
| `scheme` | Get the details of a basic Scheme. |
| `schemeMembership` | Get the scheme membership details for a member. |
| `sherloctopusDeviceMeasuredConsumption` | Fetch actual meaaured consumption data for device (for example a washing machine). |
| `sherloctopusDevicePredictedConsumptionCycle` | Fetch data for a device's predicted consumption cycle (for example a washing machine), i.e. a device which is drawing po |
| `shoptopusVoucherAvailability` | Describes availability of Shoptopus Vouchers for given Octopoint amounts. |
| `signupEligibility` | Checks whether the specified potential customer should be allowed to sign up with us. |
| `smartProductLineOptions` | Returns all available smart product line options with their latest Kraken product codes. |
| `solarGenerationEstimate` | Get an estimate of the number of kWh an average domestic solar installation would produce for a given postcode. |
| `supplyPointMetaData` | Retrieve the supply point metadata for the given account number and external identifier. |
| `vehicleEligibilityForIoGo` | Query vehicle eligibility for IO Go smart product by providing registration number. |
| `version` |  |
| `wheelOfFortunePrizeType` |  |
| `wheelOfFortunePrizesWon` | Total number of prizes won so far in the current period |
| `wheelOfFortuneSegments` |  |
| `wheelOfFortuneSpinHistory` |  |
| `wheelOfFortuneSpinsAllowed` |  |

## All Available Mutations (Index)

| Mutation | Description |
|---------|-------------|
| `acceptHeatPumpQuote` | Accept Heat Pump quote.  The possible errors that can be raised are: - OE-0101: 'Authorization' header not provided. - O |
| `backendScreenEvent` | Look up an event to perform and return the next action.  The possible errors that can be raised are: - OE-1201: Action n |
| `bookFieldAppointment` | Book an Appointment in Kraken Field.  The possible errors that can be raised are: - OE-0401: This field appointment has  |
| `calculateSolarEstimate` | Calculate an estimate for the number and price of solar panels required for the provided inputs.  The possible errors th |
| `cancelPayment` | Cancel an account's payment.  The possible errors that can be raised are: - OE-0901: The account cannot cancel payments. |
| `chargePointSetAwayModeState` | Set the away mode state for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not alread |
| `chargePointSetBoostChargingState` | Set the boost charging state for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not a |
| `chargePointSetChargeCableAutoLockState` | Set the charge cable auto lock state for a charge point  The possible errors that can be raised are: - OE-0808: Charger  |
| `chargePointSetChargingMethod` | Set the charging method for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not alread |
| `chargePointSetControlMode` | Set a control mode for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not already onb |
| `chargePointSetECOModeState` | Set the ECO mode state for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not already |
| `chargePointSetLEDBrightness` | Set the LED brightness percentage for a charge point  The possible errors that can be raised are: - OE-0808: Charger is  |
| `chargePointSetRandomDelay` | Set a random delay for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not already onb |
| `chargePointSetSchedule` | Set a schedule for a charge point  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE-0808 |
| `chargePointStartBoostCharge` | Start boost charging for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not already o |
| `chargePointStopBoostCharge` | Stop boost charging for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not already on |
| `chargePointUnlockChargeCable` | Unlock the charge cable for a charge point  The possible errors that can be raised are: - OE-0808: Charger is not alread |
| `chargePointUpgradeFirmware` | Upgrade the charge point firmware for a charge point  The possible errors that can be raised are: - OE-0808: Charger is  |
| `claimOctoplusReward` | Claim an Octoplus reward.  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE-0101: 'Autho |
| `claimShoptopusReward` | Claim a Shoptopus Reward using Octopoints.  The possible errors that can be raised are: - OE-0103: Unauthorized. - OE-00 |
| `completeEvChargerPurchase` | Perform final actions after a successful payment of an EV Charger purchase.  The possible errors that can be raised are: |
| `confirmHeatPumpQuoteDepositPayment` | Confirm a payment against a Heat Pump quote  The possible errors that can be raised are: - OE-0301: Heat pump quote not  |
| `createFieldAppointmentSearch` | Start a search request for appointments.  The possible errors that can be raised are: - OE-0402: Invalid booking token.  |
| `createHeatPumpQuote` | Create a Heat Pump quote.  The possible errors that can be raised are: - OE-0307: Unable to create heat pump quote.  |
| `createInkLiveChatMessage` | Create a new Ink live chat message  The possible errors that can be raised are: - OE-0000: Internal server error. - OE-0 |
| `createMoveInQuote` | Create a quote for a move into an occupied property.  The possible errors that can be raised are: - OE-GBR-0201: Invalid |
| `createOctoplusRewardContactUsLink` | Get or create a contact us link.  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE-0101: |
| `deauthenticateChargePoint` | Deauthenticate an electric charger.  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE-08 |
| `emailHeatPumpQuote` | Email a Heat Pump quote.  The possible errors that can be raised are: - OE-0301: Heat pump quote not found. - OE-0303: H |
| `enrollInFlexibilityScheme` | Enroll a member in a Flexibility Scheme  The possible errors that can be raised are: - OE-0001: Feature unavailable. - O |
| `enrollSchemeMember` | Enroll a member in a scheme  The possible errors that can be raised are: - OE-1101: Unsupported scheme. - OE-1102: Membe |
| `generateInkPresignedUrl` | Generate a presigned URL for Ink  The possible errors that can be raised are: - OE-0103: Unauthorized. - OE-0001: Featur |
| `greenerNightsOptOut` | Opt out an account from Greener Nights. |
| `greenerNightsSignUp` | Sign up an account for Greener Nights.  The possible errors that can be raised are: - OE-GBR-0405: Unable to sign up acc |
| `heatPumpBulkUpdateSensorDisplayName` | Update the display names for a set of sensors in bulk.  The possible errors that can be raised are: - OE-0315: Invalid d |
| `heatPumpBulkUpdateZoneDisplayNames` | Update the display names for a set of zones in bulk.  The possible errors that can be raised are: - OE-0321: Error tryin |
| `heatPumpMarkAsDeprovisioned` | Mark a heat pump as deprovisioned.  The possible errors that can be raised are: - OE-0315: Invalid data submitted for he |
| `heatPumpRequestProvisioningClaimCertificate` | Request a certificate to provision a heating controller.  The possible errors that can be raised are: - OE-0315: Invalid |
| `heatPumpSetHushMode` | Set the heat pump hush mode (quieter mode). |
| `heatPumpSetZoneMode` | Turn a specific heating controller zone ON/OFF, set it to AUTO mode or give it a BOOST.  The possible errors that can be |
| `heatPumpSetZonePrimarySensor` | Set the primary sensor for a heat pump zone. |
| `heatPumpSetZoneSchedules` | Add schedules for a heating controller zone.  The possible errors that can be raised are: - OE-0315: Invalid data submit |
| `heatPumpSetupSmartControl` | Set up smart control for a heat pump.  The possible errors that can be raised are: - OE-0315: Invalid data submitted for |
| `heatPumpUpdateFlowTemperatureConfiguration` | Update the flow temperature configuration for the heat pump.  The possible errors that can be raised are: - OE-0315: Inv |
| `initiateHeatPumpDepositPayment` | Make a payment against a Heat Pump quote.  The possible errors that can be raised are: - OE-0101: 'Authorization' header |
| `joinSavingSessionsCampaign` | Sign account up to Saving Sessions campaign.  The possible errors that can be raised are: - OE-1302: Meter point not fou |
| `joinSavingSessionsEvent` | Opt account in to a Saving Sessions event.  The possible errors that can be raised are: - OE-1305: Saving Sessions event |
| `linkNissanProductEnrollmentToSite` | Link Nissan product enrollment to the specific site.  The possible errors that can be raised are: - OE-GBR-0707: Product |
| `makeDonationToOctoplusPartnership` | Make a donation to an active partnership.  The possible errors that can be raised are: - OE-0001: Feature unavailable. - |
| `moveIn` | Move a customer in to the occupier property associated to the specified electricity meter point.  The possible errors th |
| `onboardChargePoint` | Onboard charger OCPP credentials.  The possible errors that can be raised are: - OE-0801: Invalid device UUID. - OE-0802 |
| `optInToCharityDonation` | Opt in to charity donations.  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE-0101: 'Au |
| `optInToFlexibilitySchemeSession` | Opt a member into a Flexibility Scheme session  The possible errors that can be raised are: - OE-0001: Feature unavailab |
| `optOutOfCharityDonation` | Opt out of charity donations.  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE-0101: 'A |
| `requestHeatPumpDepositFollowUp` | Request heat pump deposit follow up  The possible errors that can be raised are: - OE-0301: Heat pump quote not found. - |
| `requestHeatPumpQuoteCallback` | Request a callback from a human for a Heat Pump quote.  The possible errors that can be raised are: - OE-0301: Heat pump |
| `requestIneligibleHeatPumpQuoteFollowUp` | Register interest in a follow-up for an ineligible quote if it becomes eligible in future.  The possible errors that can |
| `selectHeatPumpQuoteProduct` | Select a Heat Pump quoted product.  The possible errors that can be raised are: - OE-0101: 'Authorization' header not pr |
| `sendSolarEstimateEmail` | Send a solar panel estimate to an email address. |
| `setHeatPumpQuoteJourneyVersion` | Set heat pump quote journey version  The possible errors that can be raised are: - OE-0301: Heat pump quote not found. - |
| `setUserConfirmationForPrediction` | Provide feedback on a prediction whether it was accurate or not.  The possible errors that can be raised are: - OE-0101: |
| `spinWheelOfFortune` | Spin the wheel of fortune  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE-0101: 'Autho |
| `submitSolarEnquiry` | Submit a solar enquiry.  The possible errors that can be raised are: - OE-GBR-0601: Invalid product code.  |
| `submitSolarInterest` | Submit interest for solar panel installation.  The possible errors that can be raised are: - OE-0202: Submit solar inter |
| `unenrollFromFlexibilityScheme` | Unenroll a member from a Flexibility Scheme  The possible errors that can be raised are: - OE-0001: Feature unavailable. |
| `unenrollSchemeMember` | Unenroll a member from a scheme  The possible errors that can be raised are: - OE-1101: Unsupported scheme.  |
| `updateCharityDonationAmount` | Update the amount of charity donation.  The possible errors that can be raised are: - OE-0001: Feature unavailable. - OE |
| `updateHeatPumpQuote` | Update Heat Pump quote details.  The possible errors that can be raised are: - OE-0301: Heat pump quote not found. - OE- |
| `updateHeatPumpQuoteFinanceOfferTerms` | Update a Heat Pump quote finance offer terms.  The possible errors that can be raised are: - OE-0001: Feature unavailabl |
| `updateOctoChargerOCPPCredentials` | Update octo charger OCPP credentials.  The possible errors that can be raised are: - OE-0801: Invalid device UUID. - OE- |

