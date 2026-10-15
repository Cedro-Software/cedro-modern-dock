![GitHub forks](https://img.shields.io/github/forks/arthurdeka/cedro-modern-dock?style=for-the-badge)
![GitHub Repo stars](https://img.shields.io/github/stars/arthurdeka/cedro-modern-dock?style=for-the-badge)
![GitHub Issues or Pull Requests](https://img.shields.io/github/issues/arthurdeka/cedro-modern-dock?style=for-the-badge)
![GitHub License](https://img.shields.io/github/license/arthurdeka/cedro-modern-dock?style=for-the-badge)
![X (formerly Twitter) Follow](https://img.shields.io/twitter/follow/TeiuAlligator?style=for-the-badge)
![Avalonia](https://img.shields.io/badge/Avalonia-11.3-0080ff?style=for-the-badge&logo=avalonia&logoColor=white)

<img width="800" alt="Cedro Modern Dock demo" src="docs/assets/demo.gif" />



> Follow @TeiuAlligator on X to stay tuned for new features and updates!


> ## How To Install
> 1. Go to [Releases](https://github.com/arthurdeka/cedro-modern-dock/releases):
> 2. Download the latest `CedroModernDock-1.2.0-x64.msi` file
> 3. Execute and install
<br>

## What's new in v1.2

- **Live window previews:** Hover a running app to see its open windows in real time, click one to bring it forward.
- **Running apps you haven't pinned:** Apps currently open but not pinned show up on the dock now.
- **Customize your icons:** iOS-style color tint with 12 presets or pick your own color.
- **Drag to reorder:** Now you can rearrange your dock shortcuts right in the Settings list by dragging them around.
- **Vertical dock:** Arrange the dock vertically on either side of the screen.
- **Updates that keep your setup:** - Upgrading to a new version replaces the app but keeps your shortcuts and settings.
- **Long-awaited feature:** Now CTRL + D does NOT minimizes the Dock anymore. It remains on the Desktop.
<br>

## Supported Languages

- 🇺🇸  English
- 🇧🇷 Portuguese (Brazil)
- 🇪🇸 Spanish
- 🇫🇷 French
- 🇩🇪 German
- 🇯🇵 Japanese
- 🇨🇳 Chinese (Simplified)
- 🇹🇼 Chinese (Traditional)
- 🇮🇳 Hindi
- 🇸🇦 Arabic
- 🇧🇩 Bengali
- 🇷🇺 Russian
- 🇵🇰 Urdu
- 🇮🇩 Indonesian
- 🇳🇬 Nigerian Pidgin
- 🇮🇳 Marathi
- 🇮🇳 Telugu
- 🇹🇷 Turkish
- 🇮🇳 Tamil
- 🇭🇰 Cantonese
- 🇻🇳 Vietnamese
<br>

<!-- BUILT WITH -->
## Built With

* ![.NET](https://img.shields.io/badge/.NET-9-5122d3?style=for-the-badge&logo=dotnet&logoColor=white)
* ![Avalonia](https://img.shields.io/badge/Avalonia-11.3-0080ff?style=for-the-badge&logo=avalonia&logoColor=white)
<br>

<!-- GETTING STARTED -->
## How To Contribute
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
