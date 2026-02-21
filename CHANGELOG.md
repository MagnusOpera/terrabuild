# Changelog

All notable changes to Terrabuild are documented in this file.

## [Unreleased]

- Upgrade Terrabuild FScript runtime/language to `0.52.0`.
- Exclude `<workspace-root>/.git/**` from FScript extension filesystem extern access.
- Adopt FScript `String.*` helpers in built-in extension scripts where behavior is preserved.

## [0.189.7-next]


- Upgrade Terrabuild FScript runtime/language to `0.50.0`.
- Align extension script/docs literal formatting to FScript compact multiline compatibility rules.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.189.6-next...0.189.7-next

## [0.189.6-next]


- Upgrade Terrabuild FScript runtime/language to `0.41.0`.
- Make extension batch support dynamic per command by requiring command handlers to return `{ Batchable; Operations }`.
- Remove static `Batchable` descriptor/attribute semantics and update built-in extensions, protocol docs, and scripting tests accordingly.
- Document and align extension protocol/type definitions on compact multiline record indentation style.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.189.5-next...0.189.6-next

## [0.189.5-next]


- Upgrade Terrabuild FScript runtime to 0.40.0 and initialize `Env` (`ScriptName`, `Arguments`) when loading `.fss` extension scripts.
- Fix FScript `Env` prelude injection to preserve leading `import` directives in embedded scripts.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.189.4-next...0.189.5-next

## [0.189.4-next]

- Create annotated release tags in `release-prepare` so `git push --follow-tags` pushes releases
- Update contributor workflow docs and add local architecture docs index
- Add a usage skill guide for day-to-day Terrabuild workflows

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.189.3-next...0.189.4-next

## [0.189.3-next]

- Rename release helper target to `make release-prepare` to avoid implying remote push/publish
- Revert entitlements changes.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.189.2-next...0.189.3-next

## [0.189.2-next]


- Initialize changelog-driven draft release notes for tag workflows
- Load built-in extension scripts from embedded resources only and remove filesystem fallback/copy
- Fix embedded extension loading to preserve workspace host context in smoke-test scenarios
- Add `make release-prepare` to automate changelog versioning, compare link, commit, and local tag creation

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.189.1-next...0.189.2-next

## [0.189.1-next]

## What's Changed
* feat: fix embedded extension location with AOT by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/384


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.189.0-next...0.189.1-next

## [0.189.0-next]

## What's Changed
* Update extension descriptions and defaults
* Migrate extensions to FScript and remove legacy extension projects by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/378
* feat: update entitlements by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/377

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.188.29...0.189.0-next

## [0.188.29]

## What's Changed
* fix: ensure PubSub does not deadlock by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/375
* feat: add unit tests for reentrant get by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/376


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.188.27...0.188.29

## [0.188.27]

## What's Changed
* feat: add web ui with graph and terminal by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/348
* feat: embed web application into Terrabuild exe by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/349
* feat: add notifications by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/350
* feat: log to file & add crtl-c message for user by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/351
* feat: eagerly abort build on error by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/352
* fat: mute terminal operations and preserve cursor by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/353
* feat: add build info panel by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/354
* feat: add build params by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/355
* feat: cleanup ui implementation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/356
* feat: edges highlights on drag by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/357
* feat: default auto theme by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/358
* feat: display log date and simplify log by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/359
* feat: add log/debug option on web app by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/360
* fix: force build must be enforced by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/361
* feat: left to right layout by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/362
* feat: add exit code and ended at in title by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/363
* feat: better sidebar by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/364
* feat: use project name and change shape of nodes by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/365
* feat: check workspace before running graph by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/366
* feat: add edges selection by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/367
* feat: rename graph command as console by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/368
* feat: add random port for the web app by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/369
* feat: do not show build log on node select by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/370
* feat: toggle build log by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/371
* feat: better node selection handling by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/372
* feat: collapse/expand terminal by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/373
* fix: incomplete root node shall lead to build error by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/374


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.187.1...0.188.27

## [0.188.27-next]

## What's Changed
* fix: incomplete root node shall lead to build error by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/374


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.188.26-next...0.188.27-next

## [0.188.26-next]

## What's Changed
* feat: collapse/expand terminal by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/373


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.188.25-next...0.188.26-next

## [0.188.25-next]

## What's Changed
* feat: better node selection handling by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/372


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.188.24-next...0.188.25-next

## [0.187.1]

## What's Changed
* fat: rename all batch strategy as single by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/347


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.187.0...0.187.1

## [0.187.0]

## What's Changed
* feat: add cpus on extensions by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/346


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.186.5...0.187.0

## [0.186.5]

## What's Changed
* fix: remove previous config if any by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/345


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.186.4...0.186.5

## [0.186.4]

## What's Changed
* fix: rendering issue by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/344


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.186.3...0.186.4

## [0.186.3]

