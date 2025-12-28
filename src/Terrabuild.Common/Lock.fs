module Lock

let inline lock (lockObject : System.Threading.Lock) ([<InlineIfLambda>] action) =
    let mutable scope = lockObject.EnterScope ()
    try action ()
    finally scope.Dispose ()
