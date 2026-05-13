package ollama

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"syscall"
	"time"
)

const OllamaBaseURL = "http://localhost:11434"

type ModelInfo struct {
	Name    string `json:"name"`
	Size    int64  `json:"size"`
	Digest  string `json:"digest"`
	ModTime string `json:"modified_at"`
}

type ModelList struct {
	Models []ModelInfo `json:"models"`
}

type PullProgress struct {
	Status    string `json:"status"`
	Completed int64  `json:"completed"`
	Total     int64  `json:"total"`
	Digest    string `json:"digest"`
}

func CheckOllamaInstalled() (bool, string, error) {
	cmd := exec.Command("ollama", "--version")
	_, err := cmd.Output()
	if err != nil {
		return false, "", err
	}

	path, err := exec.LookPath("ollama")
	if err != nil {
		return true, "", err
	}

	return true, path, nil
}

func IsOllamaRunning() bool {
	resp, err := http.Get(OllamaBaseURL + "/api/tags")
	if err != nil {
		return false
	}
	defer resp.Body.Close()
	return resp.StatusCode == http.StatusOK
}

func ListModels() ([]ModelInfo, error) {
	resp, err := http.Get(OllamaBaseURL + "/api/tags")
	if err != nil {
		return nil, fmt.Errorf("failed to connect to ollama: %v", err)
	}
	defer resp.Body.Close()

	var modelList ModelList
	if err := json.NewDecoder(resp.Body).Decode(&modelList); err != nil {
		return nil, err
	}

	return modelList.Models, nil
}

func PullModel(modelName string, progressChan chan<- PullProgress) error {
	defer close(progressChan)

	resp, err := http.Post(
		OllamaBaseURL+"/api/pull",
		"application/json",
		strings.NewReader(fmt.Sprintf(`{"name": "%s"}`, modelName)),
	)
	if err != nil {
		return fmt.Errorf("failed to start pull: %v", err)
	}
	defer resp.Body.Close()

	decoder := json.NewDecoder(resp.Body)
	for {
		var progress PullProgress
		if err := decoder.Decode(&progress); err != nil {
			if err == io.EOF {
				break
			}
			return fmt.Errorf("decode error: %v", err)
		}
		progressChan <- progress
	}

	return nil
}

func DownloadOllamaWindows() error {
	url := "https://ollama.com/download/OllamaSetup.exe"
	resp, err := http.Get(url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	tmpDir := os.TempDir()
	exePath := filepath.Join(tmpDir, "OllamaSetup.exe")

	out, err := os.Create(exePath)
	if err != nil {
		return err
	}
	defer out.Close()

	_, err = io.Copy(out, resp.Body)
	if err != nil {
		return err
	}

	cmd := exec.Command(exePath, "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART")
	if runtime.GOOS == "windows" {
		cmd.SysProcAttr = &syscall.SysProcAttr{}
	}

	return cmd.Run()
}

func DownloadOllamaMirror() error {
	url := "https://ghproxy.net/https://github.com/ollama/ollama/releases/latest/download/OllamaSetup.exe"
	resp, err := http.Get(url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	tmpDir := os.TempDir()
	exePath := filepath.Join(tmpDir, "OllamaSetup-Mirror.exe")

	out, err := os.Create(exePath)
	if err != nil {
		return err
	}
	defer out.Close()

	_, err = io.Copy(out, resp.Body)
	if err != nil {
		return err
	}

	cmd := exec.Command(exePath, "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART")
	return cmd.Run()
}

func StartOllamaServer() error {
	if IsOllamaRunning() {
		return nil
	}

	cmd := exec.Command("ollama", "serve")
	if runtime.GOOS == "windows" {
		cmd.SysProcAttr = &syscall.SysProcAttr{}
	}

	return cmd.Start()
}

type ChatMessage struct {
	Role    string `json:"role"`
	Content string `json:"content"`
}

type ChatRequest struct {
	Model    string        `json:"model"`
	Messages []ChatMessage `json:"messages"`
	Stream   bool          `json:"stream"`
}

type ChatResponse struct {
	Model     string `json:"model"`
	CreatedAt string `json:"created_at"`
	Message   struct {
		Role    string `json:"role"`
		Content string `json:"content"`
	} `json:"message"`
	Done bool `json:"done"`
}

func Chat(model string, messages []ChatMessage, responseChan chan<- ChatResponse) error {
	defer close(responseChan)

	reqBody := ChatRequest{
		Model:    model,
		Messages: messages,
		Stream:   true,
	}

	jsonBody, _ := json.Marshal(reqBody)

	client := &http.Client{Timeout: 5 * time.Minute}
	resp, err := client.Post(
		OllamaBaseURL+"/api/chat",
		"application/json",
		strings.NewReader(string(jsonBody)),
	)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	decoder := json.NewDecoder(resp.Body)
	for {
		var chatResp ChatResponse
		if err := decoder.Decode(&chatResp); err != nil {
			if err == io.EOF {
				break
			}
			return err
		}
		responseChan <- chatResp
		if chatResp.Done {
			break
		}
	}

	return nil
}
