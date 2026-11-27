using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

internal static class ScheduledJobEnricher
{
    private const string ScheduledToken = "Scheduled";
    private const string CronToken = "Cron";
    private const string IntervalToken = "Interval";
    private const string TimeoutToken = "Timeout";
    private const string AsCronTaskToken = "AsCronTask";
    private const string AsPeriodicTaskToken = "AsPeriodicTask";
    private const string ProcessorToken = "Processor";
    private const string ProcessToken = "Process";
    private const string SharedTaskToken = "shared_task";
    private const string CeleryTaskToken = ".task";
    private const string ScheduledJobAttrToken = "scheduled_job";
    private const string WorkflowDefnToken = "workflow.defn";
    private const string WorkflowRunToken = "WorkflowRun";
    private const string ActivityDefnToken = "activity.defn";
    private const string ActivityAttrToken = "Activity";
    private const string BackgroundServiceToken = "BackgroundService";
    private const string HostedServiceToken = "IHostedService";
    private const string QuartzJobInterface = "IJob";
    private const string RecurringJobPrefix = "RecurringJob.AddOrUpdate";
    private const string BackgroundJobEnqueue = "BackgroundJob.Enqueue";
    private const string BackgroundJobSchedule = "BackgroundJob.Schedule";
    private const string GoCronAddFunc = ".AddFunc";
    private const string ExecuteAsyncMethod = "ExecuteAsync";
    private const string StartAsyncMethod = "StartAsync";
    private const string ExecuteMethod = "Execute";
    private const string LowerExecuteMethod = "execute";
    private const string ProcessMethod = "process";
    private const string RunMethod = "run";
    private const string FrameworkSpringScheduled = "spring-scheduled";
    private const string FrameworkNestSchedule = "nestjs-schedule";
    private const string FrameworkSymfonySched = "symfony-scheduler";
    private const string FrameworkBullMq = "bullmq";
    private const string FrameworkCelery = "celery";
    private const string FrameworkTemporal = "temporal";
    private const string FrameworkHostedService = "hosted-service";
    private const string FrameworkQuartz = "quartz";
    private const string FrameworkHangfire = "hangfire";
    private const string FrameworkGoCron = "robfig-cron";

    internal sealed record JobEnrichmentOutput(List<JobNode> Jobs, List<Relationship> Relationships);

