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

The built-in Terraform extension writes `terrabuild.planfile`. Terrabuild can cache that file according to the target's artifact mode.

## Apply the deployment

The next command executes Terraform and can change infrastructure:

```bash
terrabuild run deploy --environment staging
```

Terrabuild rebuilds or restores the required application artifacts first, then builds or restores the Terraform plan. It runs `deploy` only after those prerequisites succeed.

Use separate credentials and backend state for each environment. Terrabuild orders the commands, but Terraform and the configured provider remain responsible for the infrastructure change.
