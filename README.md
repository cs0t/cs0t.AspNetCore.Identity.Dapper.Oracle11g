# cs0t.AspNetCore.Identity.Dapper.Oracle11g

Originally forked from : https://github.com/simonfaltum/AspNetCore.Identity.Dapper

A Dapper-backed implementation of ASP.NET Core Identity user and role stores for Oracle 11g, specifically Oracle Database 11g 11.2.0.3.0.

The package uses `ApplicationUser : IdentityUser<long>` and `ApplicationRole : IdentityRole<long>`, supports the standard Identity manager APIs, and targets `netstandard2.1` and `net8.0`.

## Features

- Dapper-based `IUserStore` and `IRoleStore` implementations
- Users, roles, claims, user-role links, external logins, and authentication tokens
- Oracle sequences and `RETURNING ... INTO` for generated `long` IDs
- Optimistic concurrency through Identity concurrency stamps
- Lazy relationship loading with in-memory synchronization
- Atomic aggregate creation and updates
- Configurable schema, table names, and sequence names
- Oracle 11g-compatible DML and managed Oracle connections
- Custom Dapper handlers for `bool`, `DateTimeOffset`, and `Guid`

## Persistence and in-memory synchronization

Core lookups such as `FindByIdAsync`, `FindByNameAsync`, and `FindByEmailAsync` load only the user or role row. Relationship collections remain `null` until an Identity operation needs them.

### Collection state contract

For `ApplicationUser.Claims`, `Roles`, `Logins`, and `Tokens`, and `ApplicationRole.Claims`:

| Collection state | Meaning during `CreateAsync` or `UpdateAsync` |
| --- | --- |
| `null` | Not loaded or changed. The corresponding table is not modified. |
| Empty list | Authoritative empty state. Existing relationships are deleted. |
| Populated list | Authoritative state. Existing rows are deleted and the list is reinserted. |

This null boundary protects relationships when a detached or partially populated object is updated. Do not initialize an unused collection on a detached object unless replacing that relationship set is intentional.

### Point operations

Identity operations such as adding a claim, assigning a role, adding a login, or setting a token use write-through synchronization:

1. The relevant collection is loaded once if it is currently `null`.
2. Logical duplicates are rejected or skipped in memory.
3. A targeted `INSERT`, `UPDATE`, `MERGE`, or `DELETE` is executed immediately.
4. The in-memory collection is changed only after the SQL operation succeeds.

Further reads of that collection on the same object use memory. Unrelated collections remain unloaded.

`ApplicationUser.Roles` uses `ApplicationUserRole`, which adds `RoleName` and `NormalizedRoleName` to the Identity user-role link. This allows role-name reads and membership checks from the loaded collection.

### Aggregate operations

`CreateAsync` and `UpdateAsync` persist the core row and every non-null relationship collection in one transaction. Child foreign keys are assigned from the aggregate root ID. Duplicate logical relationships in detached aggregates are rejected before SQL is executed.

Updates use an atomic optimistic-concurrency predicate: the store creates a new concurrency stamp, while the provider compares the database stamp with the original stamp in the `UPDATE` statement. A conflict rolls back the transaction and restores the original in-memory stamp.

Deletes expect the Oracle schema to cascade from users and roles to their dependent relationship rows.

## Oracle type handling

`AddOracle11GConnectionFactory` registers the handlers globally with Dapper:

- `OracleBoolHandler` maps nullable and non-nullable `bool` values to numeric `0`/`1` values suitable for an Oracle numeric column.
- `OracleDateTimeOffsetHandler` maps `DateTimeOffset` to Oracle `TIMESTAMP WITH TIME ZONE` and writes values in UTC.
- `OracleGuidHandler` maps `Guid` to 16-byte binary data and converts between .NET and Oracle byte ordering. Use an Oracle `RAW(16)` column for values handled this way.

The default connection factory also enables bind-by-name and configures the managed Oracle client to permit Oracle 11g authentication:

```csharp
OracleConfiguration.SqlNetAllowedLogonVersionClient =
    OracleAllowedLogonVersionClient.Version11;
```

## Installation

Install the package when it is available from your configured NuGet source:

```powershell
dotnet add package cs0t.AspNetCore.Identity.Dapper.Oracle11g
```

For source development, add a project reference instead:

```powershell
dotnet add reference path\to\cs0t.AspNetCore.Identity.Dapper.Oracle11g.csproj
```

## ASP.NET Core Identity setup

