# deyvien's KeyASIO fork

Fork: https://github.com/deyvien/KeyASIO.Net

Upstream: https://github.com/Milkitic/KeyASIO.Net

The starting source is upstream commit
`7d9516aa2f365f69d7c0326071ebd9cb3c268dbb`, retained by the annotated tag
`fork-baseline-2026-11-09`. This is the latest upstream master source at setup,
including the v4 preview work; it is newer than stable release v3.2.0.
The initial fork does not change audio behavior.

## Feature history

- `origin` is this fork; `upstream` is the original repository.
- Keep `master` as the reviewed integration branch. Start each authorized change
  on `feat/<short-name>`, `fix/<short-name>`, or `chore/<short-name>`.
- Give each feature or fix a GitHub issue describing its behavior and acceptance
  checks. Reference that issue in its commits and pull request into this fork.
- Use focused commits and record the build/test result in the pull request.
  Never commit personal settings, logs, binaries, credentials, or private keys.
- Fetch upstream explicitly and integrate updates in a separate reviewed branch.
  Preserve upstream history and the pinned submodule commits; do not force-push
  shared branches or rewrite baseline tags.
- Record the source commit and executable hash with each local delivery.
  Measured timing results must identify the build and audio configuration tested.

## Build and launch

Install the .NET 10 SDK, then from the checkout:

```powershell
git submodule update --init --recursive
dotnet build src/Apps/KeyAsio/KeyAsio.csproj -c Release
./Start-Fork.ps1
```

For a portable SDK, pass `-DotnetRoot <SDK-directory>` to the launcher.
Settings and logs stay in the ignored build output directory. This keeps them
separate from another KeyASIO installation. The application's normal single
instance behavior still applies. Its upstream update UI does not rebuild this
fork; update this checkout through Git and rebuild it instead.

The setup branch contains only this workflow and the launcher. Future audio
features and latency fixes belong in their own branches.
