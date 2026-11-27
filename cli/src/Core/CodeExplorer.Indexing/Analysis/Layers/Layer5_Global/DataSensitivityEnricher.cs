using CodeExplorer.Core.Common.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser.Layers;

internal static class DataSensitivityEnricher
{
    private static readonly HashSet<string> SecretWords = new(StringComparer.Ordinal)
    {
        "secret", "password", "passwd", "credential", "apikey",
        "privatekey", "signingkey", "refreshtoken", "accesstoken", "sessiontoken",
        "oauthtoken", "usertoken", "authtoken", "resettoken", "verificationtoken",
        "infratoken", "apitoken", "clientsecret", "mfasecret", "totpsecret", "twofactor"
    };

    private static readonly HashSet<string> PciWords = new(StringComparer.Ordinal)
    {
        "creditcard", "paymentcard", "cardnumber", "cardvault", "billingcard",
        "cvv", "cvc", "iban", "bankaccount", "routingnumber", "panhash", "cardtoken",
        "stripecustomer", "paymentmethod"
    };

    private static readonly HashSet<string> TablePiiWords = new(StringComparer.Ordinal)
    {
        "customer", "user", "buyer", "employee", "patient", "person",
        "profile", "accountholder", "contact", "invite", "owner"
    };

    private static readonly HashSet<string> ColumnPiiWords = new(StringComparer.Ordinal)
    {
        "passport", "ssn", "socialsecurity", "taxid", "nationalid", "driverlicense",
        "email", "phone", "mobile", "homeaddress", "streetaddress",
        "birthdate", "dateofbirth", "firstname", "lastname", "fullname", "ipaddress"
    };

    private sealed record ColumnNameDto(
        [property: JsonPropertyName("name")] string? Name);

