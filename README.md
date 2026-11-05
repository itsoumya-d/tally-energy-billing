# tally-energy-billing

A .NET 10 energy retail billing engine: interval usage, time-of-use tariffs,
daily supply, demand, solar export credit, GST, proration and invoice line
items.

## Why this exists

Energy billing is not a single multiplication. A retail bill has to answer:
which time-of-use band does each interval fall into, what is the daily supply
charge, what demand did the site hit, how much solar was exported, and what does
GST do to the subtotal? This service makes every part explicit and testable.

## Formulas

```text
usage          = sum(interval kWh * tariff rate for the interval's band)
supply         = daily_supply_cents * active_days
demand         = max_demand_kW * demand_cents_per_kW
export_credit  = export_kWh * export_cents_per_kWh
subtotal       = usage + supply + demand - export_credit
GST            = round(subtotal * gst_bps / 10000)
total          = subtotal + GST
```

Default tariff: Peak 32c/kWh (weekdays 07:00-11:00 and 17:00-21:00), Shoulder
22c/kWh (weekdays 11:00-17:00 and 21:00-23:00), Off-peak 12c/kWh (23:00-07:00
and weekends), daily supply $1.80, demand $12.50/kW, export 8c/kWh, GST 15%.
All banding uses Pacific/Auckland local time.

A bill is rejected when the period is reversed, active days are invalid, demand
is negative, interval usage/export is negative, intervals fall outside the
period, or an interval ends before it starts.

## API

| Method | Route | Purpose |
|---|---|---|
| GET | `/health` | Liveness probe |
| GET | `/api/tariff` | Default tariff |
| POST | `/api/bills` | Calculate a bill from period + intervals |
| GET | `/api/sample-bill` | Seeded retail bill used by the dashboard |
| GET | `/` | Energy Bill Review dashboard |

```bash
curl -s -X POST localhost:8094/api/bills \
  -H 'Content-Type: application/json' \
  -d '{"periodStart":"2026-11-01","periodEnd":"2026-11-30","activeDays":30,"maxDemandKw":6.5,"intervals":[{"start":"2026-11-02T08:00:00+13:00","end":"2026-11-02T08:30:00+13:00","kwh":2,"exportKwh":0}]}'
```

## Run

```bash
dotnet run --project src/TallyEnergyBilling.Api
```

## Test

```bash
dotnet test TallyEnergyBilling.slnx
```

22 xUnit tests cover peak/shoulder/off-peak banding, weekend behaviour, band
boundaries, usage/supply/demand/export/GST, proration, empty usage, line items,
reversed periods, invalid active days, negative values and out-of-period
intervals.

## Design tokens

From Tally Group's public site:

- Heading/body: Nunito Sans
- Background: `#FFFFFF`
- Surface: `#F5F5F5`
- Text: `#2C365D`
- Accent: `#4EEECA`
- Radius: `1px`
- Spacing unit: `8px`

## Docker

```bash
docker build -t tally-energy-billing .
docker run --rm -p 8094:8094 tally-energy-billing
```

## Honest scope

This is a tested reference engine, not a production billing run. It does not
integrate with a meter data provider, market settlement, SQL Server production
tables or a full tariff library. The banding, supply/demand/export/GST,
proration, validation and invoice-line logic are real and tested.

## License

MIT
