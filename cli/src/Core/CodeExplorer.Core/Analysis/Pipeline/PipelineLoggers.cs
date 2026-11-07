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

    [LoggerMessage(EventId = 1007, Level = LogLevel.Debug, Message = "[Layer 1    ] {HookMessage}")]
    public static partial void DirectoryScanHookMessage(ILogger logger, string hookMessage);
}

public readonly struct Layer1Logger(ILogger logger)
{
    public void ScanStarted() => Layer1LogMessages.StartingPhysicalScan(logger);
    public void ScanCompleted(int fileCount, int folderCount) => Layer1LogMessages.PhysicalScanComplete(logger, fileCount, folderCount);
    public void SkippingExcludedDirectory(string relativeDir) => Layer1LogMessages.SkippingExcludedDirectory(logger, relativeDir);
    public void SkippingGitIgnoredDirectory(string relativeDir) => Layer1LogMessages.SkippingGitIgnoredDirectory(logger, relativeDir);
    public void FileReadFailed(string relativeFile, Exception ex) => Layer1LogMessages.FileReadFailed(logger, relativeFile, ex.Message, ex);
    public void WorkspaceNameLookupFallback(string workspacePath, Exception ex) => Layer1LogMessages.WorkspaceNameLookupFallback(logger, workspacePath, ex.Message);
    public void DirectoryScanHookMessage(string hookMessage) => Layer1LogMessages.DirectoryScanHookMessage(logger, hookMessage);
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

    [LoggerMessage(EventId = 3008, Level = LogLevel.Information, Message = "[Layer 3    ] Analyzing AST semantics and visitors: {Completed}/{TotalFiles} files ({Percentage}%) [{ProjectName}]...")]
    public static partial void VisitorProgress(ILogger logger, int completed, int totalFiles, int percentage, string projectName);
}

