module example.com/go-app

go 1.26.0

require example.com/go-lib v0.0.0

replace (
    example.com/go-lib => ../go-lib
)
