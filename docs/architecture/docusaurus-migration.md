# Docusaurus Migration

Terrabuild public documentation is being moved from `../terrabuild.io` into this repository.

## Goals

- Keep Terrabuild as the single source of truth for code and public documentation.
- Preserve the current `terrabuild.io` structure and visual style as closely as practical.
- Publish the latest documentation from `main` independently of application releases.
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

- The working documentation from `main` is exposed as `Latest`.
- Existing Docusaurus snapshots remain available as historical documentation.
- Application releases do not create new documentation snapshots.
- Product documentation and generated extension documentation are published together.

## Content Migration

The initial migration path is script-driven:

- `cd website && npm run docs:sync` copies content from `../terrabuild.io`.
- Hugo `_index.md` files are normalized to `index.md`.
- Common Hugo shortcodes are downgraded to Docusaurus-compatible Markdown where possible.
- Extension reference output will move from `../terrabuild.io/content/docs/extensions` to `website/site-docs/extensions`.

The sync script is intentionally conservative and is expected to be refined as visual parity work continues.

## Release and Publishing

- Application release preparation updates the changelog and creates a tag without preparing or publishing the website.
- Release preparation generates `What's New` by aggregating all same-family stable siblings for a stable target or all same-family preview siblings for a `-next` target, retaining each revision as a subtitle.
- Pushing a `website-*.*.*` tag runs the `Publish Website` workflow from that exact tagged commit, regenerates extension documentation and a rolling `Next` page from the latest preview family plus `Unreleased`, builds the website, and deploys it to GitHub Pages; the tag has no relationship to the Terrabuild application version.
- The workflow can still be run manually to publish `main` when an unversioned deployment is needed.
- Website tags are excluded from the application release workflow.
- Generated documentation from the publishing workflow is never committed back to `main`.

## Open Work

- Recreate the Hugo/Hextra layout with near pixel-match fidelity in Docusaurus.
- Complete shortcode-to-MDX conversions where plain Markdown fallbacks are not sufficient.
- Add CI validation for website build.
- Add a `CNAME` file if the production deployment keeps using `terrabuild.io`.
