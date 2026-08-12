# GuessingGame API

GuessingGame API is an ASP.NET Core Web API for running a multiplayer guessing game. Players join a game with stakes, submit guesses over multiple rounds, and may enter a rollup round when the selected game type allows it.

The project uses:

- ASP.NET Core Web API
- Entity Framework Core
- Microsoft SQL Server
- Swagger / OpenAPI
- Repository and service patterns

## Game Flow

1. A game is created with a game type, players, and their stakes.
2. SQL Server automatically assigns IDs to the game, players, game entries, and guesses.
3. The game is started, which sets `CurrentRound` to `1` and allows guesses.
4. Each active player submits one guess for the current round.
5. When the final active player submits, the API automatically evaluates the round.
6. A player who guesses correctly is marked as `Won` and stops submitting normal guesses.
7. Remaining active players continue until they guess correctly or the maximum attempts are reached.
8. If multiple players succeed and the game type allows rollup, those players enter rollup.
9. Rollup ends with one final winner or no winner.

## Project Structure

```text
GuessingGame.API
├── Controllers
│   ├── GameController.cs
│   └── RoundController.cs
├── Data
│   └── AppDbContext.cs
├── DTOs
│   ├── Request
│   │   ├── CreateGamePlayerRequest.cs
│   │   ├── CreateGameRequest.cs
│   │   └── SubmitGuessRequest.cs
│   └── Response
│       ├── ApiResponse.cs
│       ├── CreateGameResponse.cs
│       ├── GamePlayerResponse.cs
│       ├── GameStateResponse.cs
│       ├── RoundEvaluationResult.cs
│       └── SubmitGuessResponse.cs
├── Migrations
│   ├──20260809234410_InitialCreate.cs
│   └──AppDbContextModelSnapshot.cs
├── Models
│   ├── Enums
│   │   ├── GameStatus.cs
│   │   ├── GameType.cs
│   │   └── PlayerStatus.cs
│   ├── GameConfig.cs
│   ├── GamePlayer.cs
│   ├── GameSession.cs
│   ├── GameSettings.cs
│   ├── Player.cs
│   └── PlayerGuess.cs
├── Repositories
│   ├── Interfaces
│   │   ├── IGameRepository.cs
│   │   └── IPlayerRepository.cs
│   ├── GameRepository.cs
│   └── PlayerRepository.cs
├── Services
│   ├── Interfaces
│   │   ├── IGameService.cs
│   │   └── IRoundService.cs
│   ├── GameService.cs
│   ├── GuessParser.cs
│   ├── RandomGenerator.cs
│   ├── RoundService.cs
│   └── Validation.cs
├── Program.cs
└── appsettings.json
```

## Models and Relationships

### Player

Stores the permanent player profile, including:

- Name
- Balance
- Games played
- Total wins
- Best score
- Total score

### GameSession

Stores one game and its current state, including:

- Game type
- Status
- Current round
- Maximum attempts
- Winning numbers
- Rollup round
- Start and completion times

### GamePlayer

Represents a player's participation in one specific game.

```text
GamePlayer.GameSessionId → GameSession.Id
GamePlayer.PlayerId      → Player.Id
```

It stores the player's stake, status, score, winnings, and winning round for that game.

### PlayerGuess

Stores one guess submission made by one `GamePlayer`.

```text
PlayerGuess.GamePlayerId → GamePlayer.Id
```

It also stores the round number, guess values, match count, and whether the guess was a rollup guess.

## ID Assignment

SQL Server automatically assigns integer IDs after `SaveChangesAsync()` for:

```text
Player.Id
GameSession.Id
GamePlayer.Id
PlayerGuess.Id
```

The application does not manually assign these values.

## Duplicate Protection

The database prevents the same player from being added to the same game twice:

```csharp
modelBuilder.Entity<GamePlayer>()
    .HasIndex(x => new { x.GameSessionId, x.PlayerId })
    .IsUnique();
```

It also prevents the same player from submitting more than one guess of the same type in one round:

```csharp
modelBuilder.Entity<PlayerGuess>()
    .HasIndex(x => new
    {
        x.GamePlayerId,
        x.RoundNumber,
        x.IsRollupGuess
    })
    .IsUnique();
```

## Prerequisites

Install the following:

- .NET SDK matching the project's target framework
- SQL Server Developer Edition or SQL Server Express
- Visual Studio 2022 or later
- DBeaver or SQL Server Management Studio

## Configuration

