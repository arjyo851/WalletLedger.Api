# Integration Tests Documentation

## Overview

This test suite provides comprehensive integration tests for all API endpoints in the WalletLedger.Api application. The tests use `WebApplicationFactory` to create an in-memory test server with a SQLite database.

## Test Structure

### Test Infrastructure

- **TestWebApplicationFactory**: Configures the test web application with in-memory SQLite database and test JWT settings
- **TestJwtHelper**: Generates JWT tokens for testing authentication
- **ApiIntegrationTests**: Main test class covering all API endpoints

## Test Coverage

### AuthController Tests (6 tests)
- ✅ Login with valid/invalid user ID
- ✅ Admin login
- ✅ Token refresh
- ✅ Token logout/revocation

### WalletController Tests (10 tests)
- ✅ Create wallet
- ✅ Get user wallets (with/without currency filter)
- ✅ Get wallet details
- ✅ Get balance
- ✅ Update wallet status
- ✅ Get balance at point in time
- ✅ Get balance history
- ✅ Create balance snapshot
- ✅ Authorization checks (unauthorized access to other user's wallets)

### TransactionController Tests (9 tests)
- ✅ Credit transaction
- ✅ Debit transaction
- ✅ Wallet-to-wallet transfer
- ✅ Transfer with different currencies (error case)
- ✅ Get transaction history (with filters)
- ✅ Get transaction by ID
- ✅ Get transaction by reference ID
- ✅ Idempotency (duplicate reference ID handling)
- ✅ Insufficient balance error handling

### HealthController Tests (4 tests)
- ✅ Basic health check
- ✅ Detailed health check (admin only)
- ✅ Metrics endpoint (admin only)
- ✅ Authorization checks

### AdminController Tests (2 tests)
- ✅ Admin health endpoint
- ✅ Authorization checks

## Running Tests

```bash
cd WalletLedger.Tests
dotnet test
```

To run with verbose output:
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Database

Tests use an in-memory SQLite database that is created fresh for each test class. The database is automatically cleaned up after tests complete.

## Authentication

Tests use `TestJwtHelper` to generate JWT tokens with appropriate permissions:
- **User Token**: Includes WalletRead, WalletWrite, TransactionCredit, TransactionDebit permissions
- **Admin Token**: Includes AdminHealth permission

## Notes

- All tests are isolated and can run in parallel
- Each test creates its own test data
- Tests verify both success and failure scenarios
- Authorization and authentication are fully tested




