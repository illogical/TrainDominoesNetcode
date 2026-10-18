## Multiplayer Train Dominoes
### Using Netcode for GameObjects

A Unity multiplayer Mexican Train prototype for 2–8 players with a double-twelve set, following the Puremco rules. The host runs a portable rules engine; each player receives only their own view. Start with [AGENTS.md](AGENTS.md), [TASKS.md](TASKS.md) and the [project state](Assets/Docs/PROJECT_STATE.md).

### Playing: board controls

Long trains can be scrolled and the board zoomed. This only changes what you are looking at; it never affects the game.

| | Mouse and trackpad | Touch |
| --- | --- | --- |
| Select a tile | Click | Tap |
| Scroll a train | Drag along it; Shift+wheel; or swipe sideways on a trackpad | Drag along it (a quick flick keeps it moving) |
| Zoom | Mouse wheel, centred on the pointer | Pinch |
| Scroll the board up and down (when zoomed in) | Drag up or down, or drag the bar at the right of the board | Drag with two fingers, or one finger up or down |
| Back to a train's playable end | Double-click the train, or press its `>|` button | Double-tap the train (again to see the whole board) |
| See the whole board | `F` or **Fit board** | **Fit board** |

A train that is showing its end keeps following new tiles. A train you have scrolled away from stays where it is, and its `>|` button pulses when new tiles arrive. Everything resets at the start of a round.

When you select a tile, the trains you can play it on are highlighted, and a **Play** marker appears at their ends. Click a train's button, its marker or its last tile to play there. Announcements, challenges and penalty draws appear as short notices at the top of the screen.

### Checks

From `RulesEngine/` (it pins the .NET SDK):

```sh
dotnet test
dotnet run --project ../Tests/BoardPresentation/BoardPresentation.Checks.csproj
dotnet run --project ../Tests/StateMachine/StateMachine.Checks.csproj
dotnet run --project ../Tests/ClientInteraction/ClientInteraction.Checks.csproj
dotnet run --project ../Tests/GameplayIntegration/GameplayIntegration.Checks.csproj
```

Real Unity players (built from an isolated project copy) are described in [Tests/UnityIntegration/README.md](Tests/UnityIntegration/README.md), including the board-navigation probes (`run_view.py`).