public readonly struct Layer3Logger(ILogger logger)
{
    public void ScanStarted() => Layer3LogMessages.StartingSyntacticScan(logger);
    public void ConfigurationIndexRegistered(int count) => Layer3LogMessages.RegisteredConfigurationUrls(logger, count);
    public void ParsingProject(int projectIndex, int totalProjects, string projectPath) => Layer3LogMessages.ParsingProject(logger, projectIndex, totalProjects, projectPath);
    public void ParsingProgress(int completed, int totalFiles, int percentage, string projectName) => Layer3LogMessages.ParsingProgress(logger, completed, totalFiles, percentage, projectName);
    public void VisitorProgress(int completed, int totalFiles, int percentage, string projectName) => Layer3LogMessages.VisitorProgress(logger, completed, totalFiles, percentage, projectName);
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

    [LoggerMessage(EventId = 4008, Level = LogLevel.Warning, Message = "[ConfigurationParser] Failed to parse config file '{RelativePath}': {ErrorMessage}")]
    public static partial void ConfigParseFailed(ILogger logger, string relativePath, string errorMessage, Exception? ex);
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
    public void ConfigParseFailed(string relativePath, Exception ex) => Layer4LogMessages.ConfigParseFailed(logger, relativePath, ex.Message, ex);
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

    [LoggerMessage(EventId = 6008, Level = LogLevel.Debug, Message = "[Database   ] Queued {NodeCount} nodes and {RelationshipCount} relationships to persistence pipeline.")]
    public static partial void QueuedToPersistencePipeline(ILogger logger, int nodeCount, int relationshipCount);

    [LoggerMessage(EventId = 6009, Level = LogLevel.Debug, Message = "Starting background database persistence loop...")]
    public static partial void PersistenceLoopStarted(ILogger logger);

    [LoggerMessage(EventId = 6010, Level = LogLevel.Error, Message = "[Database   ] Error writing to database: {ErrorMessage}")]
    public static partial void DatabaseWriteError(ILogger logger, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 6011, Level = LogLevel.Error, Message = "Consumer task finished with error: {ErrorMessage}")]
    public static partial void ConsumerTaskError(ILogger logger, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 6012, Level = LogLevel.Information, Message = "[Database   ] Persisted {NodeCount:N0} nodes to database...")]
    public static partial void PersistedNodesProgress(ILogger logger, int nodeCount);

    [LoggerMessage(EventId = 6013, Level = LogLevel.Information, Message = "[Database   ] Persisted {RelationshipCount:N0} relationships to database...")]
    public static partial void PersistedRelationshipsProgress(ILogger logger, int relationshipCount);

    [LoggerMessage(EventId = 6014, Level = LogLevel.Debug, Message = "[Database   ] Persisted {NodeCount:N0} nodes to database.")]
    public static partial void PersistedNodesFinal(ILogger logger, int nodeCount);

    [LoggerMessage(EventId = 6015, Level = LogLevel.Debug, Message = "[Database   ] Persisted {RelationshipCount:N0} relationships to database.")]
    public static partial void PersistedRelationshipsFinal(ILogger logger, int relationshipCount);

    [LoggerMessage(EventId = 6016, Level = LogLevel.Warning, Message = "{Tag} [{Category}] '{Location}': {ErrorMessage}")]
    public static partial void ScanIssueWarning(ILogger logger, string tag, string category, string location, string errorMessage, Exception? ex);

    [LoggerMessage(EventId = 6017, Level = LogLevel.Error, Message = "{Tag} [{Category}] '{Location}': {ErrorMessage}")]
    public static partial void ScanIssueError(ILogger logger, string tag, string category, string location, string errorMessage, Exception? ex);
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
    public void QueuedToPersistencePipeline(int nodeCount, int relationshipCount) => PipelineLogMessages.QueuedToPersistencePipeline(logger, nodeCount, relationshipCount);
    public void PersistenceLoopStarted() => PipelineLogMessages.PersistenceLoopStarted(logger);
    public void DatabaseWriteFailed(Exception ex) => PipelineLogMessages.DatabaseWriteError(logger, ex.Message, ex);
    public void ConsumerTaskFailed(Exception ex) => PipelineLogMessages.ConsumerTaskError(logger, ex.Message, ex);
    public void PersistedNodesProgress(int nodeCount) => PipelineLogMessages.PersistedNodesProgress(logger, nodeCount);
    public void PersistedRelationshipsProgress(int relationshipCount) => PipelineLogMessages.PersistedRelationshipsProgress(logger, relationshipCount);
    public void PersistedNodesFinal(int nodeCount) => PipelineLogMessages.PersistedNodesFinal(logger, nodeCount);
    public void PersistedRelationshipsFinal(int relationshipCount) => PipelineLogMessages.PersistedRelationshipsFinal(logger, relationshipCount);
    public void ScanIssueWarning(string tag, string category, string location, string errorMessage, Exception? ex) => PipelineLogMessages.ScanIssueWarning(logger, tag, category, location, errorMessage, ex);
    public void ScanIssueError(string tag, string category, string location, string errorMessage, Exception? ex) => PipelineLogMessages.ScanIssueError(logger, tag, category, location, errorMessage, ex);
}