Registration is split into two `IdentityBuilder` extensions:

- `AddDapperStores()` registers `UserStore` and `RoleStore` with ASP.NET Core Identity.
- `AddOracle11GConnectionFactory(options => ...)` configures Oracle 11g, registers the Dapper type handlers, and registers the scoped `IDatabaseConnectionFactory`.

Call both methods after `AddIdentityCore<ApplicationUser>()` and
`AddRoles<ApplicationRole>()`. `AddDapperStores` supports exactly the provided
`ApplicationUser` and `ApplicationRole` types; custom Identity entity types are
not supported.

```csharp
using cs0t.AspNetCore.Identity.Dapper.Oracle11g;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using Microsoft.AspNetCore.Identity;

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<ApplicationRole>()
    .AddDapperStores()
    .AddOracle11GConnectionFactory(options =>
    {
        options.ConnectionString = builder.Configuration
            .GetConnectionString("IdentityDatabase")!;
        options.DbSchema = "IDENTITY";
    });
```

The same registration works with `AddIdentity<ApplicationUser, ApplicationRole>()`:

```csharp
using cs0t.AspNetCore.Identity.Dapper.Oracle11g;
using cs0t.AspNetCore.Identity.Dapper.Oracle11g.Models;
using Microsoft.AspNetCore.Identity;

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddDapperStores()
    .AddOracle11GConnectionFactory(options =>
    {
        options.ConnectionString = builder.Configuration
            .GetConnectionString("IdentityDatabase")!;
        options.DbSchema = "IDENTITY";
    });
```

The separation is intentional: store registration is independent of connection
creation. Applications using the package's default Oracle connection factory
should call both extensions. Advanced applications may call `AddDapperStores()`
and register their own `IDatabaseConnectionFactory`; in that case they are also
responsible for equivalent Oracle client and Dapper type-handler configuration.

## Database configuration

The application must reference or otherwise provide Oracle Managed Data Access.
`AddOracle11GConnectionFactory` globally configures that provider for Oracle 11g
authentication (`OracleConfiguration.SqlNetAllowedLogonVersionClient =
OracleAllowedLogonVersionClient.Version11`) and enables bind-by-name. It also
globally registers Dapper handlers for `bool`, nullable `bool`,
`DateTimeOffset`, nullable `DateTimeOffset`, `Guid`, and nullable `Guid`.

It registers a scoped `IDatabaseConnectionFactory`; the factory creates and
asynchronously opens managed `OracleConnection` instances.

The application must provide an existing Oracle schema with the required
Identity tables, sequences, foreign keys, indexes, and constraints. This
package does not provide migrations. The default object names are:

| Purpose | Default name |
| --- | --- |
| Users | `ASPNET_USERS` |
| Roles | `ASPNET_ROLES` |
| User roles | `ASPNET_USER_ROLES` |
| User claims | `ASPNET_USER_CLAIMS` |
| Role claims | `ASPNET_USER_ROLE_CLAIMS` |
| User logins | `ASPNET_USER_LOGINS` |
| User tokens | `ASPNET_USER_TOKENS` |
| User IDs | `SEQ_ASPNET_USERS` |
| Role IDs | `SEQ_ASPNET_ROLES` |
| User claim IDs | `SEQ_USER_CLAIMS` |
| Role claim IDs | `SEQ_ROLE_CLAIMS` |

Names can be overridden in `AddOracle11GConnectionFactory`:

```csharp
.AddOracle11GConnectionFactory(options =>
{
    options.ConnectionString = connectionString;
    options.DbSchema = "IDENTITY";
    options.UsersTableName = "APP_USERS";
    options.RolesTableName = "APP_ROLES";
    options.UsersSequence = "SEQ_APP_USERS";
    options.RolesSequence = "SEQ_APP_ROLES";
});
```

`ConnectionString` is required. When supplied, `DbSchema` is trimmed,
normalized to uppercase, and has `[`, `]`, and backtick SQL identifier quoting
characters stripped. Table and sequence names can all be overridden through
`DbProviderOptions`; supply valid Oracle identifiers.

Database constraints should enforce the same logical uniqueness used by the stores:

- User claims: `(UserId, ClaimType, ClaimValue)`
- Role claims: `(RoleId, ClaimType, ClaimValue)`
- User roles: `(UserId, RoleId)`
- External logins: `(LoginProvider, ProviderKey)`
- User tokens: `(UserId, LoginProvider, Name)`

## License

Licensed under the [MIT License](LICENSE).
