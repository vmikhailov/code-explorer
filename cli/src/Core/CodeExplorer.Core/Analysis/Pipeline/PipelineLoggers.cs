using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Parser;

public static partial class Layer1LogMessages
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "[Layer 1    ] Starting physical scan of directory topology...")]
    public static partial void StartingPhysicalScan(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "[Layer 1    ] Physical topology scan complete. Found {FileCount} files, {FolderCount} folders.")]
    public static partial void PhysicalScanComplete(ILogger logger, int fileCount, int folderCount);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Debug, Message = "[Layer 1    ] Skipping excluded directory: '{RelativeDir}'")]
    public static partial void SkippingExcludedDirectory(ILogger logger, string relativeDir);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "[Layer 1    ] GitIgnore: Skipping ignored directory: '{RelativeDir}'")]
    public static partial void SkippingGitIgnoredDirectory(ILogger logger, string relativeDir);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "[Layer 1    ] Failed to stream and analyze file '{RelativeFile}': {ErrorMessage}")]
    public static partial void FileReadFailed(ILogger logger, string relativeFile, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Debug, Message = "[Layer 1    ] Workspace name lookup fallback used for '{WorkspacePath}': {ErrorMessage}")]
    public static partial void WorkspaceNameLookupFallback(ILogger logger, string workspacePath, string errorMessage);
}

public readonly struct Layer1Logger(ILogger logger)
{
    public void ScanStarted() => Layer1LogMessages.StartingPhysicalScan(logger);
    public void ScanCompleted(int fileCount, int folderCount) => Layer1LogMessages.PhysicalScanComplete(logger, fileCount, folderCount);
    public void SkippingExcludedDirectory(string relativeDir) => Layer1LogMessages.SkippingExcludedDirectory(logger, relativeDir);
    public void SkippingGitIgnoredDirectory(string relativeDir) => Layer1LogMessages.SkippingGitIgnoredDirectory(logger, relativeDir);
    public void FileReadFailed(string relativeFile, Exception ex) => Layer1LogMessages.FileReadFailed(logger, relativeFile, ex.Message, ex);
    public void WorkspaceNameLookupFallback(string workspacePath, Exception ex) => Layer1LogMessages.WorkspaceNameLookupFallback(logger, workspacePath, ex.Message);
}

public static partial class Layer2LogMessages
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "[Layer 2    ] Starting project boundary detection and dependency scan...")]
    public static partial void StartingBoundaryScan(ILogger logger);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "[Layer 2    ] Resolved and enqueued {DependencyCount} project dependencies.")]
    public static partial void ResolvedDependencies(ILogger logger, int dependencyCount);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "[Layer 2    ] Project detection scan complete. Found {ProjectCount} projects, {PackageCount} package nodes.")]
    public static partial void BoundaryScanComplete(ILogger logger, int projectCount, int packageCount);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "[Layer 2    ] Sibling library scan error in '{LibRoot}': {ErrorMessage}")]
    public static partial void SiblingLibraryScanError(ILogger logger, string libRoot, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning, Message = "[Layer 2    ] Error parsing dependencies for {ProjectType} in '{ProjectDir}': {ErrorMessage}")]
    public static partial void DependencyParseError(ILogger logger, string projectType, string projectDir, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning, Message = "[Layer 2    ] Error getting produced package from {ProjectType} parser in '{ProjectDir}': {ErrorMessage}")]
    public static partial void ProducedPackageError(ILogger logger, string projectType, string projectDir, string errorMessage, Exception? ex);
}

public readonly struct Layer2Logger(ILogger logger)
{
    public void ScanStarted() => Layer2LogMessages.StartingBoundaryScan(logger);
    public void DependenciesEnqueued(int dependencyCount) => Layer2LogMessages.ResolvedDependencies(logger, dependencyCount);
    public void ScanCompleted(int projectCount, int packageCount) => Layer2LogMessages.BoundaryScanComplete(logger, projectCount, packageCount);
    public void SiblingLibraryScanError(string libRoot, Exception ex) => Layer2LogMessages.SiblingLibraryScanError(logger, libRoot, ex.Message, ex);
    public void DependencyParseFailed(string projectType, string projectDir, string errorMessage, Exception ex) => Layer2LogMessages.DependencyParseError(logger, projectType, projectDir, errorMessage, ex);
    public void ProducedPackageParseFailed(string projectType, string projectDir, string errorMessage, Exception ex) => Layer2LogMessages.ProducedPackageError(logger, projectType, projectDir, errorMessage, ex);
}