public static partial class PostIndexLogMessages
{
    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "[PostIndex  ] Canonicalizing databases across workspace...")]
    public static partial void CanonicalizingDatabasesAcrossWorkspace(ILogger logger);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Information, Message = "[PostIndex  ] Normalizing architectural edges...")]
    public static partial void NormalizingArchitecturalEdges(ILogger logger);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Debug, Message = "[PostIndex  ] Purging phantom external services...")]
    public static partial void PurgingPhantomExternalServices(ILogger logger);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Debug, Message = "[PostIndex  ] Purging placeholder topics...")]
    public static partial void PurgingPlaceholderTopics(ILogger logger);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Debug, Message = "[PostIndex  ] Deduplicating project nodes...")]
    public static partial void DeduplicatingProjectNodes(ILogger logger);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Debug, Message = "[PostIndex  ] Loading post-index graph reachability data...")]
    public static partial void LoadingReachabilityData(ILogger logger);

    [LoggerMessage(EventId = 7007, Level = LogLevel.Debug, Message = "[PostIndex  ] Analyzing call paths and external API dependencies...")]
    public static partial void AnalyzingCallPaths(ILogger logger);

    [LoggerMessage(EventId = 7008, Level = LogLevel.Debug, Message = "[PostIndex  ] Saving reachability analysis results to database...")]
    public static partial void SavingReachabilityResults(ILogger logger);

    [LoggerMessage(EventId = 7009, Level = LogLevel.Debug, Message = "[PostIndex  ] Building file-to-project map...")]
    public static partial void BuildingFileToProjectMap(ILogger logger);

    [LoggerMessage(EventId = 7010, Level = LogLevel.Information, Message = "[PostIndex  ] Canonicalizing databases and linking projects...")]
    public static partial void CanonicalizingDatabasesAndLinkingProjects(ILogger logger);

    [LoggerMessage(EventId = 7011, Level = LogLevel.Debug, Message = "[PostIndex  ] Materializing direct project relationships...")]
    public static partial void MaterializingDirectProjectRelationships(ILogger logger);

    [LoggerMessage(EventId = 7012, Level = LogLevel.Debug, Message = "[PostIndex  ] Uploading {Count} direct project relationships...")]
    public static partial void UploadingDirectProjectRelationships(ILogger logger, int count);

    [LoggerMessage(EventId = 7013, Level = LogLevel.Debug, Message = "[PostIndex  ] Materializing transitive semantic relationships...")]
    public static partial void MaterializingTransitiveSemanticRelationships(ILogger logger);

    [LoggerMessage(EventId = 7014, Level = LogLevel.Debug, Message = "[PostIndex  ] Uploading {Count} transitive semantic relationships...")]
    public static partial void UploadingTransitiveSemanticRelationships(ILogger logger, int count);

    [LoggerMessage(EventId = 7015, Level = LogLevel.Information, Message = "[PostIndex  ] Classifying architectural layers for {ProjectCount} projects...")]
    public static partial void ClassifyingArchitecturalLayers(ILogger logger, int projectCount);

    [LoggerMessage(EventId = 7016, Level = LogLevel.Debug, Message = "[PostIndex  ] Uploading {Count} classified project nodes...")]
    public static partial void UploadingClassifiedProjectNodes(ILogger logger, int count);

    [LoggerMessage(EventId = 7017, Level = LogLevel.Information, Message = "[PostIndex  ] Normalizing architectural edges and cleaning database...")]
    public static partial void NormalizingEdgesAndCleaningDatabase(ILogger logger);

    [LoggerMessage(EventId = 7018, Level = LogLevel.Debug, Message = "[PostIndex  ] Materializing {Count} canonical Database nodes into graph...")]
    public static partial void MaterializingCanonicalDatabaseNodes(ILogger logger, int count);

    [LoggerMessage(EventId = 7019, Level = LogLevel.Debug, Message = "[PostIndex  ] Materializing {Count} direct workload->canonical database relationships...")]
    public static partial void MaterializingCanonicalDatabaseRelationships(ILogger logger, int count);
}

public readonly struct PostIndexLogger(ILogger logger)
{
    public void CanonicalizingDatabasesAcrossWorkspace() => PostIndexLogMessages.CanonicalizingDatabasesAcrossWorkspace(logger);
    public void NormalizingArchitecturalEdges() => PostIndexLogMessages.NormalizingArchitecturalEdges(logger);
    public void PurgingPhantomExternalServices() => PostIndexLogMessages.PurgingPhantomExternalServices(logger);
    public void PurgingPlaceholderTopics() => PostIndexLogMessages.PurgingPlaceholderTopics(logger);
    public void DeduplicatingProjectNodes() => PostIndexLogMessages.DeduplicatingProjectNodes(logger);
    public void LoadingReachabilityData() => PostIndexLogMessages.LoadingReachabilityData(logger);
    public void AnalyzingCallPaths() => PostIndexLogMessages.AnalyzingCallPaths(logger);
    public void SavingReachabilityResults() => PostIndexLogMessages.SavingReachabilityResults(logger);
    public void BuildingFileToProjectMap() => PostIndexLogMessages.BuildingFileToProjectMap(logger);
    public void CanonicalizingDatabasesAndLinkingProjects() => PostIndexLogMessages.CanonicalizingDatabasesAndLinkingProjects(logger);
    public void MaterializingDirectProjectRelationships() => PostIndexLogMessages.MaterializingDirectProjectRelationships(logger);
    public void UploadingDirectProjectRelationships(int count) => PostIndexLogMessages.UploadingDirectProjectRelationships(logger, count);
    public void MaterializingTransitiveSemanticRelationships() => PostIndexLogMessages.MaterializingTransitiveSemanticRelationships(logger);
    public void UploadingTransitiveSemanticRelationships(int count) => PostIndexLogMessages.UploadingTransitiveSemanticRelationships(logger, count);
    public void ClassifyingArchitecturalLayers(int projectCount) => PostIndexLogMessages.ClassifyingArchitecturalLayers(logger, projectCount);
    public void UploadingClassifiedProjectNodes(int count) => PostIndexLogMessages.UploadingClassifiedProjectNodes(logger, count);
    public void NormalizingEdgesAndCleaningDatabase() => PostIndexLogMessages.NormalizingEdgesAndCleaningDatabase(logger);
    public void MaterializingCanonicalDatabaseNodes(int count) => PostIndexLogMessages.MaterializingCanonicalDatabaseNodes(logger, count);
    public void MaterializingCanonicalDatabaseRelationships(int count) => PostIndexLogMessages.MaterializingCanonicalDatabaseRelationships(logger, count);
}

