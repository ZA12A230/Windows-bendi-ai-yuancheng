using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LocalAIAssistant.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LocalAIAssistant.Services
{
    public class OllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private Process? _ollamaProcess;

        public OllamaService(int port = 11434)
        {
            _baseUrl = $"http://localhost:{port}";
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
        }

        public async Task<bool> IsRunningAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/version");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    return json["version"]?.ToString() ?? "未知";
                }
            }
            catch { }
            return "未知";
        }

        public async Task<List<string>> GetInstalledModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var models = json["models"] as JArray;
                    return models?.Select(m => m["name"]?.ToString() ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>();
                }
            }
            catch { }
            return new List<string>();
        }

        public async Task<List<ModelInfo>> GetModelDetailsAsync()
        {
            var models = new List<ModelInfo>();
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var modelArray = json["models"] as JArray;
                    if (modelArray != null)
                    {
                        foreach (var m in modelArray)
                        {
                            var size = m["size"]?.Value<long>() ?? 0;
                            models.Add(new ModelInfo
                            {
                                Name = m["name"]?.ToString() ?? "",
                                Size = FormatSize(size),
                                Modified = m["modified_at"]?.ToString() ?? "",
                                Digest = m["digest"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch { }
            return models;
        }

        public async Task<string> ChatAsync(string model, string message)
        {
            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = message }
                },
                stream = false
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/chat", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseContent);
                return json["message"]?["content"]?.ToString() ?? "";
            }

            throw new Exception($"Chat failed: {response.StatusCode}");
        }

        public async Task PullModelAsync(string modelName, Action<double>? progressCallback = null)
        {
            var requestBody = new { name = modelName, stream = true };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/pull", content);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            var json = JObject.Parse(line);
                            var status = json["status"]?.ToString();
                            if (status == "pulling" && json["completed"] != null && json["total"] != null)
                            {
                                var completed = json["completed"]!.Value<long>();
                                var total = json["total"]!.Value<long>();
                                progressCallback?.Invoke((double)completed / total * 100);
                            }
                        }
                        catch { }
                    }
                }
            }
            else
            {
                throw new Exception($"Pull failed: {response.StatusCode}");
            }
        }

        public async Task DeleteModelAsync(string modelName)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/api/delete")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { name = modelName }), System.Text.Encoding.UTF8, "application/json")
            };
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Delete failed: {response.StatusCode}");
            }
        }

        public void StartOllama()
        {
            if (_ollamaProcess != null && !_ollamaProcess.HasExited) return;

            var ollamaPath = FindOllamaPath();
            if (string.IsNullOrEmpty(ollamaPath))
            {
                DownloadAndInstallOllama();
                return;
            }

            _ollamaProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ollamaPath,
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            _ollamaProcess.Start();
        }

        public void StopOllama()
        {
            if (_ollamaProcess != null && !_ollamaProcess.HasExited)
            {
                _ollamaProcess.Kill();
                _ollamaProcess = null;
            }
        }

        private string? FindOllamaPath()
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ollama", "ollama.exe"),
                "ollama.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    var fullPath = Path.Combine(dir, "ollama.exe");
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return null;
        }

        private void DownloadAndInstallOllama()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ollama.com/download",
                UseShellExecute = true
            });
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }
}
