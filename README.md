# WalletLedger.Api

A production-ready wallet and ledger backend system built with ASP.NET Core, providing secure transaction management, wallet operations, and comprehensive audit logging.

## Features

- **Transaction Management**: Credit, debit, and wallet-to-wallet transfers with full transaction history
- **Wallet Management**: Create and manage multiple wallets per user with status controls
- **Audit Logging**: Comprehensive audit trail for all operations
- **Balance Tracking**: Real-time balance calculations with historical snapshots
- **Security**: JWT-based authentication with permission-based authorization
- **Reporting**: Transaction summaries and balance history queries

## Technology Stack

- **.NET 10.0** - Latest .NET framework
- **ASP.NET Core** - Web API framework
- **Entity Framework Core** - ORM with SQL Server
- **JWT Authentication** - Token-based authentication
- **In-Memory Cache** - Caching for balance queries
- **Swagger/OpenAPI** - API documentation

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- SQL Server (or SQL Server Express)
- Visual Studio 2022 or VS Code

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd WalletLedger.Api
   ```

2. **Configure the database connection**
   
   Update `appsettings.json` with your SQL Server connection string:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=WalletLedger;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. **Apply database migrations**
   ```bash
   cd WalletLedger.Api
   dotnet ef database update --context WalletLedgerDbContext
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

5. **Access the API documentation**
   
   Navigate to `https://localhost:5001/swagger` (or your configured port)

## API Endpoints

### Authentication
- `POST /api/auth/login` - User login
- `POST /api/auth/refresh` - Refresh JWT token

### Wallets
- `GET /api/wallets` - List user wallets
- `GET /api/wallets/{walletId}` - Get wallet details
- `POST /api/wallets` - Create new wallet
- `GET /api/wallets/{walletId}/balance` - Get current balance
- `PUT /api/wallets/{walletId}/status` - Update wallet status

### Transactions
- `POST /api/transactions/credit` - Credit a wallet
- `POST /api/transactions/debit` - Debit a wallet
- `POST /api/transactions/transfer` - Transfer between wallets
- `GET /api/transactions/history` - Get transaction history
- `GET /api/transactions/{transactionId}` - Get transaction by ID
- `GET /api/transactions/by-reference/{walletId}/{referenceId}` - Get transaction by reference ID

### Reporting
- `GET /api/wallets/{walletId}/balance/point-in-time` - Balance at specific date
- `GET /api/wallets/{walletId}/balance/history` - Balance history snapshots
- `GET /api/wallets/{walletId}/summary/monthly` - Monthly transaction summary

### Health & Monitoring
- `GET /health` - Health check endpoint

## Authentication & Authorization

The API uses JWT Bearer token authentication. Include the token in the Authorization header:

```
Authorization: Bearer <your-token>
```

### Permissions

- **WalletRead** - Read wallet information
- **WalletWrite** - Create/update wallets
- **TransactionCredit** - Create credit transactions
- **TransactionDebit** - Create debit transactions
- **AdminHealth** - Access health check endpoints

## Project Structure

```
WalletLedger.Api/
├── Application/
│   ├── Interfaces/          # Service interfaces
│   └── Services/            # Business logic services
├── Auth/                     # Authentication & authorization
├── Contracts/
│   ├── Requests/            # Request DTOs
│   └── Responses/           # Response DTOs
├── Controllers/             # API endpoints
├── Data/
│   └── WalletLedgerDbContext.cs  # EF Core DbContext
├── Domain/
│   └── Entities/            # Domain models
├── Middleware/              # Custom middleware
└── Migrations/              # Database migrations
```

## Database Schema

### Core Entities

- **User** - System users
- **Wallet** - User wallets with currency and status
- **LedgerEntry** - Transaction records (credit/debit)
- **AuditLog** - Audit trail for all operations
- **BalanceSnapshot** - Historical balance snapshots
- **RefreshToken** - JWT refresh tokens

## Security Features

- JWT-based authentication
- Permission-based authorization
- Rate limiting (100 requests per minute per user)
- Input validation
- SQL injection prevention (EF Core parameterized queries)
- Comprehensive audit logging

## Performance

- Indexed database queries for optimal performance
- In-memory caching for balance calculations
- Efficient pagination for large datasets
- Database connection pooling

## Testing

The project includes integration tests in the `WalletLedger.Tests` project.

Run tests:
```bash
dotnet test
```

## Development

### Adding a New Migration

```bash
dotnet ef migrations add <MigrationName> --context WalletLedgerDbContext
dotnet ef database update --context WalletLedgerDbContext
```

### Code Style

- C# nullable reference types enabled
- Implicit usings enabled
- Follows ASP.NET Core conventions

## Key Features

### Idempotency

All transaction operations support idempotent execution via `ReferenceId`. Submitting the same `ReferenceId` multiple times will not create duplicate transactions.

### Atomic Transactions

Wallet-to-wallet transfers are executed atomically using database transactions, ensuring data consistency.

### Audit Trail

All operations are logged to the `AuditLog` table, including user actions, IP addresses, and timestamps for compliance and debugging.