public static partial class IntentLogMessages
{
    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "[Intent     ] Fast-applied cached architectural intents to {Count} nodes.")]
    public static partial void FastAppliedCachedIntents(ILogger logger, int count);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Information, Message = "[Intent     ] Materialized {DomainCount} topological AST domains across {ServiceCount} services.")]
    public static partial void MaterializedTopologicalDomains(ILogger logger, int domainCount, int serviceCount);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Debug, Message = "[Intent     ] Note on applying cached intents: {ErrorMessage}")]
    public static partial void CachedIntentsApplyNote(ILogger logger, string errorMessage);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Information, Message = "[Intent     ] Intent distillation model not available; skipping intent distillation.")]
    public static partial void ModelNotAvailableSkipping(ILogger logger);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Information, Message = "[Intent     ] Compute Device: {Device}")]
    public static partial void ComputeDevice(ILogger logger, string device);

    [LoggerMessage(EventId = 8006, Level = LogLevel.Information, Message = "[Intent     ] Concurrency: {Concurrency}x ({Reason})")]
    public static partial void ConcurrencyInfo(ILogger logger, int concurrency, string reason);

    [LoggerMessage(EventId = 8007, Level = LogLevel.Information, Message = "[Intent     ] Distilling Bounded Contexts & Aggregates across {ServiceCount} service(s) on {Device} (mode: {Mode}, concurrency: {Concurrency}x)...")]
    public static partial void DistillingBoundedContexts(ILogger logger, int serviceCount, string device, string mode, int concurrency);

    [LoggerMessage(EventId = 8008, Level = LogLevel.Information, Message = "  - [{ServiceName}] -> Context: {BoundedContext} | Aggregates: [{Aggregates}] | Domain: {Domain}")]
    public static partial void DistilledServiceContext(ILogger logger, string serviceName, string boundedContext, string aggregates, string domain);

    [LoggerMessage(EventId = 8009, Level = LogLevel.Warning, Message = "  [!] Failed to distill context for {ServiceName}: {ErrorMessage}")]
    public static partial void DistillServiceContextFailed(ILogger logger, string serviceName, string errorMessage);

    [LoggerMessage(EventId = 8010, Level = LogLevel.Information, Message = "[Intent     ] Matching Bounded Contexts against {DomainCount} configured domains from .codeexplorer/domains.json...")]
    public static partial void MatchingConfiguredDomains(ILogger logger, int domainCount);

    [LoggerMessage(EventId = 8011, Level = LogLevel.Information, Message = "[Intent     ] Grouped into {DomainCount} Business Domains via configuration:")]
    public static partial void GroupedIntoConfiguredDomains(ILogger logger, int domainCount);

    [LoggerMessage(EventId = 8012, Level = LogLevel.Information, Message = "  - [{DomainName}] ({ServiceCount} services): {Description}")]
    public static partial void DomainSummaryItem(ILogger logger, string domainName, int serviceCount, string description);

    [LoggerMessage(EventId = 8013, Level = LogLevel.Information, Message = "[Intent     ] Synthesizing Macro-Domains via LLM from {ContextCount} Bounded Contexts on {Device}...")]
    public static partial void SynthesizingMacroDomainsViaLlm(ILogger logger, int contextCount, string device);

    [LoggerMessage(EventId = 8014, Level = LogLevel.Warning, Message = "[Intent     ] Note on LLM macro-domain synthesis: {ErrorMessage}. Falling back to graph topology synthesis.")]
    public static partial void MacroDomainLlmFallback(ILogger logger, string errorMessage);

    [LoggerMessage(EventId = 8015, Level = LogLevel.Information, Message = "[Intent     ] Discovered {DomainCount} Business Domains via LLM synthesis:")]
    public static partial void DiscoveredMacroDomainsViaLlm(ILogger logger, int domainCount);

    [LoggerMessage(EventId = 8016, Level = LogLevel.Information, Message = "[Intent     ] Categorized {ProjectCount} remaining projects via ontology role/directory.")]
    public static partial void CategorizedRemainingProjects(ILogger logger, int projectCount);

    [LoggerMessage(EventId = 8017, Level = LogLevel.Information, Message = "[Intent     ] Persisted macro-domain assignments for {ServiceCount} services to graph database.")]
    public static partial void PersistedMacroDomainAssignments(ILogger logger, int serviceCount);

    [LoggerMessage(EventId = 8018, Level = LogLevel.Information, Message = "[Intent     ] Target Service [{ServiceName}] mapped to Domain: [{Domain}] ({Role})")]
    public static partial void TargetServiceMappedToDomain(ILogger logger, string serviceName, string domain, string role);

    [LoggerMessage(EventId = 8019, Level = LogLevel.Information, Message = "[Intent     ] Purged {OrphanCount} orphaned/non-project file intents (including .sql).")]
    public static partial void PurgedOrphanedIntents(ILogger logger, int orphanCount);

    [LoggerMessage(EventId = 8020, Level = LogLevel.Information, Message = "[Intent     ] Realigned {UpdatedCount} cached file intents to their parent project macro-domain.")]
    public static partial void RealignedCachedIntents(ILogger logger, int updatedCount);

    [LoggerMessage(EventId = 8021, Level = LogLevel.Information, Message = "[Intent     ] Architectural intent distillation complete. Enriched {AppliedCount} nodes in knowledge graph.")]
    public static partial void IntentDistillationComplete(ILogger logger, int appliedCount);

    [LoggerMessage(EventId = 8022, Level = LogLevel.Warning, Message = "[Intent     ] Intent distillation encountered an issue: {ErrorMessage}")]
    public static partial void IntentDistillationFailed(ILogger logger, string errorMessage);

    [LoggerMessage(EventId = 8023, Level = LogLevel.Information, Message = "[Intent     ] Architectural intent model '{ModelFileName}' (~940MB) not found. Run 'ce model download' or set CODE_INTENT_AUTO_DOWNLOAD=1 to download it.")]
    public static partial void ModelNotFoundHint(ILogger logger, string modelFileName);

    [LoggerMessage(EventId = 8024, Level = LogLevel.Information, Message = "[Intent     ] Downloading {ModelFileName} from {DownloadUrl}...")]
    public static partial void DownloadingModelStarted(ILogger logger, string modelFileName, string downloadUrl);

    [LoggerMessage(EventId = 8025, Level = LogLevel.Information, Message = "[Intent     ] Downloading model: {CurrentMb} MB / {TotalMb} MB ({Percentage:F0}%)...")]
    public static partial void DownloadingModelProgressWithTotal(ILogger logger, long currentMb, long totalMb, double percentage);

    [LoggerMessage(EventId = 8026, Level = LogLevel.Information, Message = "[Intent     ] Downloading model: {CurrentMb} MB...")]
    public static partial void DownloadingModelProgress(ILogger logger, long currentMb);

    [LoggerMessage(EventId = 8027, Level = LogLevel.Information, Message = "[Intent     ] Successfully downloaded model to '{DestinationPath}'.")]
    public static partial void DownloadingModelCompleted(ILogger logger, string destinationPath);

    [LoggerMessage(EventId = 8028, Level = LogLevel.Warning, Message = "[Intent     ] Failed to download model: {ErrorMessage}")]
    public static partial void DownloadingModelFailed(ILogger logger, string errorMessage);

    [LoggerMessage(EventId = 8029, Level = LogLevel.Information, Message = "{Message}")]
    public static partial void AgenticTurnMessage(ILogger logger, string message);
}

