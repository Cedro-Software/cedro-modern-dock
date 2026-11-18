<!-- GETTING STARTED -->
# How To Contribute
> Only for those who wish to contribute in the project's coding, none of this is required for you to do if you [just want to download it](https://github.com/arthurdeka/cedro-modern-dock/releases)
### Understand the project architecture:

The project follows a layered architecture:

- `CedroModernDock.Core`: Domain models, Application services, and i18n (portable, no OS deps)
- `CedroModernDock.Infrastructure.Windows`: Windows-specific adapters (Win32 interop, icon extraction, JSON persistence, registry auto-start, tray icon)
- `CedroModernDock`: Avalonia UI (dock view, settings window, view models, view locators)
- `CedroModernDock.Tests`: xUnit test suite

`App.axaml.cs` is responsible for composing these dependencies and injecting them into the view models.

<!-- PREREQUISITES -->
### Prerequisites

Before you begin, ensure you have the following installed on your system: (required only for development)

- **An IDE** (Visual Studio 2022 or Rider recommended).
- **.NET 9 SDK** (or .NET 10 SDK).
- **Git** for cloning the repository.

<br>

### How To Run Locally

1. Clone the repository
2. Before running, make sure to install all the dependencies (`dotnet restore`)
3. Run `dotnet run --project src/CedroModernDock` to run the program
4. Make desired changes
5. Commit your changes
6. Open a pull request

<br>

### How To `COMPILE` Locally

1. Clone the repository
2. Install all dependencies (`dotnet restore`)
3. Run `dotnet build src/CedroModernDock -c Release` on the terminal
4. The `.exe` file will be available at `src/CedroModernDock/bin/Release/net9.0-windows/CedroModernDock.exe`

<br>
