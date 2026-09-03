---
title: Pulse
description: Use Terrabuild and GitHub evidence to investigate engineering delivery trends.
---

Pulse combines Terrabuild executions with GitHub pull requests, reviews, and
comments. It groups metrics by the question they help answer and keeps the
underlying builds or GitHub records available as evidence.

Pulse is not a developer scorecard. A metric indicates where to investigate;
it does not establish cause or individual performance.

## Activity

Activity shows completed pull requests, Terrabuild runs, observed releases, and
estimated build time saved through reuse. Compare complete weeks or months and
filter by repository, team, or contributor.

## Involvement

Involvement counts distinct people who triggered Terrabuild runs for a selected
repository project. It can reveal where delivery knowledge is concentrated, but
it does not measure code ownership or productivity.

## Flow

Flow asks whether work is moving or waiting. Current measures include:

- pull-request lifetime;
- time to first human feedback;
- time to first formal review;
- concurrently open pull requests;
- stale pull requests.

## Feedback

Feedback describes how quickly and reliably the delivery system reports on a
change:

- Terrabuild run duration;
- success rate;
- recovery time after a failed run.

## Delivery

Delivery uses configured release profiles to measure release interval and the
time from merge to the first containing release. Without meaningful release
rules, these measures do not have a reliable boundary.

## Behavior

Behavior highlights recurring differences inside the selected cohort, including
pull-request size, concurrent work, and review concentration. Treat these as
prompts to inspect workflow and team context.

## Read a metric responsibly

1. Keep the repository, team, contributor, and time period stable while comparing values.
2. Check how many observations support the result.
3. Open the evidence rather than drawing a conclusion from the aggregate alone.
4. Look for a persistent pattern instead of reacting to one short period.
5. Discuss system conditions before attributing the result to a person.

GitHub and Terrabuild provide different parts of the evidence. Connect both to
use the full Pulse model.
