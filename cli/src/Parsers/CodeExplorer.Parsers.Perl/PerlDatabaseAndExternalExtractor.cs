using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parsers.Perl;

internal static class PerlDatabaseAndExternalExtractor
{
    private const string DbTypeRelational = "relational";
    private const string EngineMySql = "MySQL";
    private const string OpSelect = "SELECT";
    private const string OpInsert = "INSERT";
    private const string OpUpdate = "UPDATE";
    private const string OpDelete = "DELETE";
    private const string FilePathKey = "file_path";
    private const string NameKey = "name";
    private const string TargetKey = "target";
    private const string DomainOrServiceKey = "domain_or_service";
    private const string IsExternalKey = "is_external";
    private const string GroupByWithoutOrderByKey = "has_group_by_without_order_by";
    private const string TrueString = "true";
    private const string GroupByKeyword = "GROUP BY";
    private const string OrderByKeyword = "ORDER BY";
    private const string InsertPrefix = "INSERT";
    private const string ReplacePrefix = "REPLACE";
    private const string UpdatePrefix = "UPDATE";
    private const string DeletePrefix = "DELETE";
    private const string DbiMarker = "DBI";
    private const string DbhMarker = "$dbh";
    private const string ProtocolMreg = "MREG";
    private const string ProtocolSmtp = "SMTP";
    private const string ProtocolHttp = "HTTP";
    private const string MregDomain = "mreg";
    private const string SmtpDomain = "smtp-mailer";
    private const string HttpDomain = "external-http";
    private const string MethodSend = "SEND";
    private const string MethodRequest = "REQUEST";
    private const string RootSlash = "/";
    private const char BacktickChar = '`';
    private const char SingleQuoteChar = '\'';
    private const char DoubleQuoteChar = '"';

    private static readonly char[] SqlIdentifierTrimChars = [BacktickChar, SingleQuoteChar, DoubleQuoteChar, ' ', '\t', '\r', '\n'];

