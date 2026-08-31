---
title: Deployment
prev: /docs/getting-started/quick-start
next: /docs/getting-started/key-concepts
---

Terrabuild does not have a separate deployment mode. A deployment is a target with commands and dependencies, so the same graph can order application builds, image publication, infrastructure planning, and deployment.

## Connect deployment to build outputs

The workspace policy below makes `plan` depend on distributable artifacts from upstream projects. The `deploy` target always runs because applying infrastructure is a side effect, not a reusable build result.

```hcl {filename="WORKSPACE"}
target build {
  depends_on = [ target.^build ]
}

target dist {
  depends_on = [ target.build target.^build ]
}

target plan {
  environment_sensitive = true
  artifacts = ~managed
  depends_on = [ target.^dist ]
}

target deploy {
  build = ~always
  artifacts = ~none
  depends_on = [ target.plan ]
}

extension @terraform {
  image = "hashicorp/terraform:1.8.4"
}
```

The policies reflect the ownership and lifetime of each result:

- `target.^dist` selects distributable outputs from the deployment project's upstream applications.
- `environment_sensitive = true` permits the plan to consume the selected environment and includes that consumed value in its cache identity.
- `artifacts = ~managed` preserves the declared Terraform plan file so an authorized developer or CI runner can restore it through Insights.
- `build = ~always` makes every selected deployment perform the side effect even when its declared inputs match an earlier run.
- `artifacts = ~none` prevents a previous application from becoming a reusable result for a new deployment request.

These settings are deliberate, not a required template. Use `artifacts = ~workspace` when a plan must remain on one machine, and keep the inferred/default artifact mode when the extension already expresses the intended ownership. See [Target policies](/docs/getting-started/target-policies) for the decision guide.

The deployment project names the application projects as dependencies. That relationship gives `target.^dist` its upstream project set.

```hcl {filename="src/deploy/PROJECT"}
project infrastructure {
  depends_on = [ project.api project.web ]
  environments = [ "staging" "production" ]
  @terraform { }
}

target plan {
  @terraform init { }
  @terraform plan {
    variables = {
      environment: terrabuild.environment
      api_version: project.api.version
      web_version: project.web.version
    }
  }
}

target deploy {
  @terraform init { }
  @terraform apply { }
}
```

`terrabuild.environment` affects the Terraform plan, so `plan` opts into environment-sensitive inputs. When `--environment` is present, the project accepts only `staging` and `production`; another value excludes it from selection. Require an explicit environment in the deployment workflow rather than running this target without the flag.

## Inspect before running

`explain` resolves the graph and operations without executing them:

```bash
terrabuild explain deploy --environment staging
```

Check that the output includes the expected application `dist` targets, the infrastructure `plan`, and the final `deploy` target. It also reports whether each prerequisite would build or restore from cache.

## Create the plan

Run the plan after configuring the Terraform backend and credentials:

```bash
terrabuild run plan --environment staging
```

The built-in Terraform extension writes `terrabuild.planfile`. With the managed policy above, Terrabuild stores it locally and uploads it to encrypted Insights storage when connected. A workspace-only policy would keep the same restoration behavior local to one machine.

## Apply the deployment

The next command executes Terraform and can change infrastructure:

```bash
terrabuild run deploy --environment staging
```

Terrabuild rebuilds or restores the required application artifacts first, then builds or restores the Terraform plan. It runs `deploy` only after those prerequisites succeed.

Use separate credentials and backend state for each environment. Terrabuild orders the commands, but Terraform and the configured provider remain responsible for the infrastructure change.
