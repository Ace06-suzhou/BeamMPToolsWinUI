using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using BeamMPTools.Core.Models;

namespace BeamMPTools.Core.Installation;

/// <summary>
/// Updates an existing Windows BeamMP Launcher installation from the latest
/// official GitHub release. Downloads are staged, checked, and only then moved into place.
/// </summary>
public sealed class ClientInstaller
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/BeamMP/BeamMP-Launcher/releases/latest";
    private const string ExpectedAssetName = "BeamMP-Launcher.exe";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<InstallResult> InstallAsync(
        string targetDirectory,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            return InstallResult.Failed("安装目录不能为空。");

        string? temporaryPath = null;

        try
        {
            progress?.Report(new InstallProgress("正在查询 BeamMP 最新正式版本…"));
            ReleaseAsset releaseAsset = await GetLatestReleaseAssetAsync(cancellationToken);

            Directory.CreateDirectory(targetDirectory);
            string targetPath = Path.Combine(targetDirectory, ExpectedAssetName);
            EnsureLauncherIsClosed(targetPath);
            temporaryPath = Path.Combine(targetDirectory, $"{ExpectedAssetName}.{Guid.NewGuid():N}.download");

            string? expectedDigest = releaseAsset.Digest;
            if (string.IsNullOrWhiteSpace(expectedDigest) && !string.IsNullOrWhiteSpace(releaseAsset.DigestUrl))
            {
                progress?.Report(new InstallProgress("正在获取官方 SHA-256 校验文件…"));
                expectedDigest = await DownloadSha256Async(releaseAsset.DigestUrl, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(expectedDigest))
                throw new InvalidDataException("GitHub Release 未提供 SHA-256 校验值，已停止更新以避免安装未经校验的文件。");

            string expectedSha256 = NormalizeSha256(expectedDigest);
            Exception? lastException = null;
            string[] downloadUrls =
            {
                "https://ghproxy.net/" + releaseAsset.DownloadUrl,
                releaseAsset.DownloadUrl
            };

            bool downloaded = false;
            foreach (string downloadUrl in downloadUrls.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    Uri uri = new(downloadUrl);
                    progress?.Report(new InstallProgress($"正在从 {uri.Host} 下载 {releaseAsset.Version}…"));
                    await DownloadToFileAsync(
                        downloadUrl,
                        temporaryPath,
                        releaseAsset.Size,
                        progress,
                        cancellationToken);

                    await ValidateDownloadedExecutableAsync(
                        temporaryPath,
                        expectedSha256,
                        releaseAsset.Size,
                        cancellationToken);

                    downloaded = true;
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    TryDelete(temporaryPath);
                }
            }

            if (!downloaded)
            {
                return InstallResult.Failed(
                    "所有下载源均失败。" + (lastException is null ? string.Empty : $"最后错误：{lastException.Message}"));
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new InstallProgress("下载与文件校验完成，正在更新安装目录…"));

            // File.Move with overwrite is atomic on the same volume. If the launcher
            // is running and Windows locks its image, the operation fails safely.
            File.Move(temporaryPath, targetPath, overwrite: true);
            temporaryPath = null;

            progress?.Report(new InstallProgress("BeamMP Launcher 更新完成。"));
            return new InstallResult(true, releaseAsset.Version, targetPath, null);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporaryPath);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(temporaryPath);
            return InstallResult.Failed(exception.Message);
        }
    }

    private static async Task<ReleaseAsset> GetLatestReleaseAssetAsync(CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("BeamMPTools-WinUI3/1.0");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        using HttpResponseMessage response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            token);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(token);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        JsonElement root = document.RootElement;

        string version = root.TryGetProperty("tag_name", out JsonElement tagElement)
            ? tagElement.GetString() ?? "latest"
            : "latest";

        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub 最新版本信息中没有可用的 Release 资产列表。");

        string? executableUrl = null;
        string? checksumUrl = null;
        string? digest = null;
        long assetSize = 0;

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string name = asset.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
            string browserUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                ? urlElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(name, ExpectedAssetName, StringComparison.OrdinalIgnoreCase))
            {
                executableUrl = browserUrl;
                assetSize = asset.TryGetProperty("size", out JsonElement sizeElement)
                    && sizeElement.TryGetInt64(out long parsedSize)
                        ? parsedSize
                        : 0;
                digest = asset.TryGetProperty("digest", out JsonElement digestElement)
                    ? digestElement.GetString()
                    : null;
            }
            else if (string.Equals(name, ExpectedAssetName + ".sha256", StringComparison.OrdinalIgnoreCase))
            {
                checksumUrl = browserUrl;
            }
        }

        if (!Uri.TryCreate(executableUrl, UriKind.Absolute, out Uri? executableUri)
            || executableUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException(
                $"BeamMP 最新正式版本 {version} 没有名为 {ExpectedAssetName} 的 Windows 资产。请检查官方发布格式是否已经变更。");
        }

        if (!string.IsNullOrWhiteSpace(checksumUrl)
            && (!Uri.TryCreate(checksumUrl, UriKind.Absolute, out Uri? checksumUri)
                || checksumUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidDataException("GitHub Release 中的 SHA-256 下载地址无效。");
        }

        return new ReleaseAsset(version, executableUri.ToString(), assetSize, digest, checksumUrl);
    }

    private static async Task<string> DownloadSha256Async(string checksumUrl, CancellationToken token)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(
            checksumUrl,
            HttpCompletionOption.ResponseHeadersRead,
            token);
        response.EnsureSuccessStatusCode();
        string content = await response.Content.ReadAsStringAsync(token);
        Match match = Regex.Match(content, @"\b[0-9a-fA-F]{64}\b");
        if (!match.Success)
            throw new InvalidDataException("官方 SHA-256 文件格式无效。");

        return "sha256:" + match.Value;
    }

    private static async Task DownloadToFileAsync(
        string url,
        string destinationPath,
        long expectedSize,
        IProgress<InstallProgress>? progress,
        CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using HttpResponseMessage response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            token);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        if (expectedSize > 0 && totalBytes.HasValue && totalBytes.Value != expectedSize)
            throw new InvalidDataException("下载源返回的文件大小与 GitHub Release 元数据不一致。");

        await using Stream input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] buffer = new byte[128 * 1024];
        long bytesReceived = 0;
        int lastReportedPercent = -1;
        int read;
        long? progressTotal = totalBytes ?? (expectedSize > 0 ? expectedSize : null);
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            bytesReceived += read;

            if (progressTotal is > 0)
            {
                int percent = (int)Math.Clamp(bytesReceived * 100d / progressTotal.Value, 0, 100);
                if (percent == 100 || percent >= lastReportedPercent + 2)
                {
                    lastReportedPercent = percent;
                    progress?.Report(new InstallProgress("正在下载 BeamMP Launcher…", bytesReceived, progressTotal));
                }
            }
            else if (bytesReceived - (lastReportedPercent < 0 ? 0 : (long)lastReportedPercent * 1024 * 1024) >= 1024 * 1024)
            {
                lastReportedPercent = (int)(bytesReceived / (1024 * 1024));
                progress?.Report(new InstallProgress("正在下载 BeamMP Launcher…", bytesReceived));
            }
        }

        await output.FlushAsync(token);
        if (expectedSize > 0 && bytesReceived != expectedSize)
            throw new InvalidDataException($"下载不完整：预期 {expectedSize:N0} 字节，实际收到 {bytesReceived:N0} 字节。");
    }

    private static async Task ValidateDownloadedExecutableAsync(
        string filePath,
        string expectedSha256,
        long expectedSize,
        CancellationToken token)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length < 256 * 1024)
            throw new InvalidDataException("下载结果体积异常，已拒绝将其作为 Launcher 安装。");

        if (expectedSize > 0 && fileInfo.Length != expectedSize)
            throw new InvalidDataException("下载文件大小与官方 Release 元数据不匹配。");

        await using (FileStream stream = File.OpenRead(filePath))
        {
            byte[] dosHeader = new byte[64];
            int read = await stream.ReadAsync(dosHeader.AsMemory(), token);
            if (read != dosHeader.Length || dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z')
                throw new InvalidDataException("下载文件不是有效的 Windows PE 可执行文件（缺少 MZ 文件头）。");

            int peOffset = BitConverter.ToInt32(dosHeader, 0x3C);
            if (peOffset < 64 || peOffset > stream.Length - 4)
                throw new InvalidDataException("Windows PE 文件头位置无效。");

            stream.Position = peOffset;
            byte[] signature = new byte[4];
            read = await stream.ReadAsync(signature.AsMemory(), token);
            if (read != 4 || signature[0] != (byte)'P' || signature[1] != (byte)'E'
                || signature[2] != 0 || signature[3] != 0)
                throw new InvalidDataException("下载文件不包含有效的 PE 签名。");
        }

        await using FileStream fileStream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(fileStream, token);
        string actualHash = Convert.ToHexString(hash);

        if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("SHA-256 校验失败，文件与官方 Release 元数据不一致。");
    }

    private static string NormalizeSha256(string digest)
    {
        string value = digest.Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            value = value["sha256:".Length..].Trim();

        if (!Regex.IsMatch(value, @"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Release 摘要不是有效的 SHA-256 值，已停止更新。");

        return value;
    }


    private static void EnsureLauncherIsClosed(string executablePath)
    {
        string expectedPath = Path.GetFullPath(executablePath);
        foreach (string processName in new[] { "BeamMP-Launcher", "BeamMP" })
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    string? actualPath;
                    try
                    {
                        actualPath = process.MainModule?.FileName;
                    }
                    catch
                    {
                        // If process details cannot be read, the final atomic replace is still safe.
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(actualPath)
                        && string.Equals(Path.GetFullPath(actualPath), expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("BeamMP Launcher 仍在运行。请先完全退出 Launcher，再执行更新。");
                    }
                }
            }
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BeamMPTools-WinUI3/1.0");
        return client;
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Keep the original error as the actionable failure reason.
        }
    }

    private sealed record ReleaseAsset(
        string Version,
        string DownloadUrl,
        long Size,
        string? Digest,
        string? DigestUrl);
}