    private static readonly Regex DbiQuotedQueryRegex = new(
        @"->\s*(?:prepare|prepare_cached|do|selectall_arrayref|selectall_hashref|selectrow_array|selectrow_arrayref|selectrow_hashref|selectcol_arrayref)\s*\(\s*(?:qq\s*[\{\(\[/](?<sql>[^\}\)\]/]{1,8000})[\}\)\]/]|q\s*[\{\(\[/](?<sql>[^\}\)\]/]{1,8000})[\}\)\]/]|'(?<sql>(?:\\.|[^'\\]){1,8000})'|""(?<sql>(?:\\.|[^""\\]){1,8000})"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DbiHeredocQueryRegex = new(
        @"->\s*(?:prepare|prepare_cached|do|selectall_arrayref|selectall_hashref|selectrow_array|selectrow_arrayref|selectrow_hashref|selectcol_arrayref)\s*\(\s*<<\s*['""]?(?<tag>[A-Za-z0-9_]+)\b['""]?[^\n]*\n(?<sql>[\s\S]{1,8000}?)\n[ \t]*\k<tag>\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SqlVariableAssignmentRegex = new(
        @"\$(?:sql|query|stmt)\s*=\s*(?:qq\s*[\{\(\[/](?<sql>\s*(?:SELECT|INSERT|UPDATE|DELETE|REPLACE)\b[^\}\)\]/]{1,8000})[\}\)\]/]|'(?<sql>\s*(?:SELECT|INSERT|UPDATE|DELETE|REPLACE)\b(?:\\.|[^'\\]){1,8000})'|""(?<sql>\s*(?:SELECT|INSERT|UPDATE|DELETE|REPLACE)\b(?:\\.|[^""\\]){1,8000})"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SqlTableExtractRegex = new(
        @"\b(?:FROM|JOIN|INTO|(?<!DUPLICATE\s+KEY\s+)UPDATE|TABLE)\s+[`""']?(?<tbl>[a-zA-Z_][a-zA-Z0-9_]*)[`""']?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PtfXClientCallRegex = new(
        @"(?:PTF::XClient|\$(?:xclient|mreg|client))\s*->\s*(?<method>send|request|execute|call|command|[a-zA-Z0-9_]+)\s*\(\s*['""]?(?<cmd>[a-zA-Z0-9_.:-]+)['""]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PerlConstDeclRegex = new(
        @"(?:\buse\s+constant\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=>\s*['""](?<val>[^'""]+)['""]|(?<name>[A-Z_][A-Z0-9_]{2,})\s*=>\s*['""](?<val>[^'""]+)['""]|\b(?:my|our)\s+\$(?<name>[A-Z_][A-Z0-9_]{2,})\s*=\s*['""](?<val>[^'""]+)['""])",
        RegexOptions.Compiled);

    private static readonly Regex PerlMailRegex = new(
        @"\b(?:Mail::Sendmail|MIME::Lite|\bsendmail\s*\()",
        RegexOptions.Compiled);

    private static readonly Regex PerlHttpClientRegex = new(
        @"\b(?:LWP::UserAgent|WWW::Mechanize)\b",
        RegexOptions.Compiled);

    private static readonly HashSet<string> SqlIgnoredWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "where", "set", "values", "and", "or", "null", "not", "in", "as", "on", "dual", "exists"
    };

    internal static void ExtractDatabaseAndExternalCalls(
        string content,
        string commentMasked,
        int[] lineOffsets,
        TypeNode moduleNode,
        List<PerlFileParser.PerlSubSpan> subSpans,
        FileNode fileNode,
        string workspaceId,
        string relativePath)
    {
        if (content.Contains(DbiMarker, StringComparison.Ordinal) ||
            content.Contains(DbhMarker, StringComparison.OrdinalIgnoreCase))
        {
            EnsureMySqlDatabaseNode(fileNode, workspaceId, relativePath);
        }

        var localConstants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match cm in PerlConstDeclRegex.Matches(commentMasked))
        {
            var cName = cm.Groups["name"].Value.Trim();
            var cVal = cm.Groups["val"].Value.Trim();
            if (!string.IsNullOrEmpty(cName) && !string.IsNullOrEmpty(cVal))
            {
                localConstants[cName] = cVal;
                ConstantRegistry.Register(null, cName, cVal);
                ConstantRegistry.Register(null, $"{moduleNode.Name}.{cName}", cVal);
            }
        }

        ProcessSqlMatches(DbiQuotedQueryRegex.Matches(commentMasked), lineOffsets, moduleNode, subSpans, fileNode, workspaceId, relativePath);
        ProcessSqlMatches(DbiHeredocQueryRegex.Matches(commentMasked), lineOffsets, moduleNode, subSpans, fileNode, workspaceId, relativePath);
        ProcessSqlMatches(SqlVariableAssignmentRegex.Matches(commentMasked), lineOffsets, moduleNode, subSpans, fileNode, workspaceId, relativePath);

        foreach (Match xm in PtfXClientCallRegex.Matches(commentMasked))
        {
            var method = xm.Groups["method"].Value.Trim();
            if (method.Equals(PerlFileParser.NewMethodName, StringComparison.OrdinalIgnoreCase)) continue;

            var cmd = xm.Groups["cmd"].Value.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) cmd = method;
            cmd = ResolvePerlCommandConstant(cmd, localConstants);

            var (line, col) = PerlFileParser.GetLineAndColumn(lineOffsets, xm.Index);
            var enclosingFn = FindEnclosingSub(xm.Index, subSpans);
            var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:mreg:{cmd}:{line}";
            if (!fileNode.Children.Any(c => c.Id == extId))
            {
                var extNode = new ExternalServiceNode(extId, $"MREG {cmd}", ProtocolMreg, MregDomain, cmd)
                {
                    FilePath = relativePath,
                    StartLine = line,
                    StartCol = col,
                    CallerSymbolId = enclosingFn?.Id ?? moduleNode.Id,
                    Url = cmd,
                    Method = ProtocolMreg,
                    IsExternal = true
                };
                extNode.CustomProperties ??= new();
                extNode.CustomProperties[FilePathKey] = relativePath;
                extNode.CustomProperties[TargetKey] = cmd;
                extNode.CustomProperties[DomainOrServiceKey] = MregDomain;
                extNode.CustomProperties[IsExternalKey] = TrueString;
                fileNode.Children.Add(extNode);

                var callerId = enclosingFn?.Id ?? moduleNode.Id;
                fileNode.References.Add(new Reference(callerId, extId, OntologyConstants.Relationships.Calls, line, col));
            }
        }

        foreach (Match mm in PerlMailRegex.Matches(commentMasked))
        {
            var (line, col) = PerlFileParser.GetLineAndColumn(lineOffsets, mm.Index);
            var enclosingFn = FindEnclosingSub(mm.Index, subSpans);
            var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:smtp:{line}";
            if (!fileNode.Children.Any(c => c.Id == extId))
            {
                var extNode = new ExternalServiceNode(extId, SmtpDomain, ProtocolSmtp, SmtpDomain, RootSlash)
                {
                    FilePath = relativePath,
                    StartLine = line,
                    StartCol = col,
                    CallerSymbolId = enclosingFn?.Id ?? moduleNode.Id,
                    Method = MethodSend,
                    IsExternal = true
                };
                fileNode.Children.Add(extNode);
                var callerId = enclosingFn?.Id ?? moduleNode.Id;
                fileNode.References.Add(new Reference(callerId, extId, OntologyConstants.Relationships.Calls, line, col));
            }
        }

