---
title: "gradle"
---



## Available Commands
| Command | Description |
|---------|-------------|
| [dispatch](/docs/extensions/gradle/dispatch) | Runs an arbitrary Gradle command (action name is forwarded to `gradle`). |
| [build](/docs/extensions/gradle/build) | Invokes `gradle assemble` for the chosen configuration. |

## Project Initializer
Gradle extension for build workflows.
```
project {
  @gradle { }
}
```
Equivalent to:
```
project {
    outputs = [ "build/classes/" ]
}
```
