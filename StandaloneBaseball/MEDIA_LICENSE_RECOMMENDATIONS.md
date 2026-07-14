# Public Media Replacement Recommendations

This file separates packaging policy from attribution. It is not legal advice and
does not grant rights to any asset. The detailed file inventory is in
`THIRD_PARTY_ATTRIBUTIONS.md`.

## Current Release Policy

- **Local-only Version 2.0** retains every current file unchanged for use on the
  owner's computer. The local publish script verifies every asset by SHA-256.
- **Public Version 1.0** excludes all packaged audio, photographs, artwork, venue
  images, and other bitmap/video media whose redistribution rights are not stored
  with the project.
- Public publishing keeps `schools.csv`, the replay template, documentation, and
  the executable. Missing media uses code-rendered visuals and silence.
- An asset may move into the public package only after its exact file has a stored
  source record and written license or permission covering redistribution inside
  a downloadable Windows game.

## Recommendations And Basic Fallbacks

| Asset group | Public-release recommendation | Current basic fallback |
| --- | --- | --- |
| Steppenwolf, Alabama, Europe, *Game of Thrones*, *The Good, the Bad and the Ugly*, *Jeopardy!*, Tecmo, and other commercial music/themes | Obtain written synchronization/master-use and distribution permission covering the exact recording, or commission original replacement music. | Silence; game timing and transitions continue. |
| RBI/Tecmo-style announcer calls and game-derived audio | Obtain permission from the recording/game rights holder, or record original calls with a performer release assigning game distribution rights. | Silence; the visual play result remains available. |
| Swooshes and generic effects with creator-like filenames | Re-download from the exact official source page and retain the source URL, download record, license text/certificate, creator, date, and original filename. If the exact source cannot be proven, replace it. | Silence. |
| Stadium and field photographs | Obtain a license from the photographer/rightsholder for software redistribution and verify any separately protected logos or artwork, or replace with an original generic field rendering. | The gameplay renderer draws its generic baseball field. |
| Launch, loading, menu, lineup, All-Star, flag, scoreboard, championship badge, and stadium artwork | Store a signed ownership declaration or artist license for the exact file, including commercial redistribution and modification rights, or create original replacement artwork with documented provenance. | Code-rendered launch, loading, and menu screens; text/initials replace unavailable logos; generic presentation panels replace images. |
| Team, school, venue, and league trademarks visible in media | Obtain any permission needed for the intended branding use or remove/replace the marks. | Generic team text and user-supplied local assets. |

## Online Source Research Result

Exact-name searches for the supplied `makoto-ae`, `universfield`, and
`dragon-studio` swoosh files and the supplied Freepik-style stadium image did not
produce a verifiable exact source page. A general stock-site license cannot be
reliably attached to a particular local file without its source/download record.
Those files therefore remain local-only.

Pixabay's current license summary allows many uses in a larger creative work but
also requires users to consider third-party rights and prohibited uses. Its FAQ
recommends retaining the download link, filename, and license as proof. See:

- https://pixabay.com/service/license-summary/
- https://pixabay.com/service/faq/

The U.S. Copyright Office notes that ownership of a physical/digital copy is
different from ownership of the copyrighted work and that unauthorized uploading
or downloading can implicate reproduction and distribution rights. See:

- https://www.copyright.gov/help/faq/faq-fairuse.html

## Evidence To Retain For A Future Public Asset

1. Exact packaged relative path and SHA-256 hash.
2. Work title, creator, rights holder, and source URL.
3. License name and full license text or signed permission.
4. Download/purchase date and receipt or license certificate.
5. Scope confirming commercial software redistribution and modification rights.
6. Required attribution wording and any trademark, model, or property releases.