Add a SQL Server connection string to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=GuessingGameDb;User Id=Id;Password=CHANGE_ME;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

## Entity Framework Core Migrations

Open Visual Studio's Package Manager Console and ensure the API project is the selected default project.

Create the migration:

```powershell
Add-Migration InitialCreate -Context AppDbContext
```

Apply it to SQL Server:

```powershell
Update-Database -Context AppDbContext
```

After changing an entity model, create another migration:

```powershell
Add-Migration DescribeTheChange
Update-Database
```

The migration is generated from:

- Entity models
- `DbSet` properties in `AppDbContext`
- Relationships and constraints configured in `OnModelCreating()`

Controllers, DTOs, services, and repositories do not normally define the database structure.

## Running the API

Restore packages and build the project:

```powershell
dotnet restore
dotnet build
```

Run the application:

```powershell
dotnet run
```

Open the Swagger URL shown in the console, for example:

```text
https://localhost:7137/swagger
```

## API Endpoints

### Create a game

```http
POST /api/games
```

Example request:

```json
{
  "gameType": "Medium",
  "players": [
    {
      "playerName": "Ikechukwu",
      "stake": 1000
    },
    {
      "playerName": "David",
      "stake": 1000
    }
  ]
}
```

The endpoint creates or retrieves the permanent players, creates the game, creates each `GamePlayer` entry, and returns the generated IDs.

### Get a game

```http
GET /api/games/{gameId}
```

Returns the current game status, round, players, scores, and winnings. Winning numbers should remain hidden until the game is completed.

### Start a game

```http
POST /api/games/{gameId}/start
```

This endpoint:

- Deducts the players' stakes
- Sets `CurrentRound` to `1`
- Sets the status to `WaitingForGuesses`
- Records `StartedAt`

### Submit a normal guess

```http
POST /api/games/{gameId}/guesses
```

Example request:

```json
{
  "playerId": 1,
  "guesses": "44 22 33 11"
}
```

The round number is not entered by the user. It is obtained from:

```csharp
game.CurrentRound
```

Each active player calls this endpoint once per round. When the final required player submits, the API evaluates the round automatically.

### Submit a rollup guess

```http
POST /api/games/{gameId}/rollup/guesses
```

Example request:

```json
{
  "playerId": 1,
  "guesses": "44 22 33 11"
}
```

Only players whose status is `InRollup` may submit through this endpoint.

### Cancel a game

```http
POST /api/games/{gameId}/cancel
```

Cancels an unfinished game and refunds stakes when they were already deducted.

### Get completed results

```http
GET /api/games/{gameId}/results
```

Returns the completed game result, including the winning numbers and winner information.

## Game Statuses

```text
WaitingForPlayers
WaitingForGuesses
WaitingForRollupGuesses
Completed
Cancelled
```

## Player Statuses

```text
Active
WonRound
InRollup
Lost
LostInRollup
FinalWinner
```

## Automatic Round Evaluation

The normal guess endpoint counts how many active players submitted for the current round:

```csharp
int submitted = requiredPlayers.Count(player =>
    player.Guesses.Any(guess =>
        guess.RoundNumber == round &&
        guess.IsRollupGuess == isRollup));
```

If not everyone has submitted, the API waits. When all required players have submitted, the API automatically evaluates the round.

During normal rounds:

- Correct players become `Won` and stop guessing.
- Incorrect players remain `Active` and continue.
- `CurrentRound` increases when attempts remain.
- The game ends when everyone succeeds or attempts finish.

## Rollup Rules

Rollup starts only when:

```text
AllowRollup is true
AND
multiple players succeeded during normal rounds
```

During rollup:

- One correct player becomes the final winner.
- No correct players means the game ends without a winner.
- Multiple correct players continue into another rollup round.

## Viewing Tables in DBeaver

Expand:

```text
SQL Server connection
└── Databases
    └── GuessingGameDb
        └── Schemas
            └── dbo
                └── Tables
```

Expected tables:

```text
Players
GameSessions
GamePlayers
PlayerGuesses
__EFMigrationsHistory
```

Example queries:

```sql
SELECT * FROM dbo.Players;
SELECT * FROM dbo.GameSessions;
SELECT * FROM dbo.GamePlayers;
SELECT * FROM dbo.PlayerGuesses;
```

Keep DBeaver in auto-commit mode and avoid leaving unsaved table edits, because open transactions may block API queries.

## License

This project is intended for learning and development.