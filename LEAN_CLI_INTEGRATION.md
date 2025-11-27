# Lean CLI Integration Guide for Fyers Brokerage

This document describes the steps required to integrate Fyers Brokerage into the LEAN CLI so that `lean live` command shows Fyers as an available option.

## Overview

The LEAN CLI loads brokerage configurations from a centralized JSON file hosted at:
`https://cdn.quantconnect.com/cli/modules-{version}.json`

To add Fyers support, we need to:
1. Submit a PR to the lean-cli repository with the Fyers module configuration
2. Request a product-id from QuantConnect for the Fyers module

## Required JSON Configuration

The following JSON module configuration should be added to the lean-cli `modules.json`:

```json
{
  "configurations": [
    {
      "help": "Your Fyers API App ID (format: APPID-100)",
      "id": "fyers-client-id",
      "input-method": "prompt",
      "log-message": "Your Fyers API App ID can be obtained from the Fyers API dashboard at https://myapi.fyers.in/dashboard",
      "prompt-info": "Client ID",
      "type": "input"
    },
    {
      "help": "Your Fyers access token obtained via OAuth authentication",
      "id": "fyers-access-token",
      "input-method": "prompt-password",
      "log-message": "Generate your access token using the OAuth flow described at https://myapi.fyers.in/docsv3",
      "prompt-info": "Access token",
      "type": "input"
    },
    {
      "help": "INTRADAY (MIS) for intraday trades, CNC for delivery trades, MARGIN for margin trades",
      "id": "fyers-product-type",
      "input-choices": ["INTRADAY", "CNC", "MARGIN"],
      "input-method": "choice",
      "log-message": "Product type determines how positions are carried: INTRADAY for same-day square-off, CNC for delivery, MARGIN for margin trading.",
      "prompt-info": "Product type",
      "type": "input"
    },
    {
      "help": "EQUITY for NSE/BSE stocks, COMMODITY for MCX, CURRENCY for currency derivatives",
      "id": "fyers-trading-segment",
      "input-choices": ["EQUITY", "COMMODITY", "CURRENCY"],
      "input-method": "choice",
      "log-message": "Trading segment determines which exchange and instrument types are available.",
      "prompt-info": "Trading segment",
      "type": "input"
    },
    {
      "filters": [
        {
          "condition": {
            "dependent-config-id": "type",
            "pattern": "data-queue-handler",
            "type": "exact-match"
          }
        }
      ],
      "id": "data-queue-handler",
      "type": "info",
      "value": "[\"FyersBrokerage\"]"
    },
    {
      "filters": [
        {
          "condition": {
            "dependent-config-id": "type",
            "pattern": "brokerage",
            "type": "exact-match"
          }
        }
      ],
      "id": "live-mode-brokerage",
      "type": "info",
      "value": "FyersBrokerage"
    },
    {
      "filters": [
        {
          "condition": {
            "dependent-config-id": "type",
            "pattern": "history-provider",
            "type": "exact-match"
          }
        }
      ],
      "id": "history-provider",
      "type": "info",
      "value": "[\"BrokerageHistoryProvider\"]"
    }
  ],
  "display-id": "Fyers",
  "id": "FyersBrokerage",
  "installs": true,
  "live-cash-balance-state": "not-supported",
  "live-holdings-state": "not-supported",
  "minimum-seat": "Researcher",
  "platform": ["cloud", "local", "cli"],
  "product-id": "TBD",
  "specifications": "https://www.quantconnect.com/docs/v2/cloud-platform/live-trading/brokerages/fyers",
  "type": ["brokerage", "data-queue-handler", "history-provider"]
}
```

## Configuration Field Descriptions

| Field | Description |
|-------|-------------|
| `id` | Unique identifier matching the C# class name: `FyersBrokerage` |
| `display-id` | User-friendly name shown in CLI: `Fyers` |
| `type` | Array of module types: brokerage, data-queue-handler, history-provider |
| `platform` | Supported platforms: cloud, local, cli |
| `product-id` | QuantConnect product ID (assigned by QC team) |
| `installs` | Whether module requires installation: `true` |
| `minimum-seat` | Minimum subscription level required |
| `live-cash-balance-state` | Initial cash balance support: `not-supported` |
| `live-holdings-state` | Initial holdings support: `not-supported` |
| `specifications` | Link to documentation |

## Lean Configuration Keys

The brokerage uses these configuration keys in `lean.json`:

| Key | Description | Example |
|-----|-------------|---------|
| `fyers-client-id` | API App ID from Fyers dashboard | `ABC123XY-100` |
| `fyers-access-token` | OAuth access token | `eyJhbGc...` |
| `fyers-product-type` | Trading product type | `INTRADAY`, `CNC`, `MARGIN` |
| `fyers-trading-segment` | Exchange segment | `EQUITY`, `COMMODITY`, `CURRENCY` |

## PR Submission Process

### Step 1: Fork lean-cli Repository
```bash
git clone https://github.com/QuantConnect/lean-cli.git
cd lean-cli
git checkout -b feature/fyers-brokerage
```

### Step 2: Request Product ID
Contact QuantConnect to request a product-id for the Fyers module. This is typically done through:
- GitHub issue on lean-cli repository
- QuantConnect forum
- Direct communication with QC team

### Step 3: Update modules.json
Once you have the product-id:
1. Locate the modules JSON file
2. Add the Fyers configuration to the `modules` array
3. Ensure proper JSON formatting

### Step 4: Submit PR
Create a pull request with:
- Title: "Add Fyers Brokerage Support"
- Description including:
  - Link to Lean.Brokerages.Fyers repository
  - Summary of supported features
  - Testing evidence

## Dependencies

The Fyers brokerage module depends on:
- Main brokerage repository: `Lean.Brokerages.Fyers`
- NuGet package: `QuantConnect.FyersBrokerage` (to be published)

## Usage After Integration

Once integrated, users can run:

```bash
# Interactive mode
lean live deploy "My Project"
# Then select "Fyers" from the brokerage list

# Non-interactive mode
lean live deploy "My Project" \
  --brokerage "Fyers" \
  --fyers-client-id "YOUR_APP_ID-100" \
  --fyers-access-token "YOUR_ACCESS_TOKEN" \
  --fyers-product-type "INTRADAY" \
  --fyers-trading-segment "EQUITY"
```

## Related Repositories

- Lean Engine: https://github.com/QuantConnect/Lean
- Lean CLI: https://github.com/QuantConnect/lean-cli
- Fyers Brokerage: https://github.com/QuantConnect/Lean.Brokerages.Fyers (after merge)

## References

- [Lean CLI Documentation](https://www.lean.io/docs/v2/lean-cli/live-trading)
- [Fyers API Documentation](https://myapi.fyers.in/docsv3)
- [QuantConnect Brokerage Development](https://www.lean.io/docs/v2/lean-engine/contributions/brokerages)