public static partial class Layer3LogMessages
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "[Layer 3    ] Starting tree-sitter AST syntactic parsing pass...")]
    public static partial void StartingSyntacticScan(ILogger logger);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug, Message = "[ConfigurationIndex] Registered {Count} service endpoints and configuration keys into ConstantRegistry.")]
    public static partial void RegisteredConfigurationUrls(ILogger logger, int count);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Debug, Message = "[Layer 3    ] Parsing project {ProjectIndex} of {TotalProjects} at: '{ProjectPath}'...")]
    public static partial void ParsingProject(ILogger logger, int projectIndex, int totalProjects, string projectPath);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "[Layer 3    ] Parsing syntax trees: {Completed}/{TotalFiles} files ({Percentage}%) [{ProjectName}]...")]
    public static partial void ParsingProgress(ILogger logger, int completed, int totalFiles, int percentage, string projectName);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Warning, Message = "[Layer 3    ] Error parsing file '{FilePath}': {ErrorMessage}")]
    public static partial void FileParseError(ILogger logger, string filePath, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Warning, Message = "[Layer 3    ] Error processing visitor for '{FilePath}': {ErrorMessage}")]
    public static partial void VisitorProcessingError(ILogger logger, string filePath, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 3007, Level = LogLevel.Information, Message = "[Layer 3    ] Syntactic parsing pass complete. Parsed {SyntaxTreeCount} files.")]
    public static partial void SyntacticScanComplete(ILogger logger, int syntaxTreeCount);
}

public readonly struct Layer3Logger(ILogger logger)
{
    public void ScanStarted() => Layer3LogMessages.StartingSyntacticScan(logger);
    public void ConfigurationIndexRegistered(int count) => Layer3LogMessages.RegisteredConfigurationUrls(logger, count);
    public void ParsingProject(int projectIndex, int totalProjects, string projectPath) => Layer3LogMessages.ParsingProject(logger, projectIndex, totalProjects, projectPath);
    public void ParsingProgress(int completed, int totalFiles, int percentage, string projectName) => Layer3LogMessages.ParsingProgress(logger, completed, totalFiles, percentage, projectName);
    public void FileParseFailed(string filePath, string errorMessage, Exception ex) => Layer3LogMessages.FileParseError(logger, filePath, errorMessage, ex);
    public void VisitorFailed(string filePath, string errorMessage, Exception ex) => Layer3LogMessages.VisitorProcessingError(logger, filePath, errorMessage, ex);
    public void ScanCompleted(int syntaxTreeCount) => Layer3LogMessages.SyntacticScanComplete(logger, syntaxTreeCount);
}

public static partial class Layer4LogMessages
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "[Layer 4    ] Starting semantic enrichment pass...")]
    public static partial void StartingSemanticPass(ILogger logger);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "[Layer 4    ] Enriching {TotalProjects} projects ({SyntaxTreeCount} syntax trees) with semantic information...")]
    public static partial void EnrichingProjects(ILogger logger, int totalProjects, int syntaxTreeCount);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "[Layer 4    ] Materializing semantic workload hierarchy ({TotalProjects} projects)...")]
    public static partial void MaterializingWorkloadHierarchy(ILogger logger, int totalProjects);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "[Layer 4    ] Uploading {RelationshipCount} semantic relationships...")]
    public static partial void UploadingSemanticRelationships(ILogger logger, int relationshipCount);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Debug, Message = "[Layer 4    ] Queuing workspace ontology tree for database persistence...")]
    public static partial void QueuingOntologyTree(ILogger logger);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Information, Message = "[Layer 4    ] Semantic enrichment pass complete. Identified {SemanticNodeCount} semantic nodes.")]
    public static partial void SemanticPassComplete(ILogger logger, int semanticNodeCount);

    [LoggerMessage(EventId = 4007, Level = LogLevel.Information, Message = "[Layer 4    ] Enriched {Processed}/{Total} projects ({Percentage}%) - {ProjectName}...")]
    public static partial void EnrichedProjectProgress(ILogger logger, int processed, int total, int percentage, string projectName);
}

