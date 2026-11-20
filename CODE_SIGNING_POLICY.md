# Code signing policy

Sleeve's Windows release (`Sleeve.exe`) is built by GitHub Actions from this public repository
([`.github/workflows/publicar.yml`](.github/workflows/publicar.yml)) every time the version in
`src/windows/Sleeve.csproj` changes. Nothing is built or uploaded by hand.

Once the project is accepted into the SignPath Foundation open-source program, release builds will use:
**Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).**

## Scope

Only `Sleeve.exe` attached to the [GitHub Releases](https://github.com/paulocmarinho/sleeve/releases) of this
repository is signed. Third-party libraries bundled inside it (WebView2 SDK, NAudio, TagLibSharp) are used
under their own licenses and are not represented as Sleeve's code.

## Team and roles

| Role | Member |
| --- | --- |
| Committer and reviewer | [Paulo Marinho (@paulocmarinho)](https://github.com/paulocmarinho) |
| Approver (signing requests) | [Paulo Marinho (@paulocmarinho)](https://github.com/paulocmarinho) |

All members use multi-factor authentication on GitHub and SignPath.

## Privacy

Sleeve does not collect usage data and has no accounts. It only contacts the internet for features the user
can see and switch off in *Settings*:

- **Updates**: the GitHub Releases API of this repository, to look for a newer version.
- **High-resolution covers**: the iTunes Search API (artist and album name).
- **Lyrics**: [LRCLIB](https://lrclib.net) (song title, artist, album and duration).
- **Artist photos**: the public [Deezer API](https://developers.deezer.com) (artist name, album and song titles).

Everything else (library, playlists, play counts, settings) stays on the user's computer.
