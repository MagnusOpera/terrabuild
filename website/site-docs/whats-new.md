---
id: whats-new
title: What's New
slug: /whats-new
---

For the complete history, see the full [CHANGELOG.md](https://github.com/MagnusOpera/Terrabuild/blob/main/CHANGELOG.md) on GitHub.

## 0.200.1-next

### 0.200.1-next

- Restore remote cache publication through Insights while retaining atomic cache generations.
- Expand the Terrabuild agent skill with current target-policy, cache, phase, batching, locking, recovery, reporting, and Insights guidance.

### 0.200.0-next

- Document that Insights reporting is mandatory once a run connects while remote artifact transfer remains a cache concern.
- Replace debug diagnostics atomically without allowing diagnostic I/O to mask build failures.
- Fail file-lock acquisition on filesystem errors while continuing to wait for genuine contention.
- Emit terminal build progress only after scheduler outcomes are finalized.
- Retry abandoned container cleanup after daemon errors and timeouts.
- Keep builds usable through remote cache read and publication failures.
- Recover interrupted workspace restores conservatively when their transaction index is unreadable.
- Exclude Git repository metadata from workspace project discovery by default.
- Validate smoke-test reports against the current diagnostic schema.
- Recover interrupted restores through an indexed startup path instead of repeated workspace scans.
- Reap daemon-owned containers after cancellation or abrupt Terrabuild termination.
- Refuse cache clearing while another Terrabuild process is using the profile.
- Publish remote cache artifacts as verified atomic generations.
- Recover the previous local cache entry after an interrupted publication.
- Preserve completed execution results when publication or report rendering fails.
- Version diagnostic reports when their result vocabulary changes.
- Fail graph preparation when target actions cannot be completely evaluated.
- Distinguish logical cache reuse from physical execution for restored batch members.
- Clean failed and process-abandoned cache staging directories safely.
- Report tasks prevented by failed dependencies as blocked instead of failed executions.
- Report cached failure summaries as summaries instead of restores.
- Keep expanded forwarded environment secrets out of container arguments and cached summaries.
- Recover interrupted output restores before configuration and project hashing begin.
- Recover interrupted cache output restores from a durable transaction journal.
- Report realized restore fallbacks and include batch staging and publication in finalization timing.
- Dispose synthetic batch cache entries after their logs are copied to member results.
- Restore explicitly empty cached output sets by removing stale declared files.
- Invalidate cached targets when declared forwarded environment values change without retaining their secret values.
- Rebuild targets when remotely cached output files disappear before restoration.
- Keep execution batches separate when container CPU limits or extension scripts differ.
- Prevent targets with different names from being combined into one execution batch.
- Define the last command as the source of a target's default artifact mode.
- Select the latest website-capable release tag when preparing local documentation.
- Render caching and batching criteria as proper documentation lists.
- Keep links to the Next-only target policy guide within the active documentation version.
- Correct target reference examples to keep managed files separate from external images.
- Explain deployment target policies through plan ownership and side-effect use cases.
- Connect introductory and reference documentation to the target policy decision guide.
- Add symptom-based troubleshooting for cache, propagation, batching, locks, and environment inputs.
- Turn the playground quick start into an annotated target-policy tutorial.
- Explain batch-mode use cases, tradeoffs, locking differences, and troubleshooting.
- Add ownership-based caching guidance and concrete artifact-mode recipes.
- Add a decision guide for target scheduling, reuse, batching, environment, and locking policies.
- Expose the `explain` command in the documentation navigation and command index.
- Clarify how `terrabuild clear --all` treats idle and active target locks.
- Document inherited named locks in the workspace and project target references.
- Document the diagnostic reason used when cached summaries have no restorable outputs.
- Explain that successful external cache hits reuse summaries without restoring files.
- Drain captured process output streams concurrently to prevent full-pipe deadlocks.
- Finalize started Insights builds as failed when runner operations raise an exception.
- Apply named target locks while cached outputs are restored into the workspace.
- Rebuild offline targets when a cached summary exists without its managed outputs.
- Report named locks and lock-wait timing separately from target execution.
- Keep batch output and log staging inside named target lock leases.
- Remove idle target lock files with `terrabuild clear --all`.
- Keep named-lock waiters stable during sustained contention.
- Serialize targets that declare the same named lock across concurrent Terrabuild processes.
- Keep execution diagnostics stable when concurrent containers use unique names.
- Restore cached outputs transactionally without exposing cache directories to the runner.
- Pass container arguments without shell re-parsing and isolate concurrent container names.
- Publish cache entries atomically and persist remotely downloaded summaries locally.
- Encapsulate cache output and log storage behind the cache entry API.
- Clarify that external cache hits reuse execution summaries without restoring artifact files.
- Document that explicitly selected lazy targets execute on cache misses.
- Keep phase barriers from rebuilding downstream targets that already have valid cached artifacts.
- Recompute target cache keys when evaluated output patterns change.

**Full Changelog**: https://github.com/magnusopera/terrabuild/compare/0.199.0-next...0.200.1-next
