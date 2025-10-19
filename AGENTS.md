# Repository Guidelines

## Project Structure & Module Organization
Source lives under `src/`, with `NX.GameInfo.Core` hosting metadata extraction services and reusable models, and `NX.GameInfo.Cli` providing the cross-platform command-line entry point. The macOS GUI shell lives in `macOS/`, while legacy assets and installers sit in `NX_Game_Info/` and `TaskDialog/`. Automated tests reside in `tests/NX.GameInfo.Core.Tests`, and shared build configuration is centralized in `Directory.Build.props` and `Directory.Packages.props`.

## Build, Test, and Development Commands
Run `dotnet restore NX_Game_Info.sln` to fetch dependencies across all targets. Build the macOS desktop client with `dotnet build macOS.sln`; build the CLI via `dotnet build src/NX.GameInfo.Cli/NX.GameInfo.Cli.csproj`. Execute unit tests with `dotnet test tests/NX.GameInfo.Core.Tests/NX.GameInfo.Core.Tests.csproj`. For quick CLI validation, use `dotnet run --project src/NX.GameInfo.Cli/NX.GameInfo.Cli.csproj -- ~/Games/sample.nsp`.

## Coding Style & Naming Conventions
This codebase follows the default .NET conventions: four-space indentation, PascalCase for types and public members, camelCase for locals, and `_snake`-prefixed private fields only when readonly injection does not apply. Nullable reference types and implicit usings are enabled via `Directory.Build.props`; keep analyzers quiet and favor expressions that surface diagnostics early. When adding configuration or assets, prefer repository-relative paths and keep OS-specific code isolated inside the corresponding platform project.

## Testing Guidelines
Use xUnit in `NX.GameInfo.Core.Tests` and follow the `MethodUnderTest_Condition_Outcome` naming pattern. Add regression coverage for new parsers or edge cases alongside feature work, and verify test pass locally with `dotnet test`. For scenarios requiring key material, stub dependencies instead of committing sample keys; tests must run without Nintendo secrets.

## Commit & Pull Request Guidelines
Commit messages mirror the existing history: short imperative or sentence-case statements such as “Add cartridge update partition check”. Group related changes together, and avoid mixing formatting-only edits with functional work. Pull requests should explain the problem, outline the solution, list any manual verification (commands run, platforms touched), and attach screenshots or CLI excerpts when UI or output changes. Link related issues and flag breaking changes clearly.

## Security & Configuration Tips
Document any requirement for `prod.keys`, `title.keys`, `console.keys`, or `hac_versionlist.json` in PRs that depend on them. Never commit secrets; reference `$HOME/.switch` as the standard location. When handling user-provided archives, validate inputs defensively and rely on LibHac helpers rather than ad-hoc parsing.
