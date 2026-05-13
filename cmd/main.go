package main

import (
	"embed"
	"fmt"
	"io/fs"
	"log"
	"net/http"
	"os"
	"os/exec"
	"os/signal"
	"runtime"
	"strings"
	"syscall"
	"time"

	"local-ai-assistant/internal/config"
	"local-ai-assistant/internal/network"
	"local-ai-assistant/internal/ollama"
	"local-ai-assistant/internal/remote"
	"local-ai-assistant/internal/system"

	"github.com/gorilla/websocket"
)

//go:embed dist/*
var frontendFS embed.FS

var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool { return true },
}

var sysStatsHistory []system.SystemStats
var appStartTime time.Time

func main() {
	appStartTime = time.Now()

	cfg, err := config.Init()
	if err != nil {
		log.Printf("Failed to load config: %v", err)
	}

	_ = cfg
	_ = network.FrpConfig{}

	log.Println("Local AI Assistant starting...")

	if runtime.GOOS == "windows" {
		go func() {
			time.Sleep(2 * time.Second)
			exec.Command("cmd", "/C", "start", "http://localhost:8080").Run()
		}()
	}

	go startRemoteControl()

	mux := http.NewServeMux()

	mux.HandleFunc("/api/setup/check", handleSetupCheck)
	mux.HandleFunc("/api/setup/ollama/install", handleOllamaInstall)
	mux.HandleFunc("/api/setup/models", handleModelSetup)
	mux.HandleFunc("/api/setup/configure", handleConfigure)
	mux.HandleFunc("/api/ollama/models", handleListModels)
	mux.HandleFunc("/api/ollama/pull", handlePullModel)
	mux.HandleFunc("/api/ollama/chat", handleChat)
	mux.HandleFunc("/api/system/stats", handleSystemStats)
	mux.HandleFunc("/api/system/config", handleGetConfig)
	mux.HandleFunc("/api/system/config/update", handleUpdateConfig)
	mux.HandleFunc("/api/remote/url", handleRemoteURL)
	mux.HandleFunc("/ws/stats", handleStatsWS)

	mux.HandleFunc("/api/network/penetrate", handlePenetrate)

	distFS, _ := fs.Sub(frontendFS, "dist")
	mux.Handle("/", http.FileServer(http.FS(distFS)))

	addr := ":8080"
	log.Printf("Server starting on %s", addr)

	go func() {
		if err := http.ListenAndServe(addr, mux); err != nil {
			log.Printf("HTTP server error: %v", err)
		}
	}()

	stop := make(chan os.Signal, 1)
	signal.Notify(stop, syscall.SIGINT, syscall.SIGTERM)
	<-stop

	log.Println("Shutting down...")
}

func handleSetupCheck(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	cfg := config.Get()

	installed, path, _ := ollama.CheckOllamaInstalled()
	cfg.OllamaInstalled = installed
	cfg.OllamaPath = path

	running := ollama.IsOllamaRunning()
	config.Save()

	fmt.Fprintf(w, `{
		"setup_complete": %t,
		"ollama_installed": %t,
		"ollama_path": "%s",
		"ollama_running": %t
	}`, cfg.SetupComplete, installed, path, running)
}