public readonly struct IntentLogger(ILogger logger)
{
    public void FastAppliedCachedIntents(int count) => IntentLogMessages.FastAppliedCachedIntents(logger, count);
    public void MaterializedTopologicalDomains(int domainCount, int serviceCount) => IntentLogMessages.MaterializedTopologicalDomains(logger, domainCount, serviceCount);
    public void CachedIntentsApplyNote(string errorMessage) => IntentLogMessages.CachedIntentsApplyNote(logger, errorMessage);
    public void ModelNotAvailableSkipping() => IntentLogMessages.ModelNotAvailableSkipping(logger);
    public void ComputeDevice(string device) => IntentLogMessages.ComputeDevice(logger, device);
    public void ConcurrencyInfo(int concurrency, string reason) => IntentLogMessages.ConcurrencyInfo(logger, concurrency, reason);
    public void DistillingBoundedContexts(int serviceCount, string device, string mode, int concurrency) => IntentLogMessages.DistillingBoundedContexts(logger, serviceCount, device, mode, concurrency);
    public void DistilledServiceContext(string serviceName, string boundedContext, string aggregates, string domain) => IntentLogMessages.DistilledServiceContext(logger, serviceName, boundedContext, aggregates, domain);
    public void DistillServiceContextFailed(string serviceName, string errorMessage) => IntentLogMessages.DistillServiceContextFailed(logger, serviceName, errorMessage);
    public void MatchingConfiguredDomains(int domainCount) => IntentLogMessages.MatchingConfiguredDomains(logger, domainCount);
    public void GroupedIntoConfiguredDomains(int domainCount) => IntentLogMessages.GroupedIntoConfiguredDomains(logger, domainCount);
    public void DomainSummaryItem(string domainName, int serviceCount, string description) => IntentLogMessages.DomainSummaryItem(logger, domainName, serviceCount, description);
    public void SynthesizingMacroDomainsViaLlm(int contextCount, string device) => IntentLogMessages.SynthesizingMacroDomainsViaLlm(logger, contextCount, device);
    public void MacroDomainLlmFallback(string errorMessage) => IntentLogMessages.MacroDomainLlmFallback(logger, errorMessage);
    public void DiscoveredMacroDomainsViaLlm(int domainCount) => IntentLogMessages.DiscoveredMacroDomainsViaLlm(logger, domainCount);
    public void CategorizedRemainingProjects(int projectCount) => IntentLogMessages.CategorizedRemainingProjects(logger, projectCount);
    public void PersistedMacroDomainAssignments(int serviceCount) => IntentLogMessages.PersistedMacroDomainAssignments(logger, serviceCount);
    public void TargetServiceMappedToDomain(string serviceName, string domain, string role) => IntentLogMessages.TargetServiceMappedToDomain(logger, serviceName, domain, role);
    public void PurgedOrphanedIntents(int orphanCount) => IntentLogMessages.PurgedOrphanedIntents(logger, orphanCount);
    public void RealignedCachedIntents(int updatedCount) => IntentLogMessages.RealignedCachedIntents(logger, updatedCount);
    public void IntentDistillationComplete(int appliedCount) => IntentLogMessages.IntentDistillationComplete(logger, appliedCount);
    public void IntentDistillationFailed(string errorMessage) => IntentLogMessages.IntentDistillationFailed(logger, errorMessage);
    public void ModelNotFoundHint(string modelFileName) => IntentLogMessages.ModelNotFoundHint(logger, modelFileName);
    public void DownloadingModelStarted(string modelFileName, string downloadUrl) => IntentLogMessages.DownloadingModelStarted(logger, modelFileName, downloadUrl);
    public void DownloadingModelProgressWithTotal(long currentMb, long totalMb, double percentage) => IntentLogMessages.DownloadingModelProgressWithTotal(logger, currentMb, totalMb, percentage);
    public void DownloadingModelProgress(long currentMb) => IntentLogMessages.DownloadingModelProgress(logger, currentMb);
    public void DownloadingModelCompleted(string destinationPath) => IntentLogMessages.DownloadingModelCompleted(logger, destinationPath);
    public void DownloadingModelFailed(string errorMessage) => IntentLogMessages.DownloadingModelFailed(logger, errorMessage);
    public void AgenticTurnMessage(string message) => IntentLogMessages.AgenticTurnMessage(logger, message);
}
