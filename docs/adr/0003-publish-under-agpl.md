# Publish Office Buddy under the AGPL-3.0, keeping Ghostscript

The repository goes public and every build of `OfficeBuddy.exe` is published as a GitHub release. That distributes Ghostscript, which [ADR-0001](0001-ghostscript-for-compression.md) only allowed for use within the family. We keep Ghostscript and license all of Office Buddy under the GNU AGPL-3.0, the licence Ghostscript itself uses.

Ghostscript runs as a separate program, so Office Buddy might not have to be AGPL at all. But it is packed into the same exe, and that is a grey area. Since the code is public anyway, choosing the same licence removes the doubt at no cost.

What the AGPL asks of us when distributing Ghostscript unchanged:

- its licence text travels with it: `third_party/Ghostscript/COPYING`, next to the app at run time;
- its exact source is available: linked from [third_party/Ghostscript/README.md](../../third_party/Ghostscript/README.md) and from every release's notes.

## Considered Options

- **Another licence for our own code, relying on Ghostscript being a separate program.** Possible, but rests on the grey area above.
- **Replace Ghostscript by re-encoding images on top of PDFsharp**, as ADR-0001 suggested. Clean MIT licensing, but weeks of work.

## Consequences

Anyone may use, change and pass on Office Buddy, as long as what they pass on stays under the AGPL-3.0 with its source. Updating Ghostscript means updating the source link too.