func handleOllamaInstall(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	if r.Method != "POST" {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req struct {
		UseMirror bool `json:"use_mirror"`
	}
	_ = req

	go func() {
		var err error
		if req.UseMirror {
			err = ollama.DownloadOllamaMirror()
		} else {
			err = ollama.DownloadOllamaWindows()
		}

		if err == nil {
			cfg := config.Get()
			cfg.OllamaInstalled = true
			config.Save()
		}
	}()

	fmt.Fprintf(w, `{"status": "started"}`)
}

func handleModelSetup(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	models, _ := ollama.ListModels()
	var modelNames []string
	for _, m := range models {
		modelNames = append(modelNames, m.Name)
	}

	fmt.Fprintf(w, `{"models": ["%s"]}`, strings.Join(modelNames, `","`))
}

func handleConfigure(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	cfg := config.Get()
	cfg.SetupComplete = true
	config.Save()

	_ = r

	fmt.Fprintf(w, `{"status": "configured"}`)
}

func handleListModels(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	models, err := ollama.ListModels()
	if err != nil {
		fmt.Fprintf(w, `{"error": "%s"}`, err.Error())
		return
	}

	fmt.Fprintf(w, `{"models": %s}`, formatModelsJSON(models))
}

func formatModelsJSON(models []ollama.ModelInfo) string {
	if len(models) == 0 {
		return "[]"
	}

	result := "["
	for i, m := range models {
		if i > 0 {
			result += ","
		}
		result += fmt.Sprintf(`{"name":"%s","size":%d}`, m.Name, m.Size)
	}
	result += "]"
	return result
}

func handlePullModel(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	var req struct {
		ModelName string `json:"model_name"`
	}

	modelName := req.ModelName
	if modelName == "" {
		fmt.Fprintf(w, `{"error": "model_name required"}`)
		return
	}
	_ = r

	progressChan := make(chan ollama.PullProgress, 10)
	go func() {
		err := ollama.PullModel(modelName, progressChan)
		if err != nil {
			log.Printf("Pull error: %v", err)
		}
	}()

	go func() {
		for progress := range progressChan {
			log.Printf("Pull progress: %s - %d/%d", progress.Status, progress.Completed, progress.Total)
		}
	}()

	fmt.Fprintf(w, `{"status": "pulling"}`)
}

func handleChat(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	var req struct {
		Model    string `json:"model"`
		Messages []ollama.ChatMessage `json:"messages"`
	}
	_ = req

	if req.Model == "" {
		fmt.Fprintf(w, `{"error": "model required"}`)
		return
	}

	responseChan := make(chan ollama.ChatResponse, 10)
	go func() {
		err := ollama.Chat(req.Model, req.Messages, responseChan)
		if err != nil {
			log.Printf("Chat error: %v", err)
		}
	}()

	var fullResponse strings.Builder
	for resp := range responseChan {
		fullResponse.WriteString(resp.Message.Content)
		if resp.Done {
			break
		}
	}

	fmt.Fprintf(w, `{"response": "%s"}`, strings.ReplaceAll(fullResponse.String(), `"`, `\"`))
}

func handleSystemStats(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	stats, err := system.GetSystemStats()
	if err != nil {
		fmt.Fprintf(w, `{"error": "%s"}`, err.Error())
		return
	}

	sysStatsHistory = append(sysStatsHistory, *stats)
	if len(sysStatsHistory) > 60 {
		sysStatsHistory = sysStatsHistory[1:]
	}

	appUptime := time.Since(appStartTime).Seconds()
	fmt.Fprintf(w, `{"cpu": %.1f, "memory_used": %d, "memory_total": %d, "memory_percent": %.1f, "disk_used": %d, "disk_total": %d, "disk_percent": %.1f, "net_sent": %d, "net_recv": %d, "uptime": %d, "app_uptime": %.0f, "gpu_usage": %.1f}`,
		stats.CPUUsage, stats.MemoryUsed, stats.MemoryTotal, stats.MemoryPercent,
		stats.DiskUsed, stats.DiskTotal, stats.DiskPercent,
		stats.NetSent, stats.NetRecv, stats.Uptime, appUptime, stats.GPUUsage)
}

func handleGetConfig(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	cfg := config.Get()
	fmt.Fprintf(w, `{
		"auto_start": %t,
		"sleep_on_shutdown": %t,
		"silent_start": %t,
		"adaptive_mode": %t,
		"intranet_penetrate": %t,
		"penetrate_addr": "%s",
		"ipv4": "%s",
		"username": "%s"
	}`, cfg.AutoStart, cfg.SleepOnShutdown, cfg.SilentStart, cfg.AdaptiveMode,
		cfg.IntranetPenetrate, cfg.PenetrateAddr, cfg.IPv4, cfg.Username)
}

func handleUpdateConfig(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	cfg := config.Get()

	if r.URL.Query().Get("auto_start") == "true" {
		cfg.AutoStart = true
		system.SetAutoStartup(true)
	} else if r.URL.Query().Get("auto_start") == "false" {
		cfg.AutoStart = false
		system.SetAutoStartup(false)
	}

	config.Save()
	fmt.Fprintf(w, `{"status": "updated"}`)
}

func handleRemoteURL(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	cfg := config.Get()

	ipv4, _ := system.GetIPv4Address()
	if cfg.IPv4 == "" {
		cfg.IPv4 = ipv4
	}

	fmt.Fprintf(w, `{"remote_url": "http://%s:8081", "ipv4": "%s", "username": "%s"}`,
		cfg.IPv4, cfg.IPv4, cfg.Username)
}

func handleStatsWS(w http.ResponseWriter, r *http.Request) {
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("WebSocket upgrade error: %v", err)
		return
	}
	defer conn.Close()

	ticker := time.NewTicker(1 * time.Second)
	defer ticker.Stop()

	for {
		select {
		case <-ticker.C:
			stats, err := system.GetSystemStats()
			if err != nil {
				continue
			}

			msg := fmt.Sprintf(`{"cpu": %.1f, "memory_percent": %.1f, "disk_percent": %.1f, "gpu_usage": %.1f, "uptime": %d}`,
				stats.CPUUsage, stats.MemoryPercent, stats.DiskPercent, stats.GPUUsage, stats.Uptime)

			if err := conn.WriteMessage(websocket.TextMessage, []byte(msg)); err != nil {
				return
			}
		}
	}
}

func handlePenetrate(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")

	cfg := config.Get()
	fmt.Fprintf(w, `{"enabled": %t, "addr": "%s", "user": "%s"}`,
		cfg.IntranetPenetrate, cfg.PenetrateAddr, cfg.PenetrateUser)
}

func startRemoteControl() {
	rc := remote.NewRemoteControl(8081)
	go rc.Start()
	rc.BroadcastScreen()
}
