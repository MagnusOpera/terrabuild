namespace Contracts

type ProvenanceTransformation = {
    NodeId: string
    Step: string
    Entrypoint: string
    Preservation: Terrabuild.ScriptingContracts.ProvenanceEffect
    Origin: string
    ScriptIdentity: string
    ContractFingerprint: string
}

type ProvenanceDependency = {
    NodeId: string
    ProductionId: string option
}

type ProvenanceDeclaration = {
    Version: int
    ProducerVersion: string
    ProductionId: string
    NodeId: string
    Transformations: ProvenanceTransformation list
    Dependencies: ProvenanceDependency list
}

type ProvenancePlan = {
    Transformations: ProvenanceTransformation list
}
