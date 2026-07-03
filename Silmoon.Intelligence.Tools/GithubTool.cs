using System;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using Silmoon.AI.Models;
using Silmoon.AI.OpenAI.Models;
using Silmoon.AI.Tools;
using Silmoon.Extensions;
using Silmoon.Models;

namespace Silmoon.Intelligence.Tools
{
    public class GithubTool : ExecuteTool
    {
        public override Tool[] GetTools()
        {
            return [
                Tool.Create("GithubTool_GetFileContent", """
                Read one text file from a public GitHub repository via raw URL.
                Required: repository (owner/repo), filePath (relative path with '/').
                Optional: ref (branch/tag/commit). If omitted, tool auto-detects repository default branch, then falls back to main/master.
                Returns file content on success; otherwise returns clear error message (invalid params, not found, or request failure).
                """,
                [
                    new ToolParameterProperty("string", "repository", "GitHub repository in exact 'owner/repo' format (required).", null, true),
                    new ToolParameterProperty("string", "filePath", "Relative file path in repo using '/'. Example: 'src/index.js' (required).", null, true),
                    new ToolParameterProperty("string", "ref", "Optional git ref: branch/tag/commit. Omit to auto-resolve default branch.", null, false)
                    ]),
                Tool.Create("GithubTool_ListDirectoryEntries", """
                List all entries (files and subdirectories) under one directory in a public GitHub repository.
                Required: repository (owner/repo). Optional: dirPath (relative directory path, root if omitted), ref (branch/tag/commit).
                If ref is omitted, tool auto-detects repository default branch, then falls back to main/master.
                Returns a JSON array with directory entries (name, path, type, size, sha, downloadUrl).
                """,
                [
                    new ToolParameterProperty("string", "repository", "GitHub repository in exact 'owner/repo' format (required).", null, true),
                    new ToolParameterProperty("string", "dirPath", "Optional relative directory path using '/'. Omit or empty means repo root.", null, false),
                    new ToolParameterProperty("string", "ref", "Optional git ref: branch/tag/commit. Omit to auto-resolve default branch.", null, false)
                    ])
                ];
        }