public readonly struct Layer4Logger(ILogger logger)
{
    public void ScanStarted() => Layer4LogMessages.StartingSemanticPass(logger);
    public void EnrichingProjects(int totalProjects, int syntaxTreeCount) => Layer4LogMessages.EnrichingProjects(logger, totalProjects, syntaxTreeCount);
    public void MaterializingWorkloads(int totalProjects) => Layer4LogMessages.MaterializingWorkloadHierarchy(logger, totalProjects);
    public void UploadingSemanticRelationships(int relationshipCount) => Layer4LogMessages.UploadingSemanticRelationships(logger, relationshipCount);
    public void QueuingOntologyTree() => Layer4LogMessages.QueuingOntologyTree(logger);
    public void ScanCompleted(int semanticNodeCount) => Layer4LogMessages.SemanticPassComplete(logger, semanticNodeCount);
    public void EnrichingProgress(int processed, int total, int percentage, string projectName) => Layer4LogMessages.EnrichedProjectProgress(logger, processed, total, percentage, projectName);
}

public static partial class Layer5LogMessages
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "[Layer 5    ] Starting Layer 5 (Late Binding, Cross-References & Post-Indexing Analysis)...")]
    public static partial void StartingGlobalPass(ILogger logger);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Debug, Message = "[Layer 5    ] Uploading {Count} project containment (BelongsTo) relationships...")]
    public static partial void UploadingBelongsToRelationships(ILogger logger, int count);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "[Layer 5    ] Running global post-indexing analysis via PostIndexAnalyzer across workspace...")]
    public static partial void RunningGlobalPostIndexAnalysis(ILogger logger);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Information, Message = "[Layer 5    ] Running in-memory post-indexing analysis via PostIndexAnalyzer...")]
    public static partial void RunningInMemoryPostIndexAnalysis(ILogger logger);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "[Layer 5    ] Late binding and post-indexing analysis pass complete.")]
    public static partial void GlobalPassComplete(ILogger logger);

    [LoggerMessage(EventId = 5006, Level = LogLevel.Information, Message = "[Layer 5    ] Uploading {Count} local project dependency relationships...")]
    public static partial void UploadingProjectDependencies(ILogger logger, int count);

    [LoggerMessage(EventId = 5007, Level = LogLevel.Information, Message = "[Layer 5    ] Resolving {TotalReferences} global cross-references...")]
    public static partial void ResolvingCrossReferences(ILogger logger, int totalReferences);

    [LoggerMessage(EventId = 5008, Level = LogLevel.Information, Message = "[Layer 5    ] Resolving global cross-references: {Resolved}/{Total} ({Percentage}%)...")]
    public static partial void ResolvingCrossReferencesProgress(ILogger logger, int resolved, int total, int percentage);

    [LoggerMessage(EventId = 5009, Level = LogLevel.Information, Message = "[Layer 5    ] Resolved {Resolved}/{Total} global cross-references ({EdgeCount} edges).")]
    public static partial void ResolvedCrossReferencesComplete(ILogger logger, int resolved, int total, int edgeCount);

    [LoggerMessage(EventId = 5010, Level = LogLevel.Debug, Message = "[Layer 5    ] Uploading {Count} resolved reference relationships...")]
    public static partial void UploadingReferenceRelationships(ILogger logger, int count);

    [LoggerMessage(EventId = 5011, Level = LogLevel.Information, Message = "[Layer 5    ] Late-binding {ExternalServiceCount} external service calls against {EndpointCount} endpoints and {EntryPointCount} entry points...")]
    public static partial void StartingLateBinding(ILogger logger, int externalServiceCount, int endpointCount, int entryPointCount);

    [LoggerMessage(EventId = 5012, Level = LogLevel.Debug, Message = "[Layer 5    ] Binding ExternalService '{ServiceId}' to EntryPoint '{EntryPointId}'")]
    public static partial void BindingExternalServiceToEntryPoint(ILogger logger, string serviceId, string entryPointId);

    [LoggerMessage(EventId = 5013, Level = LogLevel.Debug, Message = "[Layer 5    ] Synthesized dependency: Project '{CallerProject}' -> Project '{TargetProject}' via entrypoint '{EntryPointName}'")]
    public static partial void SynthesizedEntryPointDependency(ILogger logger, string callerProject, string targetProject, string entryPointName);

    [LoggerMessage(EventId = 5014, Level = LogLevel.Debug, Message = "[Layer 5    ] Binding ExternalService '{ServiceId}' to Endpoint '{EndpointId}'")]
    public static partial void BindingExternalServiceToEndpoint(ILogger logger, string serviceId, string endpointId);

    [LoggerMessage(EventId = 5015, Level = LogLevel.Debug, Message = "[Layer 5    ] Synthesized dependency: Project '{CallerProject}' -> Project '{TargetProject}' via endpoint '{RouteTemplate}'")]
    public static partial void SynthesizedEndpointDependency(ILogger logger, string callerProject, string targetProject, string? routeTemplate);

    [LoggerMessage(EventId = 5016, Level = LogLevel.Debug, Message = "[Layer 5    ] Inferred project dependency: Project '{CallerProject}' -> Project '{TargetProject}' via service domain '{ServiceDomain}'")]
    public static partial void InferredDomainDependency(ILogger logger, string callerProject, string targetProject, string serviceDomain);

    [LoggerMessage(EventId = 5017, Level = LogLevel.Information, Message = "[Layer 5    ] Late-binding progress: {Processed}/{Total} external services ({Percentage}%)...")]
    public static partial void LateBindingProgress(ILogger logger, int processed, int total, int percentage);

    [LoggerMessage(EventId = 5018, Level = LogLevel.Information, Message = "[Layer 5    ] Successfully bound {BoundCount} ExternalService call(s) to endpoints/entrypoints.")]
    public static partial void LateBindingComplete(ILogger logger, int boundCount);

    [LoggerMessage(EventId = 5019, Level = LogLevel.Debug, Message = "[Layer 5    ] Uploading {Count} late-bound relationships...")]
    public static partial void UploadingLateBoundRelationships(ILogger logger, int count);

    [LoggerMessage(EventId = 5020, Level = LogLevel.Information, Message = "[Layer 5    ] Targeted preloading: querying SQLite for {SymbolCount} referenced symbol names across workspace...")]
    public static partial void StartingTargetedPreload(ILogger logger, int symbolCount);

    [LoggerMessage(EventId = 5021, Level = LogLevel.Information, Message = "[Layer 5    ] Targeted preloading complete: preloaded {AddedCount} external symbols into memory.")]
    public static partial void TargetedPreloadComplete(ILogger logger, int addedCount);

    [LoggerMessage(EventId = 5022, Level = LogLevel.Debug, Message = "[Layer 5    ] Failed to resolve inheritance reference '{TargetName}': {ErrorMessage}")]
    public static partial void InheritanceResolutionFailed(ILogger logger, string targetName, string errorMessage);
}

