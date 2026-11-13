# Naiad source dependency

Issue [#48](https://github.com/dartdavros/MarkMello/issues/48) requires Mermaid
`direction` statements inside subgraphs. Naiad 0.1.2 cannot parse them.

MarkMello builds Naiad 1.5.2 plus upstream's flowchart direction fix from the public
[MarkMello fork](https://github.com/dartdavros/Naiad/tree/codex/subgraph-directions-net10),
checked out at a fixed commit by the `external/Naiad` git submodule. The fork
starts at upstream commit `f9c2e4c9d8a074535007e9c289e0e705f8e2a39b` and keeps
`SequenceParser` public for the existing native text-measured sequence layout.
It also expresses the preview-only internal `closed Paint` hierarchy as an abstract class
so both projects build with .NET SDK 10. No diagram algorithms are replaced.
The strong-name key resolves relative to the source project, so a host solution
does not redirect signing to a nonexistent key in MarkMello's root. Naiad is
listed in the solution to keep its configuration aligned with Release builds.

The selected upstream commit retains Pidgin 3.5.1, already used by MarkMello.
Newer Naiad versions use Parlot; Parlot 2.3.1 failed Native AOT validation with
IL2075 in `SeekGuard.SkippedParser` and IL2090 in
`Numbers.TryParseDelegate<TNumber>.CreateWithStyles`. This integration therefore
keeps the existing parser dependency and AOT warning policy.

## Checkout and build

```sh
git submodule update --init --recursive
dotnet build ./src/MarkMello.Desktop/MarkMello.Desktop.csproj -c Release -m:1
dotnet test ./tests/MarkMello.Presentation.Tests/MarkMello.Presentation.Tests.csproj -c Release -m:1 --filter 'FullyQualifiedName~Mermaid|FullyQualifiedName~Sequence|FullyQualifiedName~Svg'
```

CI checkouts include submodules. Updating the dependency requires reviewing
and committing the new submodule SHA; it does not follow the fork branch at
build or application runtime. Rendering stays entirely in process and offline.

## Source license and packaging

The fork's `license.txt` licenses the sources under MIT. Upstream's
`OsmfEula.txt`, section 4, explicitly permits source compilation and
redistribution under that license. This integration builds the source project;
it does not consume or publish upstream binary NuGet packages. Consequently,
the fork disables NuGet packing and omits the SponsorCheck publishing tool,
which otherwise downloads the publisher's sponsor list during builds.
The upstream copyright and MIT notice accompany the application, along with
the MIT notice for the runtime parser dependency Pidgin 3.5.1.

The owner confirmed MarkMello has no commercial revenue and qualifies for the
upstream OpenSource binary-package exemption. It is not needed by this source
integration. Reassess the binary-package terms before returning to NuGet.

## Mermaid direction semantics

Local `TB`, `TD`, `BT`, `LR` and `RL` directions and nested subgraphs are
supported. As documented by
[Mermaid](https://mermaid.js.org/syntax/flowchart.html#direction-in-subgraphs),
an external connection to an individual inner node makes that subgraph inherit
its parent's direction. Connections to the subgraph itself preserve its local
direction. The issue #48 example contains individual cross-subgraph links;
its source, all labels and class colors are covered by a native SVG-reader test.

## Validation (2026-11-13)

- 78 targeted presentation tests passed, including all nine subgraph direction cases.
- The Release application compiled and published as Native AOT for Windows x64.
- A standalone Native AOT executable rendered the exact issue #48 source with
  every label, plus representative sequence, class and state diagrams. Its
  entry point checked that dynamic code was unavailable before rendering.
- The existing Pidgin `IL3053` warning policy is unchanged; no warning suppression
  was added to the application. The native renderer was exercised at runtime.

The local SDK 10.0.300 bundles an outdated AOT compiler, so validation used the
serviced compiler/runtime explicitly:

```sh
dotnet publish ./src/MarkMello.Desktop/MarkMello.Desktop.csproj -c Release -r win-x64 --self-contained true -m:1 -p:PublishAot=true -p:PublishSingleFile=false -p:RuntimeFrameworkVersion=10.0.14 -p:IlcPackageVersion=10.0.14 -o publish/issue48-win-x64
```

No application windows were opened or controlled for this verification.