        public override async Task<ToolCallResult> OnToolCallInvoke(ToolCallParameter toolCallParameter, ToolCallResult toolCallResult)
        {
            var functionName = toolCallParameter.FunctionName;
            ToolCallResult result = null;
            switch (functionName)
            {
                case "GithubTool_GetFileContent":
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var fileContent = await GetGithubFileContent(toolCallParameter);
                    result = ToolCallResult.Create(toolCallParameter, fileContent);
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                case "GithubTool_ListDirectoryEntries":
                    await NotifyToolExecuting(functionName, toolCallParameter);
                    var directoryContent = await ListGithubDirectoryEntries(toolCallParameter);
                    result = ToolCallResult.Create(toolCallParameter, directoryContent);
                    await NotifyToolExecuted(functionName, toolCallParameter, result);
                    break;
                default:
                    break;
            }
            return result;
        }
        async Task<StateSet<bool, object>> ListGithubDirectoryEntries(ToolCallParameter toolCallParameter)
        {
            var repository = toolCallParameter.Parameters["repository"]?.ToString()?.Trim();
            var dirPath = toolCallParameter.Parameters["dirPath"]?.ToString()?.Trim();
            var gitRef = toolCallParameter.Parameters["ref"]?.ToString()?.Trim();

            if (!TryValidateRepository(repository, out var repositoryError)) return false.ToStateSet<object>(null, repositoryError);
            if (!TryValidateDirectoryPath(dirPath, out var dirPathError)) return false.ToStateSet<object>(null, dirPathError);

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Silmoon.Intelligence/1.0");
            try
            {
                if (!gitRef.IsNullOrEmpty())
                {
                    if (!TryValidateRef(gitRef, out var refError)) return false.ToStateSet<object>(null, refError);
                    var explicitRefUrl = BuildContentsApiUrl(repository!, dirPath, gitRef);
                    var explicitRefResponse = await client.GetAsync(explicitRefUrl);
                    if (!explicitRefResponse.IsSuccessStatusCode) return false.ToStateSet<object>(null, $"GitHub request failed ({(int)explicitRefResponse.StatusCode} {explicitRefResponse.StatusCode}), message: {await explicitRefResponse.Content.ReadAsStringAsync()}.");

                    var explicitRefJson = await explicitRefResponse.Content.ReadAsStringAsync();
                    return ConvertDirectoryApiResult(explicitRefJson);
                }

                var candidateRefs = await GetCandidateRefs(client, repository!);
                foreach (var candidateRef in candidateRefs)
                {
                    if (!TryValidateRef(candidateRef, out _)) continue;
                    var requestUrl = BuildContentsApiUrl(repository!, dirPath, candidateRef);
                    var response = await client.GetAsync(requestUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        return ConvertDirectoryApiResult(json);
                    }
                    if (response.StatusCode != HttpStatusCode.NotFound) return false.ToStateSet<object>(null, $"GitHub request failed ({(int)response.StatusCode} {response.StatusCode}), message: {await response.Content.ReadAsStringAsync()}.");
                }

                return false.ToStateSet<object>(null, "Directory not found. Tried default branch and common branches (main/master).");
            }
            catch (TaskCanceledException)
            {
                return false.ToStateSet<object>(null, "GitHub request timeout.");
            }
            catch (HttpRequestException ex)
            {
                return false.ToStateSet<object>(null, $"GitHub request error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return false.ToStateSet<object>(null, ex.Message);
            }
        }
        async Task<StateSet<bool, object>> GetGithubFileContent(ToolCallParameter toolCallParameter)
        {
            var repository = toolCallParameter.Parameters["repository"]?.ToString()?.Trim();
            var filePath = toolCallParameter.Parameters["filePath"]?.ToString()?.Trim();
            var gitRef = toolCallParameter.Parameters["ref"]?.ToString()?.Trim();

            if (!TryValidateRepository(repository, out var repositoryError)) return false.ToStateSet<object>(null, repositoryError);
            if (!TryValidateFilePath(filePath, out var filePathError)) return false.ToStateSet<object>(null, filePathError);

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Silmoon.Intelligence/1.0");
            try
            {
                if (!gitRef.IsNullOrEmpty())
                {
                    if (!TryValidateRef(gitRef, out var refError)) return false.ToStateSet<object>(null, refError);

                    var explicitRefUrl = BuildRawContentUrl(repository!, filePath!, gitRef!);
                    var explicitRefResponse = await client.GetAsync(explicitRefUrl);
                    if (!explicitRefResponse.IsSuccessStatusCode) return false.ToStateSet<object>(null, $"GitHub request failed ({(int)explicitRefResponse.StatusCode} {explicitRefResponse.StatusCode}).");

                    var explicitRefContent = await explicitRefResponse.Content.ReadAsStringAsync();
                    return true.ToStateSet<object>(explicitRefContent);
                }

                var candidateRefs = await GetCandidateRefs(client, repository!);
                foreach (var candidateRef in candidateRefs)
                {
                    if (!TryValidateRef(candidateRef, out _)) continue;

                    var requestUrl = BuildRawContentUrl(repository!, filePath!, candidateRef);
                    var response = await client.GetAsync(requestUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var fileContent = await response.Content.ReadAsStringAsync();
                        return true.ToStateSet<object>(fileContent);
                    }

                    if (response.StatusCode != HttpStatusCode.NotFound) return false.ToStateSet<object>(null, $"GitHub request failed ({(int)response.StatusCode} {response.StatusCode}).");
                }

                return false.ToStateSet<object>(null, "File not found. Tried default branch and common branches (main/master).");
            }
            catch (TaskCanceledException)
            {
                return false.ToStateSet<object>(null, "GitHub request timeout.");
            }
            catch (HttpRequestException ex)
            {
                return false.ToStateSet<object>(null, $"GitHub request error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return false.ToStateSet<object>(null, ex.Message);
            }
        }

        static async Task<List<string>> GetCandidateRefs(HttpClient client, string repository)
        {
            var candidates = new List<string>();
            var parts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var owner = Uri.EscapeDataString(parts[0]);
            var repo = Uri.EscapeDataString(parts[1]);
            var repoApi = $"https://api.github.com/repos/{owner}/{repo}";

            try
            {
                var apiResponse = await client.GetAsync(repoApi);
                if (apiResponse.IsSuccessStatusCode)
                {
                    var json = await apiResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("default_branch", out var defaultBranchElement))
                    {
                        var defaultBranch = defaultBranchElement.GetString()?.Trim();
                        if (!defaultBranch.IsNullOrEmpty()) candidates.Add(defaultBranch);
                    }
                }
            }
            catch
            {
            }

            candidates.Add("main");
            candidates.Add("master");
            return candidates.Where(x => !x.IsNullOrEmpty()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        static string BuildRawContentUrl(string repository, string filePath, string gitRef)
        {
            var repositoryParts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var encodedOwner = Uri.EscapeDataString(repositoryParts[0]);
            var encodedRepo = Uri.EscapeDataString(repositoryParts[1]);
            var encodedRef = Uri.EscapeDataString(gitRef);
            var encodedPath = string.Join('/', filePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
            return $"https://raw.githubusercontent.com/{encodedOwner}/{encodedRepo}/{encodedRef}/{encodedPath}";
        }
        static string BuildContentsApiUrl(string repository, string? dirPath, string gitRef)
        {
            var repositoryParts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var encodedOwner = Uri.EscapeDataString(repositoryParts[0]);
            var encodedRepo = Uri.EscapeDataString(repositoryParts[1]);
            var encodedRef = Uri.EscapeDataString(gitRef);
            var normalizedDir = dirPath.IsNullOrEmpty() ? string.Empty : string.Join('/', dirPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
            return normalizedDir.IsNullOrEmpty() ? $"https://api.github.com/repos/{encodedOwner}/{encodedRepo}/contents?ref={encodedRef}" : $"https://api.github.com/repos/{encodedOwner}/{encodedRepo}/contents/{normalizedDir}?ref={encodedRef}";
        }
        static StateSet<bool, object> ConvertDirectoryApiResult(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false.ToStateSet<object>(null, "Specified path is not a directory.");

            var items = new List<object>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                items.Add(new
                {
                    name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null,
                    path = item.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null,
                    type,
                    size = item.TryGetProperty("size", out var sizeEl) && sizeEl.ValueKind == JsonValueKind.Number ? sizeEl.GetInt64() : 0,
                    sha = item.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() : null,
                    downloadUrl = item.TryGetProperty("download_url", out var downloadEl) ? downloadEl.GetString() : null
                });
            }
            return true.ToStateSet<object>(items);
        }

        static bool TryValidateRepository(string? repository, out string error)
        {
            error = null;
            if (repository.IsNullOrEmpty())
            {
                error = "repository is required.";
                return false;
            }

            var parts = repository.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                error = "repository must be in 'owner/repo' format.";
                return false;
            }

            if (HasUnsafeSegment(parts[0]) || HasUnsafeSegment(parts[1]))
            {
                error = "repository contains unsafe characters.";
                return false;
            }
            return true;
        }

        static bool TryValidateFilePath(string? filePath, out string error)
        {
            error = null;
            if (filePath.IsNullOrEmpty())
            {
                error = "filePath is required.";
                return false;
            }

            if (filePath.StartsWith('/') || filePath.StartsWith('\\') || filePath.Contains('\\'))
            {
                error = "filePath must use relative forward-slash path.";
                return false;
            }

            var segments = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                error = "filePath is invalid.";
                return false;
            }

            foreach (var segment in segments)
            {
                if (segment == "." || segment == ".." || HasUnsafeSegment(segment))
                {
                    error = "filePath contains unsafe segment.";
                    return false;
                }
            }

            return true;
        }
        static bool TryValidateDirectoryPath(string? dirPath, out string error)
        {
            error = null;
            if (dirPath.IsNullOrEmpty()) return true;
            if (dirPath.StartsWith('/') || dirPath.StartsWith('\\') || dirPath.Contains('\\'))
            {
                error = "dirPath must use relative forward-slash path.";
                return false;
            }

            var segments = dirPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (segment == "." || segment == ".." || HasUnsafeSegment(segment))
                {
                    error = "dirPath contains unsafe segment.";
                    return false;
                }
            }
            return true;
        }

        static bool TryValidateRef(string? gitRef, out string error)
        {
            error = null;
            if (gitRef.IsNullOrEmpty())
            {
                error = "ref is required.";
                return false;
            }

            if (gitRef.Contains("..") || gitRef.Contains('\\') || gitRef.Contains('~') || gitRef.Contains('^') || gitRef.Contains(':') || gitRef.Contains('?') || gitRef.Contains('*') || gitRef.Contains('['))
            {
                error = "ref contains unsafe characters.";
                return false;
            }

            if (gitRef.StartsWith('/') || gitRef.EndsWith('/') || gitRef.Contains("//"))
            {
                error = "ref format is invalid.";
                return false;
            }

            return true;
        }

        static bool HasUnsafeSegment(string value)
        {
            if (value.IsNullOrEmpty()) return true;
            foreach (var c in value)
            {
                if (char.IsControl(c)) return true;
            }
            return false;
        }
    }
}

