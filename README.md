![header-cheetah](https://user-images.githubusercontent.com/79997186/184224088-de4f3003-0c22-4a17-8cc7-b341b8e5b55d.png)

---

## IMPORTANT DISCLAIMER

> **THIS IS NOT AN OFFICIAL QUANTCONNECT REPOSITORY**
>
> This is an independent, community-developed brokerage integration. It is **NOT** officially supported, endorsed, or maintained by QuantConnect Corporation or Fyers.

---

### **RISK WARNING - PLEASE READ CAREFULLY**

**LIVE TRADING INVOLVES SUBSTANTIAL RISK OF FINANCIAL LOSS.** By using this software for live trading, you acknowledge and accept the following:

1. **NO WARRANTY**: This software is provided "AS IS" without any warranty of any kind. The authors and contributors are **NOT** responsible for any financial losses, trading errors, or damages arising from the use of this software.

2. **USE AT YOUR OWN RISK**: Live trading with real money carries significant risk. You may lose some or all of your invested capital. Only trade with money you can afford to lose.

3. **NOT FINANCIAL ADVICE**: This software does not constitute financial, investment, or trading advice. Consult a qualified financial advisor before making any trading decisions.

4. **TESTING RECOMMENDED**: **ALWAYS** test thoroughly with paper trading before deploying any strategy with real funds. The authors strongly recommend extended testing in a simulated environment.

5. **UNOFFICIAL INTEGRATION**: This brokerage integration has **NOT** been officially reviewed or approved by QuantConnect or Fyers. There may be bugs, errors, or incompatibilities that could result in unexpected behavior.

6. **API CHANGES**: Fyers may change their API at any time, which could cause this integration to malfunction without notice.

7. **REGULATORY COMPLIANCE**: Users are solely responsible for ensuring compliance with all applicable laws, regulations, and exchange rules in their jurisdiction.

**BY USING THIS SOFTWARE, YOU AGREE THAT THE AUTHORS AND CONTRIBUTORS SHALL NOT BE HELD LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES.**

---

## Configuration Setup

Before running the brokerage, you must configure your credentials:

1. Copy `config.example.json` to `QuantConnect.FyersBrokerage.Tests/config.json`
2. Fill in your Fyers API credentials:

```json
{
  "data-folder": "../../../Data/",
  "fyers-client-id": "YOUR_APP_ID-100",
  "fyers-access-token": "YOUR_ACCESS_TOKEN",
  "fyers-trading-segment": "EQUITY",
  "fyers-product-type": "INTRADAY"
}
```

**NEVER commit your `config.json` file with real credentials.** The `.gitignore` file is configured to exclude this file.

To obtain your credentials:
- **Client ID**: Create an app at [Fyers API Dashboard](https://myapi.fyers.in/dashboard)
- **Access Token**: Generate via OAuth flow as documented at [Fyers API Docs](https://myapi.fyers.in/docsv3)

---

## Introduction

This repository hosts the Fyers Brokerage Plugin Integration with the QuantConnect LEAN Algorithmic Trading Engine. LEAN is a brokerage agnostic operating system for quantitative finance. Thanks to open-source plugins such as this [LEAN](https://github.com/QuantConnect/Lean) can route strategies to almost any market.

[LEAN](https://github.com/QuantConnect/Lean) is maintained primarily by [QuantConnect](https://www.quantconnect.com), a US based technology company hosting a cloud algorithmic trading platform. QuantConnect has successfully hosted more than 200,000 live algorithms since 2015, and trades more than $1B volume per month.

### About Fyers

<p align="center">
<picture >
  <source media="(prefers-color-scheme: dark)" srcset="https://assets.fyers.in/images/logo.svg">
  <source media="(prefers-color-scheme: light)" srcset="https://assets.fyers.in/images/logo.svg">
  <img alt="introduction" width="40%">
</picture>
<p>

[Fyers](https://fyers.in/) is a technology-driven stock broker based in India, founded in 2015. Fyers provides access to Indian Equities (NSE, BSE), Futures & Options, Currency, and Commodities (MCX) for clients in India. Fyers is known for its advanced trading platforms, competitive pricing with zero brokerage for equity delivery, and comprehensive API access for algorithmic trading.

For more information about Fyers API, see the [Fyers API Documentation](https://myapi.fyers.in/docsv3).

---

## Current Status - What Works and What Doesn't

### **NOT CURRENTLY SUPPORTED** (Requires Official QuantConnect Approval)

| Feature | Status | Reason |
|---------|--------|--------|
| `lean live` command with Fyers | **NOT AVAILABLE** | Fyers not added to official lean-cli modules |
| QuantConnect Cloud deployment | **NOT AVAILABLE** | Requires official QC integration |
| VSCode plugin deployment | **NOT AVAILABLE** | Requires official QC integration |
| QuantConnect Data Library | **NOT AVAILABLE** | Requires official QC integration |
| `BrokerageName.Fyers` enum | **NOT AVAILABLE** | Not in official LEAN engine |

### **AVAILABLE** (Current Implementation)

| Feature | Status | Notes |
|---------|--------|-------|
| Direct LEAN Engine integration | Available | Manual configuration required |
| Live order execution | Available | Through Fyers API |
| Real-time market data streaming | Available | WebSocket-based |
| Historical data for backtesting | Available | Through Fyers History API |
| Symbol mapping (NSE, BSE, MCX) | Available | Equities, F&O, Commodities |

---

## Using the Brokerage Plugin (Manual Integration)

Since this is **NOT** officially integrated into LEAN CLI, you must manually configure the brokerage:

### Step 1: Clone and Build

```bash
git clone https://github.com/s-ndipchaudh-ri/Lean.Brokerages.Fyers.git
cd Lean.Brokerages.Fyers
dotnet build
```

### Step 2: Configure lean.json

Add the following to your `lean.json` configuration:

```json
{
  "environment": "live-fyers",
  "live-mode": true,
  "live-mode-brokerage": "FyersBrokerage",
  "data-queue-handler": ["FyersBrokerage"],
  "fyers-client-id": "YOUR_APP_ID-100",
  "fyers-access-token": "YOUR_ACCESS_TOKEN",
  "fyers-trading-segment": "EQUITY",
  "fyers-product-type": "INTRADAY"
}
```

### Step 3: Reference the DLL

**Option A: Copy DLLs to LEAN Launcher folder**

```bash
# After building, copy the output DLLs to your LEAN Launcher bin directory
cp QuantConnect.FyersBrokerage/bin/Release/*.dll /path/to/Lean/Launcher/bin/Debug/
```

**Option B: Add Project Reference (if building LEAN from source)**

Add this to your `Lean/Launcher/Launcher.csproj`:

```xml
<ProjectReference Include="..\..\Lean.Brokerages.Fyers\QuantConnect.FyersBrokerage\QuantConnect.FyersBrokerage.csproj" />
```

**Option C: Add to config.json composer-dll-directory**

In your `config.json`, specify the directory containing the Fyers DLL:

```json
{
  "composer-dll-directory": "/path/to/Lean.Brokerages.Fyers/QuantConnect.FyersBrokerage/bin/Release"
}
```

### Step 4: Set Brokerage Model in Algorithm

```csharp
// Since BrokerageName.Fyers is not in official LEAN, use custom setup:
SetBrokerageModel(new FyersBrokerageModel());
```

**Note:** Full `lean live` CLI support will only be available after official QuantConnect approval and integration.

## Account Types

Fyers supports cash and margin accounts that trade in Indian Rupees.

## Order Types and Asset Classes

Fyers supports trading India Equities, Futures, and Options with the following order types:

- Market Order
- Limit Order
- Stop-Market Order
- Stop-Limit Order

## Downloading Data

**Note:** Official QuantConnect data providers are **NOT** available for this unofficial integration.

For historical data, this brokerage provides:

- **Fyers History API**: Use the built-in `FyersBrokerageDownloader` to download historical OHLCV data directly from Fyers
- **Real-time Data**: The brokerage streams live market data via WebSocket when connected

### Using FyersBrokerageDownloader

```csharp
var downloader = new FyersBrokerageDownloader("YOUR_CLIENT_ID", "YOUR_ACCESS_TOKEN");
var data = downloader.Get(new DataDownloaderGetParameters(symbol, resolution, startDate, endDate));
```

**Limitations:**
- Fyers API has rate limits on historical data requests
- Historical data availability depends on your Fyers subscription plan
- Some data may not be available for all instruments or timeframes

## Brokerage Model

Lean models the brokerage behavior for backtesting purposes. The margin model is used in live trading to avoid placing orders that will be rejected due to insufficient buying power.

**Since `BrokerageName.Fyers` is not in official LEAN**, use the custom brokerage model:

```csharp
// For this unofficial integration:
SetBrokerageModel(new FyersBrokerageModel(AccountType.Margin));
// or
SetBrokerageModel(new FyersBrokerageModel(AccountType.Cash));
```

### Fees

We model the order fees of Fyers with its fee structure. The following table shows the fees:

| Charge Item | Fee |
| --- | --- |
| Equity Delivery | Zero brokerage |
| Equity Intraday | ₹20 per order or 0.03% (whichever is lower) |
| F&O | ₹20 per order |
| Currency | ₹20 per order |
| Commodity | ₹20 per order |

To check the latest fees, see the [Pricing page](https://fyers.in/pricing) on the Fyers website.

### Margin

We model buying power and margin calls to ensure your algorithm stays within the margin requirements.

#### Buying Power

Fyers allows up to 5x leverage for margin accounts, but the amount of margin available depends on the Equity and product type. To check the amount of margin available for each asset, see the [Margin Calculator](https://fyers.in/margin-calculator/) on the Fyers website.

#### Margin Calls

Regulation T margin rules apply. When the amount of margin remaining in your portfolio drops below 5% of the total portfolio value, you receive a [warning](https://www.quantconnect.com/docs/v2/writing-algorithms/reality-modeling/margin-calls#08-Monitor-Margin-Call-Events). When the amount of margin remaining in your portfolio drops to zero or goes negative, the portfolio sorts the generated margin call orders by their unrealized profit and executes each order synchronously until your portfolio is within the margin requirements.

### Slippage

Orders through Fyers do not experience slippage in backtests. In live trading, your orders may experience slippage.

### Fills

We fill market orders immediately and completely in backtests. In live trading, if the quantity of your market orders exceeds the quantity available at the top of the order book, your orders are filled according to what is available in the order book.

### Settlements

If you trade with a margin account, trades settle immediately. If you trade with a cash account, Equity trades settle 2 days after the transaction date (T+2).

### Deposits and Withdraws

You can deposit and withdraw cash from your brokerage account while you run an algorithm that's connected to the account. We sync the algorithm's cash holdings with the cash holdings in your brokerage account every day at 7:45 AM Eastern Time (ET).

&nbsp;
&nbsp;
&nbsp;

![whats-lean](https://user-images.githubusercontent.com/79997186/184042682-2264a534-74f7-479e-9b88-72531661e35d.png)

&nbsp;
&nbsp;
&nbsp;

LEAN Engine is an open-source algorithmic trading engine built for easy strategy research, backtesting, and live trading. We integrate with common data providers and brokerages, so you can quickly deploy algorithmic trading strategies.

The core of the LEAN Engine is written in C#, but it operates seamlessly on Linux, Mac and Windows operating systems. To use it, you can write algorithms in Python 3.8 or C#. QuantConnect maintains the LEAN project and uses it to drive the web-based algorithmic trading platform on the website.

## Contributions

Contributions are warmly very welcomed but we ask you to read the existing code to see how it is formatted, commented and ensure contributions match the existing style. All code submissions must include accompanying tests. Please see the [contributor guide lines](https://github.com/QuantConnect/Lean/blob/master/CONTRIBUTING.md).

## Code of Conduct

We ask that our users adhere to the community [code of conduct](https://www.quantconnect.com/codeofconduct) to ensure QuantConnect remains a safe, healthy environment for
high quality quantitative trading discussions.

## License Model

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. You
may obtain a copy of the License at

<http://www.apache.org/licenses/LICENSE-2.0>

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language
governing permissions and limitations under the License.
