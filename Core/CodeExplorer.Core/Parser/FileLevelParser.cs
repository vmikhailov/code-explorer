using CodeExplorer.Common;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CodeExplorer.Parser;

public class FileLevelParser
{
    private readonly ParsingContext _ctx;
    private readonly string _filePath;
    private readonly string _parentFolderOrProjectId;
    private readonly IFileParser _fileParser;

    public FileLevelParser(ParsingContext ctx, string filePath, string parentFolderOrProjectId, IFileParser fileParser)
    {
        _ctx = ctx;
        _filePath = filePath.Replace('\\', '/');
        _parentFolderOrProjectId = parentFolderOrProjectId;
        _fileParser = fileParser;
    }

    public async Task ParseAsync()
    {
        var relativePath = Path.GetRelativePath(_ctx.AbsoluteWorkspacePath, _filePath).Replace('\\', '/');
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Parsing file: '{relativePath}' ({_fileParser.LanguageName})");

        try
        {
            var fileNode = await _fileParser.ParseAsync(_filePath, _parentFolderOrProjectId, _ctx);
            if (fileNode != null)
            {
                await OntologyUploader.UploadNodeTreeAsync(fileNode, _parentFolderOrProjectId, _ctx);
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error parsing file {_filePath}: {ex.Message}");
        }
    }
}
