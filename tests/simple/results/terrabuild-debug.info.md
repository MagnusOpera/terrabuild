# Configuration

| Option | Value |
|--------|-------|
| Targets | build test |
| Force | True |
| LocalOnly | True |
| MaxConcurrency | 2 |
| Engine | docker |
| Debug | True |

# Build Graph

```mermaid
flowchart TD
classDef build stroke:red,stroke-width:3px
classDef restore stroke:orange,stroke-width:3px
classDef ignore stroke:black,stroke-width:3px
D3984AFC9D549D42ADECBD6459B06E6B08B15A33086AC8611A1C78C8798D8E3B("<b>build</b> 
.")
EF2C594E75AEFBC98D3ED0EFB175C02347454B326B734D21A497C1A12E3FBB0D("<b>build</b> 
.")
deployments/terraform-deploy:build("<b>build</b> 
deployments/terraform-deploy")
libraries/dotnet-lib:build("<b>build</b> 
libraries/dotnet-lib")
libraries/npm-lib:build("<b>build</b> 
libraries/npm-lib")
libraries/shell-lib:build("<b>build</b> 
libraries/shell-lib")
projects/dotnet-app:build("<b>build dotnet_app</b> 
projects/dotnet-app")
projects/make-app:build("<b>build</b> 
projects/make-app")
projects/npm-app:build("<b>build npm_app</b> 
projects/npm-app")
projects/open-api:build("<b>build</b> 
projects/open-api")
projects/rust-app:build("<b>build</b> 
projects/rust-app")
tests/playwright:test("<b>test playwright_test</b> 
tests/playwright")
class D3984AFC9D549D42ADECBD6459B06E6B08B15A33086AC8611A1C78C8798D8E3B ignore
class EF2C594E75AEFBC98D3ED0EFB175C02347454B326B734D21A497C1A12E3FBB0D ignore
deployments/terraform-deploy:build --> projects/dotnet-app:build
deployments/terraform-deploy:build --> projects/npm-app:build
class deployments/terraform-deploy:build ignore
class libraries/dotnet-lib:build ignore
class libraries/npm-lib:build ignore
class libraries/shell-lib:build ignore
projects/dotnet-app:build --> libraries/dotnet-lib:build
class projects/dotnet-app:build ignore
projects/make-app:build --> libraries/shell-lib:build
class projects/make-app:build ignore
projects/npm-app:build --> libraries/npm-lib:build
class projects/npm-app:build ignore
class projects/open-api:build ignore
class projects/rust-app:build ignore
class tests/playwright:test ignore
```