public readonly struct Layer5Logger(ILogger logger)
{
    public void ScanStarted() => Layer5LogMessages.StartingGlobalPass(logger);
    public void UploadingBelongsToRelationships(int count) => Layer5LogMessages.UploadingBelongsToRelationships(logger, count);
    public void RunningGlobalPostIndexAnalysis() => Layer5LogMessages.RunningGlobalPostIndexAnalysis(logger);
    public void RunningInMemoryPostIndexAnalysis() => Layer5LogMessages.RunningInMemoryPostIndexAnalysis(logger);
    public void ScanCompleted() => Layer5LogMessages.GlobalPassComplete(logger);
    public void UploadingProjectDependencies(int count) => Layer5LogMessages.UploadingProjectDependencies(logger, count);
    public void ResolvingCrossReferences(int totalReferences) => Layer5LogMessages.ResolvingCrossReferences(logger, totalReferences);
    public void CrossReferenceProgress(int resolved, int total, int percentage) => Layer5LogMessages.ResolvingCrossReferencesProgress(logger, resolved, total, percentage);
    public void CrossReferencesResolved(int resolved, int total, int edgeCount) => Layer5LogMessages.ResolvedCrossReferencesComplete(logger, resolved, total, edgeCount);
    public void UploadingReferenceRelationships(int count) => Layer5LogMessages.UploadingReferenceRelationships(logger, count);
    public void LateBindingStarted(int externalServiceCount, int endpointCount, int entryPointCount) => Layer5LogMessages.StartingLateBinding(logger, externalServiceCount, endpointCount, entryPointCount);
    public void BindingToEntryPoint(string serviceId, string entryPointId) => Layer5LogMessages.BindingExternalServiceToEntryPoint(logger, serviceId, entryPointId);
    public void SynthesizedDependencyViaEntryPoint(string callerProject, string targetProject, string entryPointName) => Layer5LogMessages.SynthesizedEntryPointDependency(logger, callerProject, targetProject, entryPointName);
    public void BindingToEndpoint(string serviceId, string endpointId) => Layer5LogMessages.BindingExternalServiceToEndpoint(logger, serviceId, endpointId);
    public void SynthesizedDependencyViaEndpoint(string callerProject, string targetProject, string? routeTemplate) => Layer5LogMessages.SynthesizedEndpointDependency(logger, callerProject, targetProject, routeTemplate);
    public void InferredDependencyViaDomain(string callerProject, string targetProject, string serviceDomain) => Layer5LogMessages.InferredDomainDependency(logger, callerProject, targetProject, serviceDomain);
    public void LateBindingProgress(int processed, int total, int percentage) => Layer5LogMessages.LateBindingProgress(logger, processed, total, percentage);
    public void LateBindingCompleted(int boundCount) => Layer5LogMessages.LateBindingComplete(logger, boundCount);
    public void UploadingLateBoundRelationships(int count) => Layer5LogMessages.UploadingLateBoundRelationships(logger, count);
    public void PreloadingTargetedSymbols(int symbolCount) => Layer5LogMessages.StartingTargetedPreload(logger, symbolCount);
    public void PreloadedTargetedSymbols(int addedCount) => Layer5LogMessages.TargetedPreloadComplete(logger, addedCount);
    public void InheritanceResolutionFailed(string targetName, string errorMessage) => Layer5LogMessages.InheritanceResolutionFailed(logger, targetName, errorMessage);
}

