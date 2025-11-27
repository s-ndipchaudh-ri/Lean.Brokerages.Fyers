# Contributing to Lean.Brokerages.Fyers

Thank you for your interest in contributing to the Fyers brokerage integration for QuantConnect's LEAN engine!

## How to Contribute

### Reporting Issues

1. Search existing issues to avoid duplicates
2. Use the issue template provided
3. Include:
   - Clear description of the problem
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version, LEAN version)

### Submitting Pull Requests

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes following our coding standards
4. Write/update tests for your changes
5. Ensure all tests pass: `dotnet test`
6. Submit a pull request using the PR template

## Development Setup

### Prerequisites

- .NET 6.0 SDK or later
- QuantConnect LEAN engine (cloned as sibling directory)
- Fyers trading account (for integration tests)

### Building

```bash
# Clone the repository
git clone https://github.com/QuantConnect/Lean.Brokerages.Fyers.git
cd Lean.Brokerages.Fyers

# Ensure LEAN is in sibling directory
# ../Lean should exist

# Build
dotnet build

# Run tests
dotnet test
```

### Configuration for Tests

Create `QuantConnect.FyersBrokerage.Tests/config.json`:

```json
{
  "fyers-client-id": "YOUR_CLIENT_ID",
  "fyers-access-token": "YOUR_ACCESS_TOKEN",
  "fyers-trading-segment": "EQUITY",
  "fyers-product-type": "INTRADAY"
}
```

**Note:** Never commit credentials. The config.json is gitignored.

## Coding Standards

### Style Guidelines

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use 4-space indentation (soft tabs)
- Keep lines under 120 characters
- Use meaningful variable and method names

### Code Requirements

1. **License Header**: All source files must include the Apache 2.0 license header
2. **XML Documentation**: Public classes and methods must have XML documentation
3. **Unit Tests**: New features must include unit tests
4. **No Warnings**: Code must compile without warnings

### Example License Header

```csharp
/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/
```

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~FyersBrokerageTests"

# Run with verbose output
dotnet test -v n
```

### Test Categories

- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test against live Fyers API (require credentials)
- **Regression Tests**: Test specific bug fixes

## Pull Request Process

1. Update documentation if needed
2. Add tests for new functionality
3. Ensure CI pipeline passes
4. Request review from maintainers
5. Address review feedback
6. Squash commits before merge

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow

## Questions?

- Open a GitHub issue for bugs/features
- Join [QuantConnect Community](https://www.quantconnect.com/forum) for discussions

## License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.
