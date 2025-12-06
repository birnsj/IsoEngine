using GameClient;

// Entry point for the game
// Build Instructions:
// 1. Ensure .NET 8 SDK is installed
// 2. Restore NuGet packages: dotnet restore
// 3. Build the solution: dotnet build
// 4. Run the game: dotnet run --project GameClient
// Or open IsoEngine.sln in Visual Studio and run from there

try
{
    Console.WriteLine("Starting IsoEngine Game...");
    using var game = new IsoEngineGame();
    Console.WriteLine("Game initialized. Starting game loop...");
    game.Run();
    Console.WriteLine("Game loop ended normally.");
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL ERROR: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    throw;
}

