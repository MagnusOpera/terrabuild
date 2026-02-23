module Terrabuild.Configuration.FrontEnd.Workspace

let parse txt =
    let ast = Terrabuild.Lang.FrontEnd.parse txt
    Terrabuild.Configuration.Transpiler.Workspace.transpile ast.Blocks

let parseWithSource sourceName txt =
    let ast = Terrabuild.Lang.FrontEnd.parseWithSource sourceName txt
    Terrabuild.Configuration.Transpiler.Workspace.transpile ast.Blocks
