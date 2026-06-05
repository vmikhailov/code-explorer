using System;
using System.Collections.Generic;
using System.Linq;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MongooseLibraryParser : ILibraryParser
{
    public string LibraryName => "mongoose";

    public bool CanParse(string libraryName)
    {
        return string.Equals(libraryName, "mongoose", StringComparison.OrdinalIgnoreCase);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsMongooseCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsMongooseCall(node))
        {
            var func = node.GetChildForField("function");
            if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
            if (func != null)
            {
                if (func.Type == "member_expression")
                {
                    var obj = func.GetChildForField("object");
                    var prop = func.GetChildForField("property");
                    if (obj != null && prop != null)
                    {
                        var objName = obj.Text;
                        var propName = prop.Text;
                        if (objName == "mongoose" && propName == "model")
                        {
                            var modelName = ExtractFirstStringArgument(node);
                            return $"Mongoose Model: {modelName}";
                        }
                        return $"Mongoose: {objName}.{propName}";
                    }
                }
                else if (func.Type == "identifier" && func.Text == "model")
                {
                    var modelName = ExtractFirstStringArgument(node);
                    return $"Mongoose Model: {modelName}";
                }
            }
            return "Mongoose Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Mongoose actions represent database queries
    }

    private static bool IsMongooseCall(Node node)
    {
        if (node.Type != "call_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_expression")
        {
            var obj = func.GetChildForField("object");
            var prop = func.GetChildForField("property");
            if (obj != null && prop != null && prop.Id != IntPtr.Zero)
            {
                var objName = obj.Text;
                var propName = prop.Text;

                if (objName == "mongoose" && propName == "model")
                {
                    return true;
                }

                return propName is "find" or "findOne" or "findById" or "findOneAndUpdate" 
                                   or "findOneAndDelete" or "create" or "save" or "updateOne" 
                                   or "updateMany" or "deleteOne" or "deleteMany" or "countDocuments";
            }
        }
        else if (func.Type == "identifier")
        {
            return func.Text == "model";
        }
        return false;
    }

    private static string? ExtractFirstStringArgument(Node node)
    {
        var args = node.Children.FirstOrDefault(c => c.Type == "arguments");
        if (args != null)
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Type is "string" or "template_string");
            if (firstArg != null)
            {
                return firstArg.Text.Trim('\'', '"', '`');
            }
        }
        return null;
    }
}
