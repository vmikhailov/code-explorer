using System;
using System.IO;
using TreeSitter;

class Program
{
    static void Main()
    {
        string filePath = "/Users/slava/Projects/ATS/src/services/cf-worker/workers/tracker-worker/workers-site/src/entities/impresssion-entity.js";
        if (!File.Exists(filePath))
        {
            Console.WriteLine("File not found");
            return;
        }
        string sourceText = File.ReadAllText(filePath);

        Console.WriteLine("Testing 'javascript' Language...");
        try
        {
            using var language = new Language("javascript");
            using var parser = new global::TreeSitter.Parser(language);
            using var tree = parser.Parse(sourceText);
            if (tree != null && tree.RootNode != null)
            {
                Console.WriteLine($"JS Parse succeeded. Root: {tree.RootNode.Type}, Children: {tree.RootNode.Children.Count}");
            }
            else
            {
                Console.WriteLine("JS Parse returned null tree");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error using javascript language: {ex}");
        }
    }
}