        foreach (Match hm in PerlHttpClientRegex.Matches(commentMasked))
        {
            var (line, col) = PerlFileParser.GetLineAndColumn(lineOffsets, hm.Index);
            var enclosingFn = FindEnclosingSub(hm.Index, subSpans);
            var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:http:{line}";
            if (!fileNode.Children.Any(c => c.Id == extId))
            {
                var extNode = new ExternalServiceNode(extId, HttpDomain, ProtocolHttp, HttpDomain, RootSlash)
                {
                    FilePath = relativePath,
                    StartLine = line,
                    StartCol = col,
                    CallerSymbolId = enclosingFn?.Id ?? moduleNode.Id,
                    Method = MethodRequest,
                    IsExternal = true
                };
                fileNode.Children.Add(extNode);
                var callerId = enclosingFn?.Id ?? moduleNode.Id;
                fileNode.References.Add(new Reference(callerId, extId, OntologyConstants.Relationships.Calls, line, col));
            }
        }
    }

    private static void ProcessSqlMatches(
        MatchCollection matches,
        int[] lineOffsets,
        TypeNode moduleNode,
        List<PerlFileParser.PerlSubSpan> subSpans,
        FileNode fileNode,
        string workspaceId,
        string relativePath)
    {
        foreach (Match m in matches)
        {
            var rawSql = m.Groups["sql"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawSql)) continue;

            var tables = ExtractTablesFromSql(rawSql);
            if (tables.Count == 0) continue;

            var op = DetermineSqlOperation(rawSql);
            var hasUnorderedGroupBy = rawSql.Contains(GroupByKeyword, StringComparison.OrdinalIgnoreCase) &&
                                      !rawSql.Contains(OrderByKeyword, StringComparison.OrdinalIgnoreCase);
            var (line, col) = PerlFileParser.GetLineAndColumn(lineOffsets, m.Index);
            var enclosingFn = FindEnclosingSub(m.Index, subSpans);

            foreach (var tbl in tables)
            {
                EnsureMySqlDatabaseNode(fileNode, workspaceId, relativePath);
                var tableNode = EnsureTableNode(fileNode, workspaceId, relativePath, tbl);
                var edgeKind = op == OpSelect
                    ? OntologyConstants.Relationships.ReadsFrom
                    : OntologyConstants.Relationships.WritesTo;

                var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{tbl.ToLowerInvariant()}:{line}:{col}";
                if (!fileNode.Children.Any(c => c.Id == qId))
                {
                    var queryNode = new QueryNode(qId, $"{op} {tbl}", rawSql, relativePath, line, line)
                    {
                        FilePath = relativePath,
                        Line = line,
                        LineEnd = line,
                        Operation = op,
                        Verb = op,
                        EvidenceKind = OntologyConstants.EvidenceKinds.Query
                    };
                    queryNode.CustomProperties ??= new();
                    queryNode.CustomProperties[FilePathKey] = relativePath;
                    if (hasUnorderedGroupBy)
                    {
                        queryNode.CustomProperties[GroupByWithoutOrderByKey] = TrueString;
                    }
                    fileNode.Children.Add(queryNode);
                    fileNode.References.Add(new Reference(qId, tbl, edgeKind, line, col));
                }

                if (enclosingFn != null)
                {
                    fileNode.References.Add(new Reference(qId, enclosingFn.Id, OntologyConstants.Relationships.AccessedBy, line, col));
                    enclosingFn.References.Add(new Reference(enclosingFn.Id, tbl, edgeKind, line, col));
                }

                fileNode.References.Add(new Reference(qId, moduleNode.Id, OntologyConstants.Relationships.AccessedBy, line, col));
                fileNode.References.Add(new Reference(moduleNode.Id, tbl, edgeKind, line, col));
            }
        }
    }

    private static TableNode EnsureTableNode(FileNode fileNode, string workspaceId, string relativePath, string tableName)
    {
        var cleanName = tableName.Trim(SqlIdentifierTrimChars);
        var tableId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Table}:{cleanName.ToLowerInvariant()}";
        var existing = fileNode.Children.OfType<TableNode>().FirstOrDefault(t => t.Id == tableId);
        if (existing != null) return existing;

        var tableNode = new TableNode(tableId, cleanName, relativePath);
        tableNode.CustomProperties ??= new();
        tableNode.CustomProperties[FilePathKey] = relativePath;
        tableNode.CustomProperties[NameKey] = cleanName;
        fileNode.Children.Add(tableNode);
        return tableNode;
    }

    private static void EnsureMySqlDatabaseNode(FileNode fileNode, string workspaceId, string relativePath)
    {
        var dbId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:mysql";
        if (fileNode.Children.Any(c => c.Id == dbId)) return;

        var dbNode = new DatabaseNode(dbId, EngineMySql, relativePath, DbTypeRelational)
        {
            Engine = EngineMySql,
            Technology = EngineMySql,
            Provider = DbiMarker,
            IsOrm = false
        };
        fileNode.Children.Add(dbNode);
        fileNode.References.Add(new Reference(fileNode.Id, dbId, OntologyConstants.Relationships.UsesDb));
    }

    private static HashSet<string> ExtractTablesFromSql(string sql)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in SqlTableExtractRegex.Matches(sql))
        {
            var tbl = m.Groups["tbl"].Value.Trim(SqlIdentifierTrimChars);
            if (!string.IsNullOrWhiteSpace(tbl) && !SqlIgnoredWords.Contains(tbl))
            {
                result.Add(tbl);
            }
        }
        return result;
    }

    private static string DetermineSqlOperation(string sql)
    {
        var trimmed = sql.TrimStart();
        if (trimmed.StartsWith(InsertPrefix, StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith(ReplacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return OpInsert;
        }
        if (trimmed.StartsWith(UpdatePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return OpUpdate;
        }
        if (trimmed.StartsWith(DeletePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return OpDelete;
        }
        return OpSelect;
    }

    private static FunctionNode? FindEnclosingSub(int pos, List<PerlFileParser.PerlSubSpan> subSpans)
    {
        foreach (var s in subSpans)
        {
            if (pos >= s.Start && pos <= s.End) return s.Node;
        }
        return subSpans.FirstOrDefault()?.Node;
    }

    private static string ResolvePerlCommandConstant(string rawCmd, Dictionary<string, string> localConstants)
    {
        var trimmed = rawCmd.Trim().TrimStart('$');
        if (string.IsNullOrEmpty(trimmed)) return rawCmd;

        if (localConstants.TryGetValue(trimmed, out var direct))
        {
            return direct;
        }

        if (trimmed.Contains("::", StringComparison.Ordinal))
        {
            var dotKey = trimmed.Replace("::", ".", StringComparison.Ordinal);
            if (ConstantRegistry.TryResolve(null, dotKey, out var dotVal) && !string.IsNullOrWhiteSpace(dotVal))
            {
                return dotVal;
            }
            var leaf = trimmed[(trimmed.LastIndexOf("::", StringComparison.Ordinal) + 2)..];
            if (localConstants.TryGetValue(leaf, out var leafVal))
            {
                return leafVal;
            }
            if (ConstantRegistry.TryResolve(null, leaf, out var leafResolved) && !string.IsNullOrWhiteSpace(leafResolved))
            {
                return leafResolved;
            }
        }
        else if (ConstantRegistry.TryResolve(null, trimmed, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        return rawCmd;
    }

    internal static string MaskComments(string source) => MaskPerlSource(source, maskStrings: false);

    internal static string MaskCommentsAndStrings(string source) => MaskPerlSource(source, maskStrings: true);

    private static string MaskPerlSource(string source, bool maskStrings)
    {
        var chars = source.ToCharArray();
        var len = chars.Length;
        var i = 0;

        while (i < len)
        {
            if (chars[i] == '#' && (i == 0 || chars[i - 1] != '$'))
            {
                while (i < len && chars[i] != '\n')
                {
                    chars[i] = ' ';
                    i++;
                }
                continue;
            }

            if (chars[i] is '"' or '\'')
            {
                var q = chars[i];
                if (maskStrings) chars[i] = ' ';
                i++;
                while (i < len && chars[i] != q)
                {
                    if (chars[i] == '\\' && i + 1 < len)
                    {
                        if (maskStrings && chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }
                    if (maskStrings && chars[i] != '\n') chars[i] = ' ';
                    i++;
                }
                if (i < len && chars[i] == q)
                {
                    if (maskStrings) chars[i] = ' ';
                    i++;
                }
                continue;
            }

            i++;
        }

        return new string(chars);
    }
}