    public static JobEnrichmentOutput EnrichJobs(
        Layer4Result? l4Result,
        Layer5Context ctx,
        Dictionary<string, List<Reference>> refsByScopeId,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var jobsById = new Dictionary<string, JobNode>(StringComparer.Ordinal);
        var rels = new List<Relationship>();
        if (l4Result == null) return new JobEnrichmentOutput([], rels);

        var syntaxStruct = ctx.SyntaxStructure;
        if (syntaxStruct == null) return new JobEnrichmentOutput([], rels);

        var filesByPath = SemanticEnrichmentHelpers.CollectFilesByPath(ctx.FilesStructure);
        var modules = SemanticEnrichmentHelpers.CollectModules(l4Result.SemanticStructure);
        var index = SemanticEnrichmentHelpers.CollectTypesAndMethods(syntaxStruct);

        foreach (var (typeId, tn) in index.TypesById)
        {
            if (tn.IsTest || tn.TypeKind == TypeKind.Interface || !index.MethodsByTypeId.TryGetValue(typeId, out var methods)) continue;

            EnrichTypeLevelJobContracts(ctx, tn, methods, filesByPath, modules, jobsById, rels, existingRefRels);

            for (var i = 0; i < methods.Count; i++)
            {
                var fn = methods[i];
                if (fn.IsTest) continue;
                EnrichMethodAnnotationsAndCalls(ctx, tn, fn, refsByScopeId, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }

        for (var i = 0; i < index.TopLevelFunctions.Count; i++)
        {
            var fn = index.TopLevelFunctions[i];
            if (fn.IsTest) continue;
            EnrichMethodAnnotationsAndCalls(ctx, null, fn, refsByScopeId, filesByPath, modules, jobsById, rels, existingRefRels);
        }

        return new JobEnrichmentOutput(jobsById.Values.ToList(), rels);
    }

    private static void EnrichTypeLevelJobContracts(
        Layer5Context ctx,
        TypeNode tn,
        List<FunctionNode> methods,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, JobNode> jobsById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var contracts = $"{tn.Extends} {tn.Implements}".Trim();
        var clsAttrs = tn.Attributes ?? string.Empty;

        if (contracts.Contains(BackgroundServiceToken, StringComparison.Ordinal) ||
            contracts.Contains(HostedServiceToken, StringComparison.Ordinal))
        {
            var fn = FindMethodByName(methods, ExecuteAsyncMethod) ?? FindMethodByName(methods, StartAsyncMethod);
            if (fn != null && !fn.IsTest)
            {
                EmitJob(ctx, fn, tn.Name, JobKind.BackgroundWorker, null, null, FrameworkHostedService, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }
        else if (HasStandaloneContract(contracts, QuartzJobInterface))
        {
            var fn = FindMethodByName(methods, ExecuteMethod) ?? FindMethodByName(methods, LowerExecuteMethod);
            if (fn != null && !fn.IsTest)
            {
                EmitJob(ctx, fn, tn.Name, JobKind.Cron, null, null, FrameworkQuartz, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }
        else if (clsAttrs.Contains(ProcessorToken, StringComparison.Ordinal))
        {
            var fn = FindMethodByName(methods, ProcessMethod) ?? FindDecoratedMethod(methods, ProcessToken);
            if (fn != null && !fn.IsTest)
            {
                EmitJob(ctx, fn, tn.Name, JobKind.FireAndForget, null, ExtractHeaderLiteral(fn), FrameworkBullMq, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }
        else if (clsAttrs.Contains(AsCronTaskToken, StringComparison.Ordinal) ||
                 clsAttrs.Contains(AsPeriodicTaskToken, StringComparison.Ordinal))
        {
            var fn = methods.FirstOrDefault(m => !m.IsTest);
            if (fn != null)
            {
                EmitJob(ctx, fn, tn.Name, JobKind.Cron, ExtractHeaderLiteral(fn), null, FrameworkSymfonySched, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }
        else if (clsAttrs.Contains(WorkflowDefnToken, StringComparison.OrdinalIgnoreCase))
        {
            var fn = FindMethodByName(methods, RunMethod) ?? methods.FirstOrDefault(m => !m.IsTest);
            if (fn != null)
            {
                EmitJob(ctx, fn, tn.Name, JobKind.Workflow, null, null, FrameworkTemporal, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }
    }

    private static void EnrichMethodAnnotationsAndCalls(
        Layer5Context ctx,
        TypeNode? ownerType,
        FunctionNode fn,
        Dictionary<string, List<Reference>> refsByScopeId,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, JobNode> jobsById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var attrs = fn.Attributes ?? string.Empty;
        var displayName = ownerType != null ? $"{ownerType.Name}.{fn.Name}" : fn.Name;

        if (attrs.Length > 0)
        {
            var schedExpr = ExtractHeaderLiteral(fn);
            if (attrs.Contains(ScheduledToken, StringComparison.Ordinal) ||
                attrs.Contains(ScheduledJobAttrToken, StringComparison.OrdinalIgnoreCase))
            {
                EmitJob(ctx, fn, displayName, JobKind.Cron, schedExpr, null, FrameworkSpringScheduled, filesByPath, modules, jobsById, rels, existingRefRels);
            }
            else if (HasStandaloneContract(attrs, CronToken))
            {
                EmitJob(ctx, fn, displayName, JobKind.Cron, schedExpr, null, FrameworkNestSchedule, filesByPath, modules, jobsById, rels, existingRefRels);
            }
            else if (HasStandaloneContract(attrs, IntervalToken) || HasStandaloneContract(attrs, TimeoutToken))
            {
                EmitJob(ctx, fn, displayName, JobKind.Recurring, schedExpr, null, FrameworkNestSchedule, filesByPath, modules, jobsById, rels, existingRefRels);
            }
            else if (attrs.Contains(SharedTaskToken, StringComparison.OrdinalIgnoreCase) ||
                     attrs.Contains(CeleryTaskToken, StringComparison.OrdinalIgnoreCase))
            {
                EmitJob(ctx, fn, displayName, JobKind.FireAndForget, null, schedExpr, FrameworkCelery, filesByPath, modules, jobsById, rels, existingRefRels);
            }
            else if (attrs.Contains(WorkflowRunToken, StringComparison.Ordinal))
            {
                EmitJob(ctx, fn, ownerType?.Name ?? fn.Name, JobKind.Workflow, null, null, FrameworkTemporal, filesByPath, modules, jobsById, rels, existingRefRels);
            }
            else if (attrs.Contains(ActivityDefnToken, StringComparison.OrdinalIgnoreCase) ||
                     HasStandaloneContract(attrs, ActivityAttrToken))
            {
                EmitJob(ctx, fn, displayName, JobKind.Activity, null, null, FrameworkTemporal, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }

        var scopeRefs = SemanticEnrichmentHelpers.GetScopeReferences(fn.Id, fn.References, refsByScopeId);
        for (var i = 0; i < scopeRefs.Count; i++)
        {
            var r = scopeRefs[i];
            var qualified = r.HasExplicitReceiver ? $"{r.ReceiverName}.{r.MemberName}" : r.TargetName;
            if (string.IsNullOrEmpty(qualified)) continue;

            if (qualified.Contains(RecurringJobPrefix, StringComparison.Ordinal))
            {
                var jobKey = ExtractFirstCallArgAtLine(fn, r.Line) ?? displayName;
                EmitJob(ctx, fn, jobKey, JobKind.Cron, null, null, FrameworkHangfire, filesByPath, modules, jobsById, rels, existingRefRels);
            }
            else if (qualified.Contains(BackgroundJobEnqueue, StringComparison.Ordinal) ||
                     qualified.Contains(BackgroundJobSchedule, StringComparison.Ordinal))
            {
                EmitJob(ctx, fn, displayName, JobKind.FireAndForget, null, null, FrameworkHangfire, filesByPath, modules, jobsById, rels, existingRefRels);
            }
            else if (qualified.EndsWith(GoCronAddFunc, StringComparison.Ordinal))
            {
                var cronSpec = ExtractFirstCallArgAtLine(fn, r.Line);
                EmitJob(ctx, fn, displayName, JobKind.Cron, cronSpec, null, FrameworkGoCron, filesByPath, modules, jobsById, rels, existingRefRels);
            }
        }
    }

    private static FunctionNode? FindMethodByName(List<FunctionNode> methods, string targetName)
    {
        for (var i = 0; i < methods.Count; i++)
        {
            var m = methods[i];
            if (string.Equals(m.Name, targetName, StringComparison.Ordinal) ||
                m.Name.EndsWith('.' + targetName, StringComparison.Ordinal))
            {
                return m;
            }
        }
        return null;
    }

    private static FunctionNode? FindDecoratedMethod(List<FunctionNode> methods, string attrToken)
    {
        for (var i = 0; i < methods.Count; i++)
        {
            if (methods[i].Attributes?.Contains(attrToken, StringComparison.Ordinal) == true)
            {
                return methods[i];
            }
        }
        return null;
    }

    private static bool HasStandaloneContract(string text, string word)
    {
        var idx = text.IndexOf(word, StringComparison.Ordinal);
        if (idx < 0) return false;
        var beforeOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
        var afterIdx = idx + word.Length;
        var afterOk = afterIdx >= text.Length || !char.IsLetterOrDigit(text[afterIdx]);
        return beforeOk && afterOk;
    }

    private static string? ExtractHeaderLiteral(FunctionNode fn)
    {
        for (var i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is CallLiteralNode lit && lit.Line <= fn.StartLine + 1 && !string.IsNullOrWhiteSpace(lit.Value))
            {
                return lit.Value.Trim('"', '\'', ' ');
            }
        }
        return null;
    }

    private static string? ExtractFirstCallArgAtLine(FunctionNode fn, int line)
    {
        for (var i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is CallArgumentNode ca && ca.Line == line && ca.ArgIndex == 0 && !string.IsNullOrWhiteSpace(ca.ResolvedValue))
            {
                return ca.ResolvedValue.Trim('"', '\'', ' ');
            }
        }
        return null;
    }

    private static void EmitJob(
        Layer5Context ctx,
        FunctionNode fn,
        string jobName,
        JobKind subKind,
        string? scheduleExpr,
        string? queue,
        string framework,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, JobNode> jobsById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var kindKey = subKind.ToCanonicalString();
        var jobId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Job}:{kindKey}:{jobName}";
        var fileNorm = fn.FilePath.Replace('\\', '/');

        if (!jobsById.TryGetValue(jobId, out var node))
        {
            node = new JobNode(jobId, jobName, fileNorm, subKind)
            {
                ScheduleExpr = scheduleExpr,
                Queue = queue,
                Framework = framework,
                FilePath = fileNorm,
                StartLine = fn.StartLine
            };
            jobsById[jobId] = node;

            if (filesByPath.TryGetValue(fileNorm, out var fNode))
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new DeclaredInRelationship(jobId, fNode.Id), rels, existingRefRels);
            }

            var ownerMod = SemanticEnrichmentHelpers.FindOwningModule(fileNorm, modules);
            if (ownerMod != null)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new ContainsRelationship(ownerMod.Id, jobId), rels, existingRefRels);
                if (ownerMod is WorkloadNode)
                {
                    SemanticEnrichmentHelpers.AddUniqueRel(new ExposesRelationship(ownerMod.Id, jobId), rels, existingRefRels);
                }
            }
        }

        SemanticEnrichmentHelpers.AddUniqueRel(new TriggersRelationship(jobId, fn.Id), rels, existingRefRels);
    }
}