public static partial class PipelineLogMessages
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "[Indexer    ] Indexing process completed successfully! Total Nodes: {TotalNodes:N0}, Total Relationships: {TotalRelationships:N0}.")]
    public static partial void IndexingCompleted(ILogger logger, int totalNodes, int totalRelationships);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Warning, Message = "[Indexer    ] Recorded {TotalIssues} scan/parse issue(s) in graph ({SyntaxErrorCount} syntax error(s), {OtherErrorCount} runtime/manifest issue(s)). Query via: MATCH (i:ScanIssue) RETURN i")]
    public static partial void RecordedScanIssuesSummary(ILogger logger, int totalIssues, int syntaxErrorCount, int otherErrorCount);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "[Database   ] Synchronizing file registry and caching AST snapshots for {FileCount} files in parallel...")]
    public static partial void SynchronizingFileRegistry(ILogger logger, int fileCount);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Information, Message = "[Incremental] Persisting {EntryCount} file registry metadata and snapshot entries to SQLite...")]
    public static partial void PersistingFileRegistry(ILogger logger, int entryCount);

    [LoggerMessage(EventId = 6005, Level = LogLevel.Information, Message = "[Incremental] Pruning {StaleCount} obsolete file registry entries from SQLite...")]
    public static partial void PruningFileRegistry(ILogger logger, int staleCount);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Information, Message = "[Database   ] Clearing previous data for '{TargetPath}'...")]
    public static partial void ClearingDatabaseData(ILogger logger, string targetPath);

    [LoggerMessage(EventId = 6007, Level = LogLevel.Debug, Message = "[Database   ] All background channel persistence writes completed! Total persisted: {NodeCount} nodes, {RelationshipCount} relationships.")]
    public static partial void QueuedOntologyTree(ILogger logger, int nodeCount, int relationshipCount);
}

public readonly struct PipelineLogger(ILogger logger)
{
    public void IndexingCompleted(int totalNodes, int totalRelationships) => PipelineLogMessages.IndexingCompleted(logger, totalNodes, totalRelationships);
    public void RecordedScanIssues(int totalIssues, int syntaxErrorCount, int otherErrorCount) => PipelineLogMessages.RecordedScanIssuesSummary(logger, totalIssues, syntaxErrorCount, otherErrorCount);
    public void SyncingFileRegistry(int fileCount) => PipelineLogMessages.SynchronizingFileRegistry(logger, fileCount);
    public void PersistingFileRegistry(int entryCount) => PipelineLogMessages.PersistingFileRegistry(logger, entryCount);
    public void PruningFileRegistry(int staleCount) => PipelineLogMessages.PruningFileRegistry(logger, staleCount);
    public void ClearingWorkspaceData(string targetPath) => PipelineLogMessages.ClearingDatabaseData(logger, targetPath);
    public void PersistenceSummary(int nodeCount, int relationshipCount) => PipelineLogMessages.QueuedOntologyTree(logger, nodeCount, relationshipCount);
}
