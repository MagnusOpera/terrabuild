# Docusaurus Migration

Terrabuild public documentation is being moved from `../terrabuild.io` into this repository.

## Goals

- Keep Terrabuild as the single source of truth for code and public documentation.
- Preserve the current `terrabuild.io` structure and visual style as closely as practical.
- Add stable release documentation versioning using Docusaurus.
- Keep authoring in Markdown for docs, blog, and non-versioned product pages.

## Site Layout

- `website/docusaurus.config.ts`, `website/package.json`, `website/sidebars.ts` live under the site folder.
- `website/site-docs/` contains the public documentation routed under `/docs`.
- `website/blog/` contains the migrated blog posts.
- `website/pages/` contains non-versioned editorial pages such as `Why Terrabuild`.
- `website/static/` contains site assets migrated from the Hugo site.
- Existing repository-local architecture notes remain under `docs/`.

`website/site-docs/` is used instead of `docs/` to avoid colliding with the existing internal documentation folder.

## Versioning Policy

- Stable release tags `X.Y.Z` create Docusaurus docs snapshots.
- Preview tags `X.Y.Z-next` do not create version snapshots.
- The working documentation is exposed as `Next`.
- Both product documentation and generated extension documentation are versioned together for stable releases.

## Content Migration

The initial migration path is script-driven:

- `cd website && npm run docs:sync` copies content from `../terrabuild.io`.
- Hugo `_index.md` files are normalized to `index.md`.
- Common Hugo shortcodes are downgraded to Docusaurus-compatible Markdown where possible.
- Extension reference output will move from `../terrabuild.io/content/docs/extensions` to `website/site-docs/extensions`.

The sync script is intentionally conservative and is expected to be refined as visual parity work continues.

## Release and Publishing

- `make release-prepare version=X.Y.Z` should refresh generated docs, snapshot docs version `X.Y.Z`, then commit and tag.
- `make release-prepare version=X.Y.Z-next` should not create a docs snapshot.
- Website publication happens from GitHub Pages when a GitHub release is published.

## Open Work

- Recreate the Hugo/Hextra layout with near pixel-match fidelity in Docusaurus.
- Complete shortcode-to-MDX conversions where plain Markdown fallbacks are not sufficient.
- Add CI validation for website build.
- Add a `CNAME` file if the production deployment keeps using `terrabuild.io`.