    public static List<Node> EnrichTableSensitivity(Layer4Result? l4Result, WorkspaceNode? workspace = null)
    {
        var updatedNodes = new List<Node>();
        if (l4Result == null) return updatedNodes;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allTables = new List<TableNode>();
        var highestByName = new Dictionary<string, DataSensitivity>(StringComparer.OrdinalIgnoreCase);

        void Walk(IOntologyNode node)
        {
            if (node is TableNode tbl && visited.Add(tbl.Id))
            {
                allTables.Add(tbl);
                var classified = tbl.Sensitivity ?? ClassifyTable(tbl);
                if (classified != DataSensitivity.Unspecified && !string.IsNullOrWhiteSpace(tbl.Name))
                {
                    if (!highestByName.TryGetValue(tbl.Name, out var prev) || SensitivityRank(classified) > SensitivityRank(prev))
                    {
                        highestByName[tbl.Name] = classified;
                    }
                }
                if (tbl.Sensitivity == null && classified != DataSensitivity.Unspecified)
                {
                    tbl.Sensitivity = classified;
                    updatedNodes.Add(Node.FromNode(tbl));
                }
            }

            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        Walk(l4Result.SemanticStructure);
        if (workspace != null)
        {
            Walk(workspace);
        }

        // Synchronize duplicate TableNodes sharing the same canonical table name
        for (var i = 0; i < allTables.Count; i++)
        {
            var tbl = allTables[i];
            if (string.IsNullOrWhiteSpace(tbl.Name) || !highestByName.TryGetValue(tbl.Name, out var best))
                continue;

            var current = tbl.Sensitivity ?? DataSensitivity.Unspecified;
            if (SensitivityRank(best) > SensitivityRank(current))
            {
                tbl.Sensitivity = best;
                updatedNodes.Add(Node.FromNode(tbl));
            }
        }

        return updatedNodes;
    }

    internal static DataSensitivity ClassifyTable(TableNode table)
    {
        var tableCandidates = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(table.Name)) tableCandidates.Add(table.Name);
        if (!string.IsNullOrWhiteSpace(table.EntityName)) tableCandidates.Add(table.EntityName);

        var columnCandidates = new List<string>(8);
        if (!string.IsNullOrWhiteSpace(table.ColumnsJson))
        {
            try
            {
                var cols = JsonSerializer.Deserialize<List<ColumnNameDto>>(table.ColumnsJson);
                if (cols != null)
                {
                    for (var i = 0; i < cols.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(cols[i].Name))
                        {
                            columnCandidates.Add(cols[i].Name!);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed column metadata; fall back to table/entity names.
            }
        }

        var highest = DataSensitivity.Unspecified;

        for (var i = 0; i < tableCandidates.Count; i++)
        {
            var words = SplitIdentifierTerms(tableCandidates[i]);
            if (MatchesAnyTerm(words, SecretWords)) return DataSensitivity.Secret;
            if (MatchesAnyTerm(words, PciWords)) highest = DataSensitivity.Pci;
            else if (highest == DataSensitivity.Unspecified &&
                     (MatchesAnyTerm(words, TablePiiWords) || MatchesAnyTerm(words, ColumnPiiWords)))
            {
                highest = DataSensitivity.Pii;
            }
        }

        for (var i = 0; i < columnCandidates.Count; i++)
        {
            var rawCol = columnCandidates[i];
            if (IsForeignKeyOrIdColumn(rawCol)) continue;

            var words = SplitIdentifierTerms(rawCol);
            if (MatchesAnyTerm(words, SecretWords)) return DataSensitivity.Secret;
            if (MatchesAnyTerm(words, PciWords)) highest = DataSensitivity.Pci;
            else if (highest == DataSensitivity.Unspecified && MatchesAnyTerm(words, ColumnPiiWords))
            {
                highest = DataSensitivity.Pii;
            }
        }

        return highest;
    }

    private static int SensitivityRank(DataSensitivity s) => s switch
    {
        DataSensitivity.Secret => 3,
        DataSensitivity.Pci => 2,
        DataSensitivity.Pii => 1,
        _ => 0
    };

    private static List<string> SplitIdentifierTerms(string raw)
    {
        var rawWords = new List<string>(6);
        var start = -1;

        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (!char.IsLetterOrDigit(ch))
            {
                if (start >= 0)
                {
                    rawWords.Add(Singularize(raw[start..i].ToLowerInvariant()));
                    start = -1;
                }
                continue;
            }

            if (start < 0)
            {
                start = i;
            }
            else if (char.IsUpper(ch) &&
                     (char.IsLower(raw[i - 1]) ||
                      (i + 1 < raw.Length && char.IsUpper(raw[i - 1]) && char.IsLower(raw[i + 1]))))
            {
                rawWords.Add(Singularize(raw[start..i].ToLowerInvariant()));
                start = i;
            }
        }

        if (start >= 0)
        {
            rawWords.Add(Singularize(raw[start..].ToLowerInvariant()));
        }

        // Emit individual words + adjacent 2-word compounds (e.g. ["payment", "method"] -> "paymentmethod")
        var terms = new List<string>(rawWords.Count * 2);
        for (var i = 0; i < rawWords.Count; i++)
        {
            terms.Add(rawWords[i]);
            if (i + 1 < rawWords.Count)
            {
                terms.Add(rawWords[i] + rawWords[i + 1]);
            }
        }
        return terms;
    }

    private static string Singularize(string word)
    {
        if (word.Length <= 3) return word;
        if (word.EndsWith("ies", StringComparison.Ordinal) && word.Length > 4)
            return word[..^3] + "y";
        if (word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) && !word.EndsWith("us", StringComparison.Ordinal))
            return word[..^1];
        return word;
    }

    private static bool IsForeignKeyOrIdColumn(string colName)
    {
        if (colName.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
            colName.EndsWith("_uuid", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (colName.Length > 2 && colName.EndsWith("Id", StringComparison.Ordinal) &&
            char.IsLower(colName[^3]))
        {
            return true;
        }

        return colName.Length > 4 && colName.EndsWith("Uuid", StringComparison.Ordinal) &&
               char.IsLower(colName[^5]);
    }

    private static bool MatchesAnyTerm(List<string> candidateTerms, HashSet<string> targetSet)
    {
        for (var i = 0; i < candidateTerms.Count; i++)
        {
            if (targetSet.Contains(candidateTerms[i]))
            {
                return true;
            }
        }
        return false;
    }
}