## What's Changed
* feat: simplify sign and release workflow by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/335
* feat: update self build by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/337
* feat: merge pr and push workflows by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/339
* feat: implement new obvious dependency management by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/341
* feat: do not upload outputs if descriptor is empty by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/342
* feat: enable SxS install by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/343


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.185.18...0.186.3

## [0.185.19]

Changes:
* allow side by side install

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.185.18...0.185.19

## [0.185.18]

## What's Changed
* feat: add fast path for required nodes by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/334


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.185.17...0.185.18

## [0.185.17]

## What's Changed
* feat: skip non required nodes in Runner by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/333


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.185.16...0.185.17

## [0.185.16]

## What's Changed
* feat: delegate restore to cascade by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/332


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.185.15...0.185.16

## [0.185.15]

## What's Changed
* fix: restore external shall not be required by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/331


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.185.14...0.185.15

## [0.185.14]

## What's Changed
* feat: add more relaxation rules by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/330


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.185.13...0.185.14

## [0.185.13]

## What's Changed
* feat: proper batch computation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/314
* feat: configurable batch mode by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/315
* feat: remove extension/batch and extend target/batch meaning by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/316
* feat: implement cascade to compute exact node requirements by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/317
* feat: add support for outputs in WORKSPACE's target by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/318
* fix: correct requirement computation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/319
* feat: add support for lazy targets by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/320
* feat: run whole cluster if exec node found by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/321
* chore: use Generic collections for better perfs by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/322
* fix: invalid task name for job by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/323
* feat: simplify hash computation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/324
* feat: faster pubsub by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/325
* feat: use Lock on Hub and logger by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/326
* fix: fix log + use serilog interpolation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/327
* chore: update logs by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/328
* fix: USER identification is fixed via $HOME by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/329


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.184.3...0.185.13

## [0.184.3]

Changes:
* 0.184.3 is same as 0.184.1 and reverts changes in 0.184.2 as this break rebuild idempotency assumptions.

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.184.2...0.184.3

## [0.184.2]

## What's Changed
* fix: parent must rebuild if child has changed by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/313


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.184.1...0.184.2

## [0.184.1]

## What's Changed
* feat: add support for env block for extension by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/311
* feat: version is only checked on release builds by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/312


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.183.5...0.184.1

## [0.183.5]

## What's Changed
* feat: differentiated subscribe by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/310


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.183.4...0.183.5

## [0.183.4]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.183.3...0.183.4

## [0.183.3]

## What's Changed
* feat: download logs if log is enabled by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/309


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.183.2...0.183.3

## [0.183.2]

## What's Changed
* feat: encrypt artifacts before storing them server side by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/308


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.182.6...0.183.2

## [0.182.5]

## What's Changed
* chore: silent sentry errors as they are useless by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/306


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.182.4...0.182.5

## [0.182.4]

## What's Changed
* feat: extension owned project identifier by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/303
* chore: unify npm package management by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/304
* feat: adjust error level on sentry upload for local build by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/305


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.181.4...0.182.4

## [0.181.4]

## What's Changed
* fix: enforce tb var prefix by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/302


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.181.3...0.181.4

## [0.181.3]

## What's Changed
* feat: variable names are case insensitive by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/301


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.181.2...0.181.3

## [0.181.2]

## What's Changed
* feat: download external artifacts summary by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/300


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.181.1...0.181.2

## [0.181.1]

## What's Changed
* feat: environments pattern matching by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/299


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.181.0...0.181.1

## [0.181.0]

## What's Changed
* feat: add environments filter on projects by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/298


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.180.1...0.181.0

## [0.180.1]

## What's Changed
* fix: always add --link-workspace-packages for pnpm install by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/297


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.180.0...0.180.1

## [0.180.0]

## What's Changed
* feat: add support for optional comma in list and map by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/296


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.179.1...0.180.0

## [0.179.1]

## What's Changed
* chore: align code with syntax by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/294
* fix: single project must not specify --recursive by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/295


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.179.0...0.179.1

## [0.179.0]

## What's Changed
* feat: rename rebuild as build by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/293


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.178.6...0.179.0

## [0.178.6]

## What's Changed
* feat: .net 10 by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/285
* feat: add pnpm support by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/286
* fix: debug mode must log by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/288
* feat: check docs can be generated by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/289
* fix: cleanup up lock files mess by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/290
* feat: update extension docs by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/291
* feat: invert lockfile flag by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/292


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.177.3...0.178.6

## [0.177.3]

## What's Changed
* feat: configuration engine in workspace by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/275
* feat: update api for bulk insertions by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/276
* feat: sentry cli extension by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/278
* feat: add support for max error level for commands by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/279
* feat: port sentry extension from 0.176 by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/280
* feat: update single artifact to keep api compatibility by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/281
* feat: force summary download of failed restorable nodes by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/282
* feat: rename build-graph as node-graph by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/283
* feat: rename container as image by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/284


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.10...0.177.3

## [0.176.18]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.17...0.176.18

## [0.176.17]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.16...0.176.17

## [0.176.16]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.15...0.176.16

## [0.176.14]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.13...0.176.14

