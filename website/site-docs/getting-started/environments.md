---
title: Configure environments
description: Separate project selection, tool settings, and environment-specific results.
---

An environment can change which projects participate, which settings a tool
receives, and which results can be reused. Model those choices separately.
For example, a compiled application may be identical in staging and production,
while its Terraform plan must use a different backend and configuration.

## Three questions to answer

| Question | Configuration |
| --- | --- |
| Which projects belong to this environment? | `project.environments` and `--environment`. |
| What settings should commands receive? | Workspace variables, locals, and extension arguments or environment values. |
| Does a result consume deployment context? | `environment_sensitive = true` on the consuming target. |

Passing `--environment staging` sets `terrabuild.environment`. It does not
implicitly load a settings file, select a Terraform workspace, switch cloud
credentials, or make every target environment-sensitive. Configure those effects
where your tools need them.

## Try a context-specific target

Continue in the [tutorial workspace](./quick-start.md). Add this declaration to
`WORKSPACE`:

```terrabuild title="WORKSPACE"
variable region {
  default = "eu-west-1"
}

target describe { }
```

Create a script that writes a small settings file:

```sh title="package/describe.sh"
set -eu
mkdir -p dist
printf 'environment=%s region=%s\n' "$1" "$2" > dist/settings.txt
```

Append a target to `package/PROJECT`:

```terrabuild title="package/PROJECT"
target describe {
  environment_sensitive = true
  build = ~always
  artifacts = ~none
  @shell sh {
    args = "describe.sh ${terrabuild.environment} ${var.region}"
  }
}
```

Run it with two contexts:

```bash
terrabuild run describe --project package --environment staging
cat package/dist/settings.txt
terrabuild run describe --project package --environment production --variable region=eu-central-1
cat package/dist/settings.txt
```

The first file reads `environment=staging region=eu-west-1`; the second reads
`environment=production region=eu-central-1`. The target always rewrites this
local file and stores no cached artifact. It makes no external changes.

`region` is a workspace variable, accessed as `var.region`. `terrabuild.environment`
is a predefined run value. Using the latter requires the target's explicit
sensitivity declaration, even for this non-cacheable action.

To try the validation, temporarily remove `environment_sensitive = true` and run
`explain describe` with the same options. Terrabuild reports the neutral target
that consumes `terrabuild.environment`. Restore the declaration afterwards.

### Variable precedence

Workspace variables must be declared before use. For `region`, the override
order is:

1. `TB_VAR_region` in the process environment;
2. `--variable region=...` on the command line;
3. the declared default.

An existing `TB_VAR_region` therefore takes precedence over the command in this
tutorial. See [Variable block](../workspace/variable.md) for the full reference.

## Keep common work reusable

The tutorial's `build` target does not read the environment, so its inputs can
remain identical between these requests:

```bash
terrabuild run build --project package --environment staging
terrabuild run build --project package --environment production
```

For a real deployment, put environment-dependent rendering or planning in a
separate target after compilation. Mark that target environment-sensitive and
pass the context explicitly. Hashes of the sensitive values it consumes then
participate in its cache identity.

The same rule applies when a sensitive value is used indirectly through a local
or extension setting. Other sensitive predefined inputs include branch, commit,
and CI state; see [Predefined variables](../expression/predefined-variables.md).

## Select projects by environment

An infrastructure project can declare where it participates:

```terrabuild
project infrastructure {
  environments = [ "staging" "production" "preview-*" ]
  @terraform { }
}
```

With `--environment`, Terrabuild matches these patterns case-insensitively.
Projects without an environment list remain enabled. Without `--environment`,
the list does not exclude the project.

This is selection metadata, not an authorization boundary. CI or the deployment
system must still control access to protected environments. Inspect the complete
selection with `explain` before running it.

## Configure the execution environment too

The deployment destination and the tool's execution environment are different.
An extension can pin a container image and platform, forward selected host
variables, and set values for its operations:

```terrabuild
extension @terraform {
  image = "hashicorp/terraform:1.10"
  platform = "linux/amd64"
  variables = [ "ARM_*" ]
  env {
    TF_IN_AUTOMATION = "true"
  }
}
```

Choose the tool version and platform supported by your repository. This example
forwards Azure credential variables to Terraform; adapt the variable list for
your provider. The extension configuration makes those selected values part of
the target's input fingerprint. Container execution requires Docker or Podman.

Use [extension specialization](../project/extension.md) for project-specific
images or additional settings. Scalar settings can replace inherited values;
`defaults` and `env` can add keys but cannot replace inherited keys.

Next, [model a deployment](./deployment.md) to connect application artifacts,
environment-specific state, and explicit external actions.
