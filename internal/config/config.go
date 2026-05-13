package config

import (
	"encoding/json"
	"os"
	"path/filepath"
	"sync"
	"time"
)

type AppConfig struct {
	SetupComplete      bool           `json:"setup_complete"`
	OllamaInstalled    bool           `json:"ollama_installed"`
	OllamaPath         string         `json:"ollama_path"`
	DownloadedModels   []string       `json:"downloaded_models"`
	AutoStart          bool           `json:"auto_start"`
	SleepOnShutdown    bool           `json:"sleep_on_shutdown"`
	SilentStart        bool           `json:"silent_start"`
	AdaptiveMode       bool           `json:"adaptive_mode"`
	IntranetPenetrate  bool           `json:"intranet_penetrate"`
	PenetrateAddr      string         `json:"penetrate_addr"`
	PenetrateUser      string         `json:"penetrate_user"`
	PenetratePass      string         `json:"penetrate_pass"`
	IPv4               string         `json:"ipv4"`
	Username           string         `json:"username"`
	Password           string         `json:"password"`
	AIUsageLimit       float64        `json:"ai_usage_limit"`
	LastCheckTime      time.Time      `json:"last_check_time"`
}

var (
	configInstance *AppConfig
	configMutex    sync.Mutex
	configPath     string
)

func Init() (*AppConfig, error) {
	configMutex.Lock()
	defer configMutex.Unlock()

	configPath = filepath.Join(os.TempDir(), "local-ai-config.json")

	cfg := &AppConfig{}
	if data, err := os.ReadFile(configPath); err == nil {
		if err := json.Unmarshal(data, cfg); err != nil {
			return cfg, err
		}
	}

	configInstance = cfg
	return cfg, nil
}

func Get() *AppConfig {
	configMutex.Lock()
	defer configMutex.Unlock()
	return configInstance
}

func Save() error {
	configMutex.Lock()
	defer configMutex.Unlock()

	data, err := json.MarshalIndent(configInstance, "", "  ")
	if err != nil {
		return err
	}

	return os.WriteFile(configPath, data, 0644)
}
