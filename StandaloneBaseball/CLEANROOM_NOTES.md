# Dan's RBI Baseball 2026 Clean-Room Notes

This project is a new baseball program, not a ROM decompilation.

The ROM can be used as an optional local data source through `RomSnapshotImporter`.
The importer reads a user's local ROM and converts team/player tables into this
program's JSON model. The standalone program does not embed ROM code, ROM CHR
graphics, copyrighted game screens, or byte-for-byte assets.

Constraints intentionally removed from the NES ROM model:

- No 30-team limit.
- No 6-character player-name limit.
- No fixed team-name field widths.
- No 64-color NES palette limit.
- No 3-color sprite limit.
- No fixed 12-batter/4-pitcher roster structure.
- No requirement to write back into a `.nes` file.

Current ROM observations from `D:/_Downloads/R.B.I. Baseball 2025.nes`:

- iNES file, mapper 4.
- 98,320 bytes total.
- 64 KB PRG and 32 KB CHR.
- 30 team-name fields.
- 480 player records: 360 batter records and 120 pitcher records.
- Player-name bytes decode cleanly with the editor's known text codec.