## [0.176.13]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.12...0.176.13

## [0.176.12]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.11...0.176.12

## [0.176.11]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.176.10...0.176.11

## [0.176.10]

## What's Changed
* feat: add support for external cache policy by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/263
* feat: skip ignored nodes by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/264
* feat: align use remote by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/265
* feat: unify use remote computation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/266
* feat: option to disable SSL cert validation for terrabuild api by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/267
* feat: add back hub to compute node action by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/268
* feat: implement rebuild strategy by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/269
* feat: implement cascade cluster build by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/270
* feat: cascaded targets are locals by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/271
* feat: faster action evaluation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/272
* feat: add enum syntax ~<identifier> by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/273
* feat: rename cache as artifacts by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/274


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.175.3...0.176.10

## [0.175.3]

## What's Changed
* feat: prioritize background task over normal task by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/259
* feat: track and kill processes if required by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/260
* fix: exec error must fail task by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/261
* feat: ensure errors are correctly reported on build by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/262
* feat: better looking groups

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.174.13...0.175.3

## [0.174.14]

Changes
* fix: exec error must fail task (#261)

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.174.13...0.174.14


## [0.174.13]

## What's Changed
* fix: invalid up to date computation by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/258


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.174.12...0.174.13

## [0.174.12]

## What's Changed
* feat: batch build by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/246
* feat: batch docs by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/247
* feat: batch node status by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/248
* feat: humanize build duration by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/249
* feat: delay progress display for parent tasks by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/250
* feat: abbreviated humanized timespan by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/251
* feat: atomic batch progress by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/252
* feat: run io tasks as background continuations by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/253
* feat: dotnet publish as batchable command by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/254
* feat: remove IsLeaf on Node by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/255
* feat: simplify version check by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/256
* feat: stop scheduling tasks on first error by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/257


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.173.14...0.174.12

## [0.173.14]

## What's Changed
* feat: default labels by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/235
* feat: add locked mode support by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/236
* feat: implement filter by project types by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/238
* feat: fix project selection by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/239
* feat: more configuration log by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/240
* feat: rewrite builder by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/237
* feat: use gitignore to determine project contents by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/241
* feat: configure terminal autoflush by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/242
* feat: fix timer + rendering by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/243
* feat: use LibGit2Sharp for better performance and correct file matching by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/244
* feat: restore node but not its dependencies by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/245


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.172.0...0.173.14

## [0.172.0]

## What's Changed
* feat: use sync updates by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/234


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.171.0...0.172.0

## [0.171.0]

## What's Changed
* feat: generate mermaid as markdown by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/230


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.170.2...0.171.0

## [0.170.2]

* feat: update icons on graph

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.170.1...0.170.2

## [0.170.1]

* fix: add status nodes on graph

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.170.0...0.170.1

## [0.170.0]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.169.0...0.170.0

## [0.169.0]

## What's Changed
* feat: cleanup graph implementation + phony targets by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/232


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.168.1...0.169.0

## [0.168.1]

## What's Changed
* fix: restore shall not install dependencies by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/231


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.168.0...0.168.1

## [0.168.0]

## What's Changed
* feat: continuation based build by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/229


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.167.1...0.168.0

## [0.167.1]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.167.0...0.167.1

## [0.167.0]

## What's Changed
* feat: optimize restore idempotent targets by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/228


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.166.0...0.167.0

## [0.166.0]

## What's Changed
* feat: idempotent target by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/227


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.165.0...0.166.0

## [0.165.0]

## What's Changed
* feat: scaffold with attributes by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/226


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.164.0...0.165.0

## [0.164.0]

## What's Changed
* feat: deferred targets by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/225


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.163.2...0.164.0

## [0.163.2]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.163.1...0.163.2

## [0.163.1]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.163.0...0.163.1

## [0.163.0]

## What's Changed
* feat: add support for workspace target cache by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/224


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.162.5...0.163.0

## [0.162.5]

feat: add dotnet tool support

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.162.4...0.162.5

## [0.162.4]

fix: use `terraform destroy` instead of `terraform apply -destroy`

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.162.3...0.162.4

## [0.162.3]

fix: ephemeral tasks shall not discard outputs

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.162.2...0.162.3

## [0.162.2]

fix: remove --shm-size and --init as this generates more problems than it solves

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.162.1...0.162.2

## [0.162.1]

fix: remove --ipc=host since it's not compatible with shared memory settings

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.162.0...0.162.1

## [0.162.0]

## What's Changed
* feat: use init for containers by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/217
* feat: increase shared memory by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/219
* feat: playwright support by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/218
* feat: update extensions parameters by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/220
* feat: cacheability is implemented as attribute on extensions by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/221
* fix: upload iif target is remote cacheable by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/222
* feat: update docs by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/223


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.161.0...0.162.0

## [0.161.0]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.160.0...0.161.0

## [0.160.0]

## What's Changed
* feat: dotnet restore only current project by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/216


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.159.5...0.160.0

## [0.159.5]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.159.4...0.159.5

## [0.159.4]

**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.159.3...0.159.4

## [0.159.3]

## What's Changed
* feat: parallel downloads by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/215


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.158.0...0.159.3

## [0.158.0]

## What's Changed
* feat: add version check by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/214


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.157.2...0.158.0

## [0.157.1]

## What's Changed
* feat: enhance exit code handling by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/212


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.157.0...0.157.1

## [0.155.0]

## What's Changed
* fix: dotnet test is cacheable by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/206
* fix: terraform plan is unmanaged by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/207
* feat: update retry logic for unmanaged task by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/208


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.154.0...0.155.0

## [0.154.0]

## What's Changed
* feat: use only snake_case identifiers by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/204
* feat: numbers are int only by @pchalamet in https://github.com/MagnusOpera/terrabuild/pull/205


**Full Changelog**: https://github.com/MagnusOpera/terrabuild/compare/0.153.0...0.154.0

## [0.153.0]

## What's Changed
* fix: invalid hash computation on automatic dependencies by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/203


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.152.2...0.153.0

## [0.152.2]

## What's Changed
* fix: mermaid graph shall not have extra nodes by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/202


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.152.1...0.152.2

## [0.152.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.152.0...0.152.1

## [0.152.0]

feat: PROJECT and WORKSPACE are now part of hash

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.151.2...0.152.0

## [0.151.2]

fix: restore implies both managed and outputs

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.151.1...0.151.2

## [0.151.1]

fix: restore force managed

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.151.0...0.151.1

## [0.151.0]

## What's Changed
* feat: force restore flag by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/201


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.150.0...0.151.0

## [0.150.0]

## What's Changed
* Fix: filename in hash by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/200


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.149.0...0.150.0

## [0.149.0]

## What's Changed
* feat: add support for npx

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.148.3...0.149.0

## [0.148.2]

fix: terraform requires variables for destroy 🤷‍♂️

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.148.1...0.148.2

## [0.148.1]

## What's Changed
* fix: enforce truthy eval for boolean operation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/199


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.147.1...0.148.1

## [0.147.0]

## What's Changed
* fix: boolean operators shall be && and || by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/198


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.146.0...0.147.0

## [0.146.0]

## What's Changed
* feat: implement ~= operator (regex match) by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/197


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.145.0...0.146.0

## [0.145.0]

## What's Changed
* feat: option to create terraform workspace on plan if it does not exist by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/195
* feat: implement terraform destroy by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/196


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.144.2...0.145.0

## [0.144.2]

## What's Changed
* fix: shared files shall not crash on hash computation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/194


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.144.0...0.144.2

## [0.144.0]

## What's Changed
* feat: update configuration listing by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/193


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.143.5...0.144.0

## [0.143.5]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.143.4...0.143.5

## [0.143.4]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.143.3...0.143.4

## [0.143.3]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.143.2...0.143.3

## [0.143.2]

## What's Changed
* feat: expand $TERRABUILD_HOME for containers by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/192


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.143.1...0.143.2

## [0.143.1]

## What's Changed
* feat: use regex for describing container vars by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/191


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.143.0...0.143.1

## [0.143.0]

## What's Changed
* feat: support for TERRABUILD_CREDENTIALS by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/190


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.142.2...0.143.0

## [0.142.2]

## What's Changed
* feat: additionnal props for openapi gen by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/189


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.142.1...0.142.2

## [0.142.1]

## What's Changed
* feat: update settings emoji 🦄 by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/188


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.142.0...0.142.1

## [0.142.0]

## What's Changed
* feat: ability to filter by project id by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/187


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.141.3...0.142.0

## [0.141.3]

feat: update build graph emoji 👀
**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.141.2...0.141.3

## [0.141.2]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.141.1...0.141.2

## [0.141.0]

## What's Changed
* feat: change pattern npm & yarn + remove support for inner npm module by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/186


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.140.2...0.141.0

## [0.140.3]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.140.1...0.140.3

## [0.140.1]

## What's Changed
* feat: add colors on mermaid graph by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/185


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.140.0...0.140.1

## [0.140.0]

## What's Changed
* feat: array access by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/184


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.139.9...0.140.0

## [0.139.9]

## What's Changed
* upgrade to .net 9.0.300 by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/177
* feat: managed artifacts by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/179
* feat: add support for managed at workspace level by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/180
* fix: rebuild not propagated by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/181
* chore: rework node scheduling by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/182
* fix: if task is older than children then rebuild by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/183


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.138.0...0.139.9

## [0.138.0]

## What's Changed
* add terrabuild.environment by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/175


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.137.2...0.138.0

## [0.137.2]

Fix labels output.
**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.137.1...0.137.2

## [0.137.1]

## What's Changed
* bold target on mermaid graph by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/174


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.137.0...0.137.1

## [0.137.0]

## What's Changed
* expose arch and os by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/173


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.136.2...0.137.0

## [0.136.2]

## What's Changed
* better console progress by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/171
* remove unscheduled nodes by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/172


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.136.1...0.136.2

## [0.136.1]

## What's Changed
* remove version by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/170


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.136.0...0.136.1

## [0.136.0]

## What's Changed
* rename for consistency with project.id.version by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/169


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.135.26...0.136.0

## [0.135.26]

## What's Changed
* fix success computation when filter is used by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/168


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.135.25...0.135.26

## [0.135.25]

## What's Changed
* configuration is a collection of attributes now by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/133
* use block syntax for defaults by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/134
* HCL Parser + syntax change by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/135
* fix invalid optimization for nop nodes by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/137
* add support for homebrew alpha/beta formulae by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/138
* Use env variable content to detect configuration changes by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/139
* chore: merge env vars by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/140
* Enable F# Nullable Reference Types by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/141
* split publish targets by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/142
* fix release config by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/143
* filter out auth error by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/144
* do not get variable values as this can lead to graph reevaluation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/145
* no need for duration - just diff end and start by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/146
* standalone lang parser by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/147
* fix project dependencies by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/148
* terraform force reconfigure by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/149
* fix project dependencies when using depends_on by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/150
* cleanup nunit by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/151
* code cleanup by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/152
* add * and / by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/153
* remove useless function, use string interpolation instead by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/154
* Add tests for lang and transpiler by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/155
* remove target. prefix by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/156
* add test cases for transpiler errors by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/157
* update brew template: rename terrabuild-beta as terrabuild-next by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/158
* fix docs by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/159
* update scaffold to latest syntax by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/160
* use lowercase for project unique id by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/161
* use falsy behavior for ? and ?? by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/162
* configuration is optional by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/163
* more falsy behavior on eval by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/164
* move initializer to project block by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/165
* enable multiple initializers by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/166
* update error msg by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/167


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.134.0...0.135.25

## [0.134.0]

## What's Changed
* add logs by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/128
* remove // comments by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/129
* string interpolation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/130
* Add project locals by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/131
* upgrade to .net 9.0.202 by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/132


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.130.0...0.134.0

## [0.130.0]

## Breaking changes
* `--logs` is no more, use `--log` instead

## What's Changed
* log is no more linked to debug by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/127


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.129.2...0.130.0

## [0.129.2]

## What's Changed
* Pending tasks must not raise error in build

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.129.1...0.129.2

## [0.129.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.129.0...0.129.1

## [0.129.0]

## What's Changed
* add support for github actions commands by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/126


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.128.0...0.129.0

## [0.128.0]

## What's Changed
* Add exception area by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/121
* rename tests by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/122
* allow overriding sentry dsn by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/123
* better reporting for pubsub by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/124
* better cli handler by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/125


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.127.0...0.128.0

## [0.127.0]

## What's Changed
* upgrade to .net 9.0.201 by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/118
* rename run-smoke-tests as smoke-tests by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/119
* add case insensitive scan by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/120


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.126.4...0.127.0

## [0.126.4]

## What's Changed
* add sentry to collect exception by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/117


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.125.2...0.126.4

## [0.126.2]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.126.1...0.126.2

## [0.126.0]

## What's Changed
* add sentry to collect exception by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/117


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.125.2...0.126.0

## [0.125.2]

## What's Changed
* Handle GitHub event by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/115
* add timestamp by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/116


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.124.1...0.125.2

## [0.124.1]

* fix crash on detached hea
* fix commit email

** Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.124.0...0.124.1

## [0.124.0]

## What's Changed
* collect commit & subject by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/114


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.123.2...0.124.0

## [0.123.2]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.123.1...0.123.2

## [0.123.1]

## What's Changed
* get commit log by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/112
* qualify tag by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/113


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.122.1...0.123.1

## [0.122.1]

## What's Changed
* use string instead of guid for workspace Id by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/111


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.122.0...0.122.1

## [0.122.0]

## What's Changed
* change profile structure by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/110


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.121.3...0.122.0

## [0.121.3]

## What's Changed
* add config for Terraform init by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/108


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.121.2...0.121.3

## [0.121.2]

* fix start build api

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.121.1...0.121.2


## [0.121.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.121.0...0.121.1

* override variable (CLI or TB_VAR) for latest built context

## [0.121.0]

## What's Changed
* upgrade to .net 9.0.200 by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/103
* change api route by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/104
* Adapt for new API by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/106
* container platform support by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/107


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.120.8...0.121.0

## [0.120.8]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.120.7...0.120.8

* unify backstick identifier handling

## [0.120.6]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.120.5...0.120.6

* fix reserved identifier: use backquote around identifier

## [0.120.4]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.120.3...0.120.4

* fix variables override

## [0.120.3]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.120.2...0.120.3

* fix $ in strings
* npm _-dispatch__ shall not install packages

## [0.120.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.120.0...0.120.1

## [0.120.0]

## What's Changed
* yarn ignore engines by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/102
* allow dash in identifiers by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/101


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.119.0...0.120.0

## [0.119.0]

## What's Changed
* change terrabuild location injection in makefile by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/100


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.118.0...0.119.0

## [0.118.0]

## What's Changed
* track values not vars by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/99


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.117.4...0.118.0

## [0.117.4]

## What's Changed
* container attribute is an expression by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/98


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.117.3...0.117.4

## [0.117.3]

## What's Changed
* restored task must send status at build time - not restore time by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/97


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.117.2...0.117.3

## [0.117.2]

## What's Changed
* default container was not explicit - feature discarded by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/96


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.117.1...0.117.2

## [0.117.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.117.0...0.117.1

## What's Changed
* add open-api-generator extension by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/94
* Container override on extension and project by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/95


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.116.0...0.117.1

## [0.116.0]

## What's Changed
* Private dependencies as projects by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/92
* Chore: required qualified on ast by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/93


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.115.6...0.116.0

## [0.115.6]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.115.5...0.115.6

## [0.115.5]

## What's Changed
* add force to npm install by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/86
* silent null extension by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/87
* ignore subprojects on dependencies by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/88


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.115.4...0.115.5

## [0.115.4]

## What's Changed
* add unit test for scan folders by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/85


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.115.3...0.115.4

## [0.115.2]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.115.1...0.115.2

## [0.115.1]

## What's Changed
* do not forbid scripts if native - just this won't build by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/84


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.115.0...0.115.1

## [0.115.0]

## What's Changed
* disable scripts for native deployments by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/81
* scanning is ignoring ignored folders by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/83
* doc generator must use correct syntax for arguments by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/82


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.114.0...0.115.0

## [0.114.0]

## What's Changed
* integration test with custom script by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/80
* implement cache configuration at target level by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/78


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.113.0...0.114.0

## [0.113.0]

## What's Changed
* fix script loading by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/79


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.112.0...0.113.0

## [0.112.0]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.111.0...0.112.0

## [0.108.0]

## What's Changed
* Ability to ignores folders at workspace level by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/71
* .terrabuild is no more required by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/72


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.107.1...0.108.0

## [0.107.1]

Fix project scan issue when PROJECT and WORKSPACE are in the same sub-directory.

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.107.0...0.107.1

## [0.107.0]

## What's Changed
* Serve by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/69
* ignore sub workspace by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/70


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.106.0...0.107.0

## [0.106.0]

## What's Changed
* Prepare brew environment by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/67
* add & and | operator for booleans by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/68


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.104.0...0.106.0

## [0.104.0]

## What's Changed
* new functions: better format (using template) and tostring by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/66


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.102.1...0.104.0

## [0.102.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.102.0...0.102.1

## [0.102.0]

## What's Changed
* migrate to .net 9 by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/63
* Rework Docker interface by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/64
* Use kebab-case for arguments by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/65


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.100.4...0.102.0

## [0.100.4]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.100.3...0.100.4

## [0.100.3]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.100.2...0.100.3

## [0.100.2]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.100.1...0.100.2


## [0.100.1]

## What's Changed
* make some variables immediately available by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/62


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.100.0...0.100.1

## [0.100.0]

## What's Changed
* fix circular variable evaluation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/59
* add yarn support by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/60
* Eager evaluation to avoid circular dependencies by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/61


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.98.0...0.100.0

## What's Changed
* ignore WORKSPACE and PROJECT for hash computation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/55
* Support add operator for list and map by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/56
* add format support by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/57
* function expression use tuple syntax (comma separated) by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/58
* add support for multi-arch by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/54
* fix circular variable evaluation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/59
* add yarn support by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/60
* Eager evaluation to avoid circular dependencies by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/61


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.98.1...0.100.0

## [0.98.0]

## What's Changed
* Add Podman support by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/53


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.96.4...0.98.0

## [0.96.4]

Bug fix:
* ensure container info are considered in hash computation

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.96.3...0.96.4

## [0.96.3]

## What's Changed
* container not part of hash by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/52


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.96.2...0.96.3

## [0.96.2]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.96.1...0.96.2

## [0.96.1]

Changes:
* Add `!`(not) operator

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.96.0...0.96.1

## [0.96.0]

## What's Changed
* add terrabuild_debug var by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/47
* remove BranchOrTag in command context by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/48
* rename ProjectHash as Hash by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/49
* Add replace function by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/50
* Change accessors + add count function by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/51


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.94.1...0.96.0

## [0.94.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.94.0...0.94.1

## [0.94.0]

Changes:
* Rename Dynamic task as External task. Breaking change for custom extensions.

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.92.0...0.94.0

## [0.92.0]

## What's Changed
* Optimizer is officially removed. Unused artifacts are still not downloaded.
* No more universal binary for macOS: download correct version now (either arm64 or x64)
* State can be synchronized with external state (Terraform for example). See --checkstate and Extensions for dynamic operations
* `terrabuild_head_commit` variable is available in workflow now. It's discourage to use it anyway since this interferes with task caching

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.90.4...0.92.0

## [0.90.4]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.90.3...0.90.4

## [0.90.3]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.90.2...0.90.3

## [0.90.2]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.90.1...0.90.2

## [0.90.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.90.0...0.90.1

## [0.90.0]

## What's Changed
* Remove batch build support by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/43


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.88.1...0.90.0

## [0.88.1]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.88.0...0.88.1

## [0.88.0]

## What's Changed
* Notarize terrabuild by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/42


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.86.0...0.88.0

## [0.86.0]

## What's Changed
* Cleanup exec commands in build by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/32
* isolate consistency computation by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/33
* add metadata to CI by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/34
* rename api args by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/35
* cleanup GraphDef by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/36
* remove file size from artifact - compute server side instead by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/38
* rename field (breaking change) by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/39
* track artifact completion by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/40
* implement links (loose dependencies) for build optimizations by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/41


**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.84.5...0.86.0

## [0.84.5]

Changes:
* Fix npm run

## [0.84.4]

Changes:
* Add `@system`extension with `write` command
* Add run command to `@npm` extension
* Expose current project hash `$terrabuild_hash`


## [0.84.1]

Changes:
* Fix logs on build failure

## [0.84.0]

Graph:
- multi-steps graph construction
- multi-nodes targets
- decoupling graph processing from cache api
- optimizer can optimize multi-steps targets

Performance:
- only download what's required and nothing more (if not used, no download at all)

Reporting:
- node status on GitHub Summary
- better report of timings

## [0.82.0]

Changes:
* Better optimizer
* Build summary on GitHub Actions
* Stable
* I/O performance

See https://terrabuild.io/blog/beta/

## [0.81.15]

Changes:
* Add integration tests by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/27
* Run for real integration tests by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/28
* Fix ignores for rust projects

## [0.81.13]

Changes:
* fix: dotnet test must not rebuild

## [0.81.10]

Changes:
* add link on badge

## [0.81.9]

Changes:
* Enhance build reports

## [0.81.8]

Changes:
* Fix poor anchor management on github actions

## [0.81.7]

Changes:
* Shorten identifier on logs

## [0.81.6]

Changes:
* Remove randomness in structures to enforce testability

## [0.81.4]

Changes:
* Graph at top of report

## [0.81.3]

Changes:
* Title for build graph in logs

## [0.81.2]

Changes:
* Fix optimizer was ignoring batchability of current node

## [0.81.0]

Changes:
* New optimizer

## [0.79.1]

Changes:
* Back to LR graph

## [0.79.0]

Changes:
* add option to disable batch builds (--nobatch)

## [0.78.3]

Changes:
* Fix documentation

## [0.78.2]

Changes:
* simplify name on graph for clusters

## [0.78.1]

Changes:
* better looking graph for clusters

## [0.78.0]

Changes:
* dotnet test is batchable
* graph optimization can spawn new clusters
* better looking mermaid graph

## [0.77.2]

Changes:
* add colors on graph (from cluster info)

## [0.77.1]

Changes:
* revert name change after optimizer pass

## [0.77.0]

Changes:
* use nuget lock for building Terrabuild
* simplify dotnet extensions commands
* fix bug in optimizer removing dependencies after optimization

## [0.76.4]

Changes:
* revert --no-dependencies on bulk .net build

## [0.76.3]

Changes:
* revert --no-dependencies on dotnet build extension

## [0.76.2]

Changes:
* common home cache for containers

## [0.76.1]

Changes:
* throttle dotnet restore (--disable-parallel)
* dump proc count in logs

## [0.75.0]

Changes:
* Add annotations in GitHub for failed targets

## [0.74.9]

Changes:
* Reduce links size in markdown

## [0.74.8]

Changes:
* Remove post-xxx for batched nodes - better with markdown and logs

## [0.74.7]

Changes:
* Split total as cost/gain

## [0.74.6]

Changes:
* Segregate workflows on GitHub
* Total duration on markdown

## [0.74.4]

Changes:
* Add mermaid graph to markdown

## [0.74.3]

Changes:
* Fix duration in markdown

## [0.74.2]

Changes:
* Add target duration on markdown

## [0.74.1]

Changes:
* Fix anchors on github

## [0.74.0]

Changes:
* better markdown with summary

## [0.73.0]

Changes:
* Optimizer use forced flag to batch optimize tasks

## [0.72.0]

Changes:
* Add markdown output on GitHub

## [0.71.0]

Changes:
* add variables `terrabuild_retry` and `terrabuild_force`

## [0.70.1]

Changes:
* add `tag` on docker push

## [0.69.0]

Changes:
* add variable `terrabuild_note`

## [0.68.2]

Changes:
* fix target sources for force

## [0.68.1]

Changes:
* Fix forced nodes not being built

## [0.68.0]

Changes:
* force only apply to selection

## [0.67.0]

Changes:
* add logs

## [0.66.0]

Changes:
* Remove top level target shortcuts
* Report logs for all nodes leading to success or failure on build

## [0.64.0]

Fix:
* rebuild shall not track used variables

## [0.63.0]

Changes:
* Rename top level targets

## [0.62.0]

Changes:
* WhatIf is moved from top level to target


## [0.61.0]

Changes:
* add option to disable containers (--nocontainer)
* configuration default is always used as base configuration
* reenable home mapping on containers

## [0.59.0]

Changes:
* Remove home mapping on extensions

## [0.58.0]

Changes:
* Use stable hash computation across local and CI

## [0.57.0]

## What's Changed
* rebuild is an expression by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/12


## [0.56.0]

## What's Changed
* invalidate cache as required by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/11


## [0.55.0]

## What's Changed
* Fix tasks count by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/10


## [0.54.2]

Changes:
* Fix child task triggering parent task

## [0.54.0]

## What's Changed
* Use PubSub by @pchalamet in https://github.com/MagnusOpera/Terrabuild/pull/9


## [0.53.0]

Changes:
* rename `files` as `includes` in project

## [0.52.7]

Changes:
* Cache for logs
* Dump all build logs on error

## [0.52.6]

Changes:
* rename `.complete` file as `status` in cache

## [0.52.5]

Changes:
* enable cache summary only again on build end

## [0.52.4]

Changes:
* Fix invalid cache usage in build

## [0.52.3]

Changes:
* Fix required node scheduling

## [0.52.1]

Changes:
* Fix root node computation for trim
* dotnet extension now restores dependencies :-( 

## [0.50.2]

Changes:
* Fix non-triggered dependant nodes for non-required nodes

## [0.50.1]

Changes:
* Fix regression in cache init

## [0.50.0]

Changes:
* trim graph: build only required nodes

## [0.49.0]

Changes:
* report tag on insights

## [0.48.0]

Changes:
* Update reading configuration header

## [0.47.0]

Changes:
* `files`, `outputs`, `dependencies` and `ignores` specified on `project` block are now merged with configuration (from extension or default one)

## [0.46.0]

Changes:
* Ensure graph idempotency when dump logs
* Add terrabuild_tag (flag -t)

## [0.45.0]

Changes:
* Fix regression in graph computation

## [0.44.0]

Changes:
* Support for build note
* Better error reporting for invalid project dependencies
* Fix bug in graph when requested target does not exist

## [0.41.0]

Changes:
* remove useless environment switch

## [0.40.0]

Changes:
* enhance logs title

## [0.39.0]

Changes:
* add not equal operator (`!=`)
* add try get item (`.?[ ]`)


## [0.38.0]

Changes:
* Fix logs output for multiple actions on command

## [0.36.0]

Changes:
* add support for project file content (attribute files on project)
* display meta command on logging

## [0.34.0]

Changes:
* Add null-coalesce operator ??

## [0.32.0]

Changes:
* Add support for map and list
* Add item function (`.[ ]` syntax)


## [0.31.0]

Changes:
* allow passing environment variables to container
* cleanup notifications

## [0.30.3]

Changes:
* remove useless --localonly switch for logs

## [0.30.2]

Changes:
* Fix logs always dumped (even when not requested)
* Add support for terraform validate

## [0.30.0]

Changes:
* add logs command

## [0.20.3]

Changes:
* use `--net=host` for container builders
* override variables using environment variables - override must respect variable type
* add discovery of local dependency discovery for npm
* fix error reporting (was not not complete) also add more context information


## [0.18.14]

Changes:
* Terraform init does not attempt to -reconfigure anymore

## [0.18.13]

Fix:
* Deploy target shortcut was incorrect

## [0.18.12]

Fix:
* docker arguments could generate a new version

## [0.18.11]

Changes:
* Add docker platform support

## [0.18.9]

Changes:
* change loginspace route api

## [0.18.8]

Changes:
* Stricter rules for identifier parsing

## [0.18.7]

Changes:
* Use project hash instead of project name in cache entry

## [0.18.6]

Changes:
* Remove project name from hash to allow relocation

## [0.18.5]

Changes:
* Find workspace root if none provided

## [0.18.4]

Changes:
* Fix command line parsing when global switches were involved
* Fix unknown variables in hash computation
* Fix log in graph
* Log all fatal errors

## [0.18.1]

Changes:
* enable multiple auths so different spaces with different tokens can be used. NOTE: it's no more an error **not** to be authenticated in a space and still having a token for another space.
* default environment variables **must** exist now (was empty env before if not found).
* Add variables tracking on targets so this can safely impact project hash computation.


## [0.15.0]

Enhancement beta-test:
* Ability to connect to multiple spaces

## [0.14.1]

Fix for beta-version:
* Better error handling
* Restore shell cursor upon abnormal termination
* Terrabuild is available as a .net tool

## [0.11.0]

First alpha release !

Check [documentation](https://terrabuild.io) ! 

## [0.2.0]

**Full Changelog**: https://github.com/MagnusOpera/Terrabuild/compare/0.1.0...0.2.0

## [0.1.0]

First stable release with HCL syntax and graph optimization.
