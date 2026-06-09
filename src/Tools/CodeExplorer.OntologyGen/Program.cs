using OntologyGen;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: OntologyGen <commonDir> <outputMdPath>");
    Console.Error.WriteLine("  commonDir   - path to the Common/ folder containing Nodes/ and Relationships/");
    Console.Error.WriteLine("  outputMdPath - path to write the generated ontology.md");
    return 1;
}

var commonDir = args[0];
var outputPath = args[1];

if (!Directory.Exists(commonDir))
{
    Console.Error.WriteLine($"Error: commonDir does not exist: {commonDir}");
    return 1;
}

var nodesDir = Path.Combine(commonDir, "Nodes");
var relationshipsDir = Path.Combine(commonDir, "Relationships");

if (!Directory.Exists(nodesDir))
{
    Console.Error.WriteLine($"Error: Nodes directory not found: {nodesDir}");
    return 1;
}

if (!Directory.Exists(relationshipsDir))
{
    Console.Error.WriteLine($"Error: Relationships directory not found: {relationshipsDir}");
    return 1;
}

Console.WriteLine($"[OntologyGen] Scanning {nodesDir}");
Console.WriteLine($"[OntologyGen] Scanning {relationshipsDir}");

var nodeFiles = Directory.GetFiles(nodesDir, "*.cs", SearchOption.AllDirectories);
var relationshipFiles = Directory.GetFiles(relationshipsDir, "*.cs", SearchOption.TopDirectoryOnly);

var extractor = new OntologyExtractor();

var nodeInfos = await extractor.ExtractNodesAsync(nodeFiles);
var relInfos = await extractor.ExtractRelationshipsAsync(relationshipFiles);

var markdown = MarkdownRenderer.Render(nodeInfos, relInfos);

var outputDir = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(outputDir))
    Directory.CreateDirectory(outputDir);

await File.WriteAllTextAsync(outputPath, markdown);

Console.WriteLine($"[OntologyGen] Written {nodeInfos.Count} nodes, {relInfos.Count} relationships → {outputPath}");
return 0;
